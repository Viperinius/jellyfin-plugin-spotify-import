using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public PlaylistSync(
            ILogger<PlaylistSync> logger,
            IPlaylistManager playlistManager,
            ILibraryManager libraryManager,
            IUserManager userManager,
            List<ProviderPlaylistInfo> playlists,
            Dictionary<string, string> userPlaylistIds)
        {
            _logger = logger;
            _playlistManager = playlistManager;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _providerPlaylists = playlists;
            _userPlaylistIds = userPlaylistIds;
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
                        UserName = targetUser.UserName
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

                var playlist = await GetOrCreatePlaylistByName(jfPlaylistName, user).ConfigureAwait(false);

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

                if (updateReason != ItemUpdateType.None)
                {
                    await _libraryManager.UpdateItemAsync(playlist, playlist.GetParent(), updateReason, cancellationToken).ConfigureAwait(false);
                }

                await FindTracksAndAddToPlaylist(playlist, providerPlaylist.Tracks, user, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task FindTracksAndAddToPlaylist(Playlist playlist, List<ProviderTrackInfo> providerTrackInfos, User user, CancellationToken cancellationToken)
        {
            var newTracks = new List<Guid>();
            var missingTracks = new List<ProviderTrackInfo>();

            foreach (var providerTrack in providerTrackInfos)
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
                    else if (Plugin.Instance?.Configuration.GenerateMissingTrackLists ?? false)
                    {
                        missingTracks.Add(providerTrack);
                    }
                }
            }

            await _playlistManager.AddToPlaylistAsync(playlist.Id, newTracks, user.Id).ConfigureAwait(false);

            if ((Plugin.Instance?.Configuration.GenerateMissingTrackLists ?? false) && missingTracks.Any())
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
            _logger.LogDebug(
                "Now processing provider track {Name} [{Album}][{AlbumArtist}][{Artist}]",
                providerTrackInfo.Name,
                providerTrackInfo.AlbumName,
                string.Join("#", providerTrackInfo.AlbumArtistNames),
                string.Join("#", providerTrackInfo.ArtistNames));

            var artist = GetArtist(providerTrackInfo);
            if (artist == null)
            {
                failedMatchCriterium = ItemMatchCriteria.Artists;
                return null;
            }

            var album = GetAlbum(artist, providerTrackInfo);
            if (album == null)
            {
                failedMatchCriterium = ItemMatchCriteria.AlbumName;
                return null;
            }

            if (!CheckAlbumArtist(album, providerTrackInfo))
            {
                failedMatchCriterium = ItemMatchCriteria.AlbumArtists;
                return null;
            }

            var track = GetTrack(album, providerTrackInfo);
            if (track == null)
            {
                failedMatchCriterium = ItemMatchCriteria.TrackName;
            }

            return track;
        }

        private MusicArtist? GetArtist(ProviderTrackInfo providerTrackInfo)
        {
            foreach (var artistName in providerTrackInfo.ArtistNames)
            {
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
                    _logger.LogDebug("Did not find any artists for the name {Name}", artistName);
                }
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

        private MusicAlbum? GetAlbum(MusicArtist artist, ProviderTrackInfo providerTrackInfo)
        {
            foreach (var item in artist.Children)
            {
                if (item is not MusicAlbum album)
                {
                    continue;
                }

                if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumName) ?? false)
                {
                    var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                    if (!TrackComparison.AlbumNameEqual(album, providerTrackInfo, level))
                    {
                        continue;
                    }
                }

                return album;
            }

            return null;
        }

        private Audio? GetTrack(MusicAlbum album, ProviderTrackInfo providerTrackInfo)
        {
            foreach (var item in album.Tracks)
            {
                _logger.LogDebug(
                    "> Checking server track {Name} [{Album}][{AlbumArtist}][{Artist}]",
                    item.Name,
                    album.Name,
                    string.Join("#", item.AlbumArtists),
                    string.Join("#", item.Artists));

                if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.TrackName) ?? false)
                {
                    var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
                    if (!TrackComparison.TrackNameEqual(item, providerTrackInfo, level))
                    {
                        continue;
                    }
                }

                return item;
            }

            return null;
        }

        private static bool CheckPlaylistForTrack(Playlist playlist, User user, ProviderTrackInfo providerTrackInfo)
        {
            var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
            foreach (var item in playlist.GetChildren(user, false))
            {
                if (item is not Audio audioItem)
                {
                    continue;
                }

                if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.Artists) ?? false) && !TrackComparison.ArtistOneContained(audioItem, providerTrackInfo, level))
                {
                    continue;
                }

                if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumName) ?? false) && !TrackComparison.AlbumNameEqual(audioItem, providerTrackInfo, level))
                {
                    continue;
                }

                if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumArtists) ?? false) && !TrackComparison.AlbumArtistOneContained(audioItem, providerTrackInfo, level))
                {
                    continue;
                }

                if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.TrackName) ?? false) && !TrackComparison.TrackNameEqual(audioItem, providerTrackInfo, level))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private async Task<Playlist?> GetOrCreatePlaylistByName(string name, User user)
        {
            var playlists = _playlistManager.GetPlaylists(user.Id);
            var playlist = playlists.Where(p => p.Name == name).FirstOrDefault();
            if (playlist != null)
            {
                return playlist;
            }

            var result = await _playlistManager.CreatePlaylist(new MediaBrowser.Model.Playlists.PlaylistCreationRequest
            {
                Name = name,
                MediaType = "Audio",
                UserId = user.Id
            }).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(result.Id))
            {
                playlists = _playlistManager.GetPlaylists(user.Id);
                return playlists.Where(p => p.Id.ToString().Replace("-", string.Empty, StringComparison.InvariantCulture) == result.Id).FirstOrDefault();
            }

            return null;
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
