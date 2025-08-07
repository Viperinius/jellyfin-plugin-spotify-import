using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Matchers;

namespace Viperinius.Plugin.SpotifyImport.Sync
{
    internal class StringMatchFinder : ITrackMatchFinder
    {
        private const int MaxSearchChars = 5;

        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;

        public StringMatchFinder(
            ILogger logger,
            ILibraryManager libraryManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
        }

        public ItemMatchCriteria LastFailedCriteria { get; private set; }

        public bool IsEnabled => true;

        public Audio? FindTrack(string providerId, ProviderTrackInfo providerTrackInfo)
        {
            if (!IsEnabled)
            {
                return null;
            }

            LastFailedCriteria = ItemMatchCriteria.None;
            var matchCandidates = new List<(int, ItemMatchLevel, Audio)>();

            var artistProviderNextIndex = 0;
            var artistJfNextIndex = 0;
            while (artistProviderNextIndex >= 0)
            {
                var artist = GetArtist(providerTrackInfo, ref artistProviderNextIndex, ref artistJfNextIndex);
                if (artist == null)
                {
                    LastFailedCriteria |= ItemMatchCriteria.Artists;
                    continue;
                }

                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("> Found matching artist {Name} {Id}", artist.Name, artist.Id);
                }

                var albumNextIndex = 0;
                while (albumNextIndex >= 0)
                {
                    var album = GetAlbum(artist, providerTrackInfo, ref albumNextIndex);
                    if (album == null)
                    {
                        LastFailedCriteria |= ItemMatchCriteria.AlbumName;
                        continue;
                    }

                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation("> Found matching album {Name} {Id}", album.Name, album.Id);
                    }

                    if (!CheckAlbumArtist(album, providerTrackInfo))
                    {
                        LastFailedCriteria |= ItemMatchCriteria.AlbumArtists;
                        continue;
                    }

                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation("> Album artists ok");
                    }

                    var tracks = GetTrack(album, providerTrackInfo);
                    matchCandidates.AddRange(tracks);
                    if (!tracks.Any())
                    {
                        LastFailedCriteria |= ItemMatchCriteria.TrackName;
                    }
                }
            }

            if (matchCandidates.Count > 0)
            {
                // sort by prio first, then match level
                matchCandidates.Sort((a, b) =>
                {
                    var result = a.Item1.CompareTo(b.Item1);
                    if (result == 0)
                    {
                        result = a.Item2.CompareTo(b.Item2);
                    }

                    return result;
                });
                LastFailedCriteria = ItemMatchCriteria.None;
                return matchCandidates.First().Item3;
            }

            return null;
        }

        private MusicArtist? GetArtist(ProviderTrackInfo providerTrackInfo, ref int nextProviderArtistIndex, ref int nextJfArtistIndex)
        {
            var artistName = providerTrackInfo.ArtistNames.ElementAtOrDefault(nextProviderArtistIndex);
            if (string.IsNullOrEmpty(artistName))
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("> Reached end of provider artist list");
                }

                nextProviderArtistIndex = -1;
                return null;
            }

            // only search for the first few characters to increase the chances of finding artists with slightly differing names between provider and jellyfin
            var queryResult = _libraryManager.GetArtists(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                SearchTerm = artistName[0..Math.Min(artistName.Length, MaxSearchChars)],
            });

            if (queryResult.Items.Count == nextJfArtistIndex || queryResult.Items.Count == 0)
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("> Reached end of jellyfin artist list");
                    if (queryResult.Items.Count == 0)
                    {
                        _logger.LogInformation("> Did not find any artists for the name {Name}", artistName);
                    }
                }

                nextProviderArtistIndex++;
                nextJfArtistIndex = 0;
                return null;
            }

            var (item, _) = queryResult.Items.ElementAt(nextJfArtistIndex);
            nextJfArtistIndex++;

            if (item is not MusicArtist artist)
            {
                return null;
            }

            if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.Artists) ?? false)
            {
                var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                if (!TrackComparison.ArtistOneContained(artist, providerTrackInfo, level))
                {
                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation(
                            "> Artist did not match: {JName} [Jellyfin, {Id}], {PName} [Provider]",
                            artist.Name,
                            artist.Id,
                            string.Join("#", providerTrackInfo.ArtistNames));
                    }

                    return null;
                }
            }

            return artist;
        }

        private bool CheckAlbumArtist(MusicAlbum album, ProviderTrackInfo providerTrackInfo)
        {
            if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumArtists) ?? false)
            {
                var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                return TrackComparison.AlbumArtistOneContained(album, providerTrackInfo, level);
            }

            return true;
        }

        private MusicAlbum? GetAlbum(MusicArtist artist, ProviderTrackInfo providerTrackInfo, ref int nextAlbumIndex)
        {
            var albums = artist.Children;
            if (!albums.Any())
            {
                // for whatever reason albums are apparently not always set as children of the artist... so try to find them using album artist
                albums = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                {
                    AlbumArtistIds = new[] { artist.Id },
                    IncludeItemTypes = new[] { BaseItemKind.MusicAlbum }
                });
                albums ??= new List<MediaBrowser.Controller.Entities.BaseItem>();
            }

            var item = albums.ElementAtOrDefault(nextAlbumIndex);
            nextAlbumIndex++;
            if (item == null)
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("> Reached end of album list (has {Count} entries)", albums.Count());
                }

                nextAlbumIndex = -1;
                return null;
            }

            if (item is not MusicAlbum album)
            {
                return null;
            }

            if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumName) ?? false)
            {
                var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                if (!TrackComparison.AlbumNameEqual(album, providerTrackInfo, level).ComparisonResult)
                {
                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation(
                            "> Album did not match: {JName} [Jellyfin, {Id}], {PName} [Provider]",
                            album.Name,
                            album.Id,
                            providerTrackInfo.AlbumName);
                    }

                    return null;
                }
            }

            return album;
        }

        private IEnumerable<(int Prio, ItemMatchLevel Level, Audio Item)> GetTrack(MusicAlbum album, ProviderTrackInfo providerTrackInfo)
        {
            foreach (var item in album.Tracks)
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation(
                        ">> Checking server track {Name} [{Album}][{AlbumArtist}][{Artist}]",
                        item.Name,
                        album.Name,
                        string.Join("#", item.AlbumArtists),
                        string.Join("#", item.Artists));
                }

                var prio = int.MaxValue;
                var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.TrackName) ?? false)
                {
                    var checkResult = TrackComparison.TrackNameEqual(item, providerTrackInfo, level);
                    if (!checkResult.ComparisonResult || checkResult.MatchedLevel == null || checkResult.MatchedPrio == null)
                    {
                        continue;
                    }
                    else
                    {
                        prio = (int)checkResult.MatchedPrio;
                        level = (ItemMatchLevel)checkResult.MatchedLevel;
                        if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                        {
                            _logger.LogInformation("> Found matching potential track {Name} {Id}", item.Name, item.Id);
                        }
                    }
                }

                yield return (prio, level, item);
            }

            yield break;
        }
    }
}
