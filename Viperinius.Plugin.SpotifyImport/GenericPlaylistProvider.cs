using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Configuration;
using Viperinius.Plugin.SpotifyImport.Utils;

namespace Viperinius.Plugin.SpotifyImport
{
    internal abstract class GenericPlaylistProvider
    {
        private readonly ILogger<GenericPlaylistProvider> _logger;
        private readonly DbRepository _dbRepository;

        protected GenericPlaylistProvider(DbRepository dbRepository, ILogger<GenericPlaylistProvider> logger)
        {
            _logger = logger;
            _dbRepository = dbRepository;
            Playlists = new List<ProviderPlaylistInfo>();
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the base URL to the provider API.
        /// </summary>
        public abstract Uri ApiUrl { get; }

        /// <summary>
        /// Gets or sets the auth token for the API (if needed).
        /// </summary>
        public abstract object? AuthToken { get; set; }

        /// <summary>
        /// Gets or sets the list of playlist sources.
        /// </summary>
        public List<ProviderPlaylistInfo> Playlists { get; protected set; }

        public abstract void SetUpProvider();

        public virtual async Task<List<string>?> GetUserPlaylistIds(
            TargetUserConfiguration target,
            CancellationToken? cancellationToken = null)
        {
            var playlistsInfo = await GetUserPlaylistsInfo(target, cancellationToken).ConfigureAwait(false);
            return playlistsInfo?.Select(p => p.Id).ToList();
        }

        public virtual async Task PopulatePlaylists(List<string> playlistIds, CancellationToken? cancellationToken = null)
        {
            _logger.LogInformation("Starting to query {Amount} playlists from {Name}", playlistIds.Count, Name);

            foreach (var playlistId in playlistIds)
            {
                var playlist = await GetPlaylist(playlistId, false, cancellationToken).ConfigureAwait(false);
                if (playlist != null)
                {
                    var previousState = _dbRepository.GetLastProviderPlaylistState(Name, playlist.Id);
                    if (!string.IsNullOrEmpty(previousState) && playlist.State == previousState)
                    {
                        _logger.LogInformation("Skipped playlist {Name} ({Id}) as there are no changes", playlist.Name, playlist.Id);
                        // TODO: dont fully skip the playlist but load the previous tracks from db to pass to playlist sync (in case new jellyfin matches are available)
                        continue;
                    }

                    playlist = await GetPlaylist(playlistId, true, cancellationToken).ConfigureAwait(false);
                    if (playlist != null)
                    {
                        if (!_dbRepository.UpsertProviderPlaylist(Name, playlist))
                        {
                            _logger.LogError("Failed to update / insert playlist {Name} ({Id}) into db", playlist.Name, playlistId);
                        }

                        Playlists.Add(playlist);

                        foreach (var track in playlist.Tracks)
                        {
                            if (string.IsNullOrEmpty(track.Id))
                            {
                                _logger.LogError("Track has empty / invalid id: {Name}", track.Name);
                                continue;
                            }

                            if (!_dbRepository.InsertProviderTrack(Name, track))
                            {
                                _logger.LogError("Failed to insert track {Name} ({Id}) into db", track.Name, track.Id);
                            }
                        }
                    }
                }

                if (playlist == null)
                {
                    _logger.LogError("Failed to get playlist with id {Id}", playlistId);
                }
            }
        }

        protected abstract Task<List<ProviderPlaylistInfo>?> GetUserPlaylistsInfo(
            TargetUserConfiguration target,
            CancellationToken? cancellationToken = null);

        protected abstract Task<ProviderPlaylistInfo?> GetPlaylist(string playlistId, bool includeTracks, CancellationToken? cancellationToken = null);

        protected abstract Task<ProviderTrackInfo?> GetTrack(string trackId, CancellationToken? cancellationToken = null);

        protected abstract string CreatePlaylistState<T>(T data);
    }
}
