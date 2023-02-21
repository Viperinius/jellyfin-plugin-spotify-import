using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using Microsoft.Extensions.Logging;

namespace Viperinius.Plugin.SpotifyImport
{
    internal class PlaylistSync
    {
        private readonly ILogger<PlaylistSync> _logger;
        private readonly IPlaylistManager _playlistManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly List<ProviderPlaylistInfo> _providerPlaylists;

        public PlaylistSync(
            ILogger<PlaylistSync> logger,
            IPlaylistManager playlistManager,
            ILibraryManager libraryManager,
            IUserManager userManager,
            List<ProviderPlaylistInfo> playlists)
        {
            _logger = logger;
            _playlistManager = playlistManager;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _providerPlaylists = playlists;
        }

        public async Task Execute(CancellationToken cancellationToken = default)
        {
            var user = GetUser();
            if (user == null)
            {
                _logger.LogError("Failed to determine user");
                return;
            }

            foreach (var providerPlaylist in _providerPlaylists)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var playlist = await GetOrCreatePlaylistByName(providerPlaylist.Name, user).ConfigureAwait(false);

                if (playlist == null)
                {
                    _logger.LogError("Failed to get Jellyfin playlist with name {Name}", providerPlaylist.Name);
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
                }
            }

            await _playlistManager.AddToPlaylistAsync(playlist.Id, newTracks, user.Id).ConfigureAwait(false);
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

                if (ItemMatchesTrackInfo(audioItem, providerTrackInfo))
                {
                    return audioItem;
                }
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

                if (ItemMatchesTrackInfo(audioItem, providerTrackInfo))
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

            return _userManager.Users.FirstOrDefault((User?)null);
        }

        private static bool ItemMatchesTrackInfo(Audio audioItem, ProviderTrackInfo trackInfo)
        {
            // TODO: check for track number as well?
            return audioItem.Name == trackInfo.Name &&
                   audioItem.AlbumEntity.Name == trackInfo.AlbumName &&
                   audioItem.AlbumEntity.Artists.Contains(trackInfo.AlbumArtistName) &&
                   audioItem.Artists.Contains(trackInfo.ArtistName);
        }
    }
}
