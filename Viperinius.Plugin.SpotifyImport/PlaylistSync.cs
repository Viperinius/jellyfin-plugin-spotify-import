using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Configuration;
using Viperinius.Plugin.SpotifyImport.Matchers;

namespace Viperinius.Plugin.SpotifyImport
{
    internal class PlaylistSync
    {
        private const int MaxSearchChars = 5;

        private readonly ILogger<PlaylistSync> _logger;
        private readonly IPlaylistManager _playlistManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly List<ProviderPlaylistInfo> _providerPlaylists;
        private readonly Dictionary<string, string> _userPlaylistIds;
        private readonly ManualMapStore _manualMapStore;

        public PlaylistSync(
            ILogger<PlaylistSync> logger,
            IPlaylistManager playlistManager,
            ILibraryManager libraryManager,
            IUserManager userManager,
            List<ProviderPlaylistInfo> playlists,
            Dictionary<string, string> userPlaylistIds,
            ManualMapStore manualMapStore)
        {
            _logger = logger;
            _playlistManager = playlistManager;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _providerPlaylists = playlists;
            _userPlaylistIds = userPlaylistIds;
            _manualMapStore = manualMapStore;
        }

        public async Task Execute(CancellationToken cancellationToken = default)
        {
            foreach (var providerPlaylist in _providerPlaylists)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // get the targeted playlist configuration
                var targetConfig = Plugin.Instance?.Configuration.Playlists.FirstOrDefault(p => p.Id == providerPlaylist.Id);

                if (targetConfig == null || string.IsNullOrEmpty(targetConfig.Id))
                {
                    // is this a playlist specified by user?
                    var userId = _userPlaylistIds.GetValueOrDefault(providerPlaylist.Id);
                    var targetUser = Plugin.Instance?.Configuration.Users.FirstOrDefault(u => u.Id == userId);

                    if (targetUser == null || string.IsNullOrEmpty(targetUser.Id))
                    {
                        _logger.LogError("Failed to get target playlist configuration for playlist {Id}", providerPlaylist.Id);
                        continue;
                    }

                    targetConfig = new TargetPlaylistConfiguration
                    {
                        Id = targetUser.Id,
                        Name = string.Empty,
                        UserName = targetUser.UserName,
                        IsPrivate = targetUser.IsPrivate,
                        RecreateFromScratch = false,
                    };
                }

                // get the targeted user
                var user = GetUser(targetConfig.UserName);
                if (user == null)
                {
                    _logger.LogError("Failed to get user {Name}", targetConfig.UserName);
                    continue;
                }

                // determine jellyfin playlist name
                var jfPlaylistName = targetConfig.Name;
                if (string.IsNullOrEmpty(jfPlaylistName))
                {
                    jfPlaylistName = providerPlaylist.Name;
                }

                var playlist = await GetOrCreatePlaylistByName(jfPlaylistName, user, targetConfig.IsPrivate, targetConfig.RecreateFromScratch).ConfigureAwait(false);

                if (playlist == null)
                {
                    _logger.LogError("Failed to get Jellyfin playlist with name {Name}", jfPlaylistName);
                    continue;
                }

                var updateReason = ItemUpdateType.None;

                if (providerPlaylist.ImageUrl != null && !playlist.HasImage(ImageType.Primary, 0))
                {
                    playlist.AddImage(new MediaBrowser.Controller.Entities.ItemImageInfo
                    {
                        Path = providerPlaylist.ImageUrl.ToString()
                    });
                    updateReason |= ItemUpdateType.ImageUpdate;
                }

                if (!string.IsNullOrWhiteSpace(providerPlaylist.Description) && providerPlaylist.Description != playlist.Overview)
                {
                    playlist.Overview = providerPlaylist.Description;
                    updateReason |= ItemUpdateType.MetadataEdit;
                }

                if ((!targetConfig.IsPrivate) != playlist.OpenAccess)
                {
                    playlist.OpenAccess = !targetConfig.IsPrivate;
                    updateReason |= ItemUpdateType.MetadataEdit;
                }

                if (updateReason != ItemUpdateType.None)
                {
                    await _libraryManager.UpdateItemAsync(playlist, playlist.GetParent(), updateReason, cancellationToken).ConfigureAwait(false);
                }

                await FindTracksAndAddToPlaylist(playlist, providerPlaylist, user, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task FindTracksAndAddToPlaylist(Playlist playlist, ProviderPlaylistInfo providerPlaylistInfo, User user, CancellationToken cancellationToken)
        {
            var newTracks = new List<Guid>();
            var missingTracks = new List<ProviderTrackInfo>();

            foreach (var providerTrack in providerPlaylistInfo.Tracks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!CheckPlaylistForTrack(playlist, user, providerTrack))
                {
                    var track = GetMatchingTrack(providerTrack, out var failedCriterium);
                    if (failedCriterium != ItemMatchCriteria.None && (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false))
                    {
                        _logger.LogInformation(
                            "{Criterium} did not match for track {Name} [{Album}][{Artist}]",
                            failedCriterium,
                            providerTrack.Name,
                            providerTrack.AlbumName,
                            string.Join("#", providerTrack.ArtistNames));
                    }

                    if (track != null)
                    {
                        newTracks.Add(track.Id);
                    }
                    else
                    {
                        missingTracks.Add(providerTrack);
                    }
                }
            }

            await _playlistManager.AddItemToPlaylistAsync(playlist.Id, newTracks, user.Id).ConfigureAwait(false);
            await UpdatePlaylistCompletenessDesc(playlist, providerPlaylistInfo, missingTracks.Count, providerPlaylistInfo.Tracks.Count, cancellationToken).ConfigureAwait(false);

            if ((Plugin.Instance?.Configuration.GenerateMissingTrackLists ?? false) && missingTracks.Count > 0)
            {
                var missingFilePath = MissingTrackStore.GetFilePath(playlist.Name);

                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("Write missing tracks file with {Count} entries to {Path}", missingTracks.Count, missingFilePath);
                }

                await MissingTrackStore.WriteFile(missingFilePath, missingTracks).ConfigureAwait(false);
            }
        }

        protected Audio? GetMatchingTrack(ProviderTrackInfo providerTrackInfo, out ItemMatchCriteria failedMatchCriterium)
        {
            failedMatchCriterium = ItemMatchCriteria.None;
            if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
            {
                _logger.LogInformation(
                    "Now processing provider track {Name} [{Album}][{AlbumArtist}][{Artist}]",
                    providerTrackInfo.Name,
                    providerTrackInfo.AlbumName,
                    string.Join("#", providerTrackInfo.AlbumArtistNames),
                    string.Join("#", providerTrackInfo.ArtistNames));
            }

            var manualTrack = _manualMapStore.GetByProviderTrackInfo(providerTrackInfo);
            if (manualTrack?.Provider.Equals(providerTrackInfo) ?? false)
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogDebug("Found manual mapping for track with id {Id}", manualTrack.Jellyfin.Track);
                }

                return _libraryManager.GetItemById<Audio>(Guid.Parse(manualTrack.Jellyfin.Track));
            }

            if (Plugin.Instance?.Configuration.UseLegacyMatching ?? false)
            {
                return GetMatchingTrackLegacy(providerTrackInfo, out failedMatchCriterium);
            }

            var matchCandidates = new List<(int, ItemMatchLevel, Audio)>();

            var artistNextIndex = 0;
            while (artistNextIndex >= 0)
            {
                var artist = GetArtist(providerTrackInfo, ref artistNextIndex);
                if (artist == null)
                {
                    failedMatchCriterium |= ItemMatchCriteria.Artists;
                    continue;
                }

                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogDebug("> Found matching artist {Name} {Id}", artist.Name, artist.Id);
                }

                var albumNextIndex = 0;
                while (albumNextIndex >= 0)
                {
                    var album = GetAlbum(artist, providerTrackInfo, ref albumNextIndex);
                    if (album == null)
                    {
                        failedMatchCriterium |= ItemMatchCriteria.AlbumName;
                        continue;
                    }

                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogDebug("> Found matching album {Name} {Id}", album.Name, album.Id);
                    }

                    if (!CheckAlbumArtist(album, providerTrackInfo))
                    {
                        failedMatchCriterium |= ItemMatchCriteria.AlbumArtists;
                        continue;
                    }

                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogDebug("> Album artists ok");
                    }

                    var tracks = GetTrack(album, providerTrackInfo);
                    matchCandidates.AddRange(tracks);
                    if (!tracks.Any())
                    {
                        failedMatchCriterium |= ItemMatchCriteria.TrackName;
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
                failedMatchCriterium = ItemMatchCriteria.None;
                return matchCandidates.First().Item3;
            }

            return null;
        }

        private Audio? GetMatchingTrackLegacy(ProviderTrackInfo providerTrackInfo, out ItemMatchCriteria failedMatchCriterium)
        {
            failedMatchCriterium = ItemMatchCriteria.None;
            if (!(Plugin.Instance?.Configuration.UseLegacyMatching ?? false))
            {
                return null;
            }

            var queryResult = _libraryManager.GetItemsResult(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                SearchTerm = providerTrackInfo.Name[0..Math.Min(providerTrackInfo.Name.Length, MaxSearchChars)],
                MediaTypes = new[] { MediaType.Audio }
            });

            if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
            {
                _logger.LogDebug("> Found {Count} tracks when searching for {Name}", queryResult.Items.Count, providerTrackInfo.Name);
            }

            foreach (var item in queryResult.Items)
            {
                if (item is not Audio audioItem)
                {
                    continue;
                }

                if (ItemMatchesTrackInfo(audioItem, providerTrackInfo, out failedMatchCriterium))
                {
                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogDebug("> Found matching track {Name} {Id}", audioItem.Name, audioItem.Id);
                    }

                    return audioItem;
                }
            }

            return null;
        }

        private MusicArtist? GetArtist(ProviderTrackInfo providerTrackInfo, ref int nextArtistIndex)
        {
            var artistName = providerTrackInfo.ArtistNames.ElementAtOrDefault(nextArtistIndex);
            nextArtistIndex++;
            if (string.IsNullOrEmpty(artistName))
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("> Reached end of artist list");
                }

                nextArtistIndex = -1;
                return null;
            }

            // only search for the first few characters to increase the chances of finding artists with slightly differing names between provider and jellyfin
            var queryResult = _libraryManager.GetArtists(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                SearchTerm = artistName[0..Math.Min(artistName.Length, MaxSearchChars)],
            });

            foreach (var (item, _) in queryResult.Items)
            {
                if (item is not MusicArtist artist)
                {
                    continue;
                }

                if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.Artists) ?? false)
                {
                    var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                    if (!TrackComparison.ArtistOneContained(artist, providerTrackInfo, level))
                    {
                        continue;
                    }
                }

                return artist;
            }

            if (queryResult.Items.Count == 0 && (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false))
            {
                _logger.LogDebug("> Did not find any artists for the name {Name}", artistName);
            }

            return null;
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
                    _logger.LogDebug(
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
                            _logger.LogDebug("> Found matching potential track {Name} {Id}", item.Name, item.Id);
                        }
                    }
                }

                yield return (prio, level, item);
            }

            yield break;
        }

        private bool CheckPlaylistForTrack(Playlist playlist, User user, ProviderTrackInfo providerTrackInfo)
        {
            foreach (var item in playlist.GetChildren(user, false))
            {
                if (item is not Audio audioItem)
                {
                    continue;
                }

                var manualTrack = _manualMapStore.GetByTrackId(audioItem.Id);
                if (manualTrack?.Provider.Equals(providerTrackInfo) ?? false)
                {
                    return true;
                }

                if (ItemMatchesTrackInfo(audioItem, providerTrackInfo, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ItemMatchesTrackInfo(Audio audioItem, ProviderTrackInfo trackInfo, out ItemMatchCriteria failedCriterium)
        {
            var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
            failedCriterium = ItemMatchCriteria.None;

            if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.Artists) ?? false) && !TrackComparison.ArtistOneContained(audioItem, trackInfo, level))
            {
                failedCriterium = ItemMatchCriteria.Artists;
                return false;
            }

            if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumName) ?? false) && !TrackComparison.AlbumNameEqual(audioItem, trackInfo, level).ComparisonResult)
            {
                failedCriterium = ItemMatchCriteria.AlbumName;
                return false;
            }

            if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumArtists) ?? false) && !TrackComparison.AlbumArtistOneContained(audioItem, trackInfo, level))
            {
                failedCriterium = ItemMatchCriteria.AlbumArtists;
                return false;
            }

            if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.TrackName) ?? false) && !TrackComparison.TrackNameEqual(audioItem, trackInfo, level).ComparisonResult)
            {
                failedCriterium = ItemMatchCriteria.TrackName;
                return false;
            }

            return true;
        }

        private async Task<Playlist?> GetOrCreatePlaylistByName(string name, User user, bool shouldBePrivate, bool deleteExistingPlaylist)
        {
            var playlists = _playlistManager.GetPlaylists(user.Id);
            var playlist = playlists.Where(p => p.Name == name).FirstOrDefault();

            if (playlist != null && deleteExistingPlaylist)
            {
                _libraryManager.DeleteItem(playlist, new DeleteOptions { DeleteFileLocation = true }, true);

                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("Deleted existing playlist {Name} ({Id})", playlist.Name, playlist.Id);
                }

                playlist = null;
            }

            if (playlist == null)
            {
                var result = await _playlistManager.CreatePlaylist(new MediaBrowser.Model.Playlists.PlaylistCreationRequest
                {
                    Name = name,
                    MediaType = MediaType.Audio,
                    UserId = user.Id,
                    Public = !shouldBePrivate,
                }).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(result.Id))
                {
                    playlists = _playlistManager.GetPlaylists(user.Id);
                    playlist = playlists.Where(p => p.Id.ToString().Replace("-", string.Empty, StringComparison.InvariantCulture) == result.Id).FirstOrDefault((Playlist?)null);
                }
            }

            if (playlist == null)
            {
                return null;
            }

            // don't just return the previous playlist instance because .GetManageableItems() returns "garbage" LinkedChild ids for that one (TODO: is this a jellyfin bug?)
            return _libraryManager.GetItemById(playlist.Id) as Playlist;
        }

        private async Task UpdatePlaylistCompletenessDesc(Playlist playlist, ProviderPlaylistInfo providerPlaylistInfo, int missingTracks, int totalTracks, CancellationToken cancellationToken)
        {
            if (!(Plugin.Instance?.Configuration.ShowCompletenessInformation ?? false))
            {
                return;
            }

            if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
            {
                _logger.LogInformation("Update completeness for {Name} (Provider Id {Id})", playlist.Name, providerPlaylistInfo.Id);
            }

            playlist.Tagline = $"Synced {totalTracks - missingTracks} out of {totalTracks} tracks " +
                               $"from {providerPlaylistInfo.ProviderName} playlist {providerPlaylistInfo.Id} (at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [UTC])";

            await _libraryManager.UpdateItemAsync(playlist, playlist.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        }

        private User? GetUser(string? username = null)
        {
            if (!string.IsNullOrWhiteSpace(username))
            {
                return _userManager.GetUserByName(username);
            }

            return _userManager.Users.FirstOrDefault(u => u?.HasPermission(PermissionKind.IsAdministrator) ?? false, null);
        }
    }
}
