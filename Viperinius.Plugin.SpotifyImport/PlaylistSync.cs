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
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Configuration;
using Viperinius.Plugin.SpotifyImport.Matchers;

namespace Viperinius.Plugin.SpotifyImport
{
    internal class PlaylistSync
    {
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

                if (providerPlaylist.ImageUrl != null && !playlist.HasImage(MediaBrowser.Model.Entities.ImageType.Primary, 0))
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
                    var track = GetTrack(providerTrack);

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

        private Audio? GetTrack(ProviderTrackInfo providerTrackInfo)
        {
            var queryResult = _libraryManager.GetItemsResult(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                NameContains = providerTrackInfo.Name,
                MediaTypes = new[] { "Audio" }
            });

            foreach (var item in queryResult.Items)
            {
                if (item is not Audio audioItem)
                {
                    continue;
                }

                if (ItemMatchesTrackInfo(audioItem, providerTrackInfo, out var failedCriterium))
                {
                    return audioItem;
                }

                if (failedCriterium != ItemMatchCriteria.None && (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false))
                {
                    _logger.LogDebug("{Criterium} did not match for track {Name} [{Artist}]", failedCriterium, providerTrackInfo.Name, providerTrackInfo.ArtistName);
                }
            }

            if (queryResult.Items.Count == 0 && (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false))
            {
                _logger.LogDebug("Did not find any tracks containing the name {Name}", providerTrackInfo.Name);
            }

            return null;
        }

        private static bool CheckPlaylistForTrack(Playlist playlist, User user, ProviderTrackInfo providerTrackInfo)
        {
            foreach (var item in playlist.GetChildren(user, false))
            {
                if (item is not Audio audioItem)
                {
                    continue;
                }

                if (ItemMatchesTrackInfo(audioItem, providerTrackInfo, out _))
                {
                    return true;
                }
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

        protected static bool ItemMatchesTrackInfo(Audio audioItem, ProviderTrackInfo trackInfo, out ItemMatchCriteria failedCriterium)
        {
            // TODO: check for track number as well?

            var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
            failedCriterium = ItemMatchCriteria.None;
            var result = false;

            if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.TrackName) ?? false)
            {
                result = TrackComparison.TrackNameEqual(audioItem, trackInfo, level);
                if (!result)
                {
                    failedCriterium = ItemMatchCriteria.TrackName;
                    return result;
                }
            }

            if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumName) ?? false)
            {
                result = TrackComparison.AlbumNameEqual(audioItem, trackInfo, level);
                if (!result)
                {
                    failedCriterium = ItemMatchCriteria.AlbumName;
                    return result;
                }
            }

            if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumArtists) ?? false)
            {
                result = TrackComparison.AlbumArtistContained(audioItem, trackInfo, level);
                if (!result)
                {
                    failedCriterium = ItemMatchCriteria.AlbumArtists;
                    return result;
                }
            }

            if (Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.Artists) ?? false)
            {
                result = TrackComparison.ArtistContained(audioItem, trackInfo, level);
                if (!result)
                {
                    failedCriterium = ItemMatchCriteria.Artists;
                    return result;
                }
            }

            return result;
        }
    }
}
