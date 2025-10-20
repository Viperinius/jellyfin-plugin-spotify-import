using System;
using System.Collections.Generic;
using System.Linq;
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
            CachedPlaylists = new List<string>();
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

        /// <summary>
        /// Gets or sets the list of playlists that were populated from cache.
        /// </summary>
        public List<string> CachedPlaylists { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether the provider is initialised.
        /// </summary>
        public abstract bool IsSetUp { get; protected set; }

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
                        var tracks = GetCachedPlaylistTracksFromDb(playlist.Id);
                        if (tracks != null)
                        {
                            playlist.Tracks = tracks;
                            Playlists.Add(playlist);
                            CachedPlaylists.Add(playlist.Id);
                            _logger.LogInformation("Using cached last state for provider playlist {Name} ({Id}) as there are no changes", playlist.Name, playlist.Id);
                            continue;
                        }
                    }

                    if (playlist.Tracks.Count == 0)
                    {
                        playlist = await GetPlaylist(playlistId, true, cancellationToken).ConfigureAwait(false);
                    }

                    if (playlist != null)
                    {
                        if (_dbRepository.UpsertProviderPlaylist(Name, playlist) == null)
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

                            if (_dbRepository.InsertProviderTrack(Name, track) == null)
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

        public virtual bool UpdatePlaylistTracksInDb(CancellationToken? cancellationToken = null)
        {
            _logger.LogInformation("Starting to update tracks of {Amount} playlists from {Name} in db", Playlists.Count, Name);
            var result = true;

            foreach (var playlist in Playlists)
            {
                if (cancellationToken?.IsCancellationRequested ?? false)
                {
                    _logger.LogWarning("Updating playlist tracks in db cancelled before saving playlist {Name} ({Id})", playlist.Name, playlist.Id);
                    return false;
                }

                if (CachedPlaylists.Contains(playlist.Id))
                {
                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation("Skipped updating cached playlist tracks in db for {Name} ({Id})", playlist.Name, playlist.Id);
                    }

                    continue;
                }

                var playlistDbId = _dbRepository.GetProviderPlaylistDbId(Name, playlist.Id);
                if (playlistDbId == null)
                {
                    result = false;
                    _logger.LogError("No playlist with name {Name} ({Id}) found in db for provider {Prov}", playlist.Name, playlist.Id, Name);
                    continue;
                }

                _dbRepository.DeleteProviderPlaylistTracks((int)playlistDbId);

                for (var ii = 0; ii < playlist.Tracks.Count; ii++)
                {
                    var track = playlist.Tracks[ii];
                    if (string.IsNullOrEmpty(track.Id))
                    {
                        result = false;
                        _logger.LogError("Track has empty / invalid id: {Name}", track.Name);
                        continue;
                    }

                    var trackDbId = _dbRepository.GetProviderTrackDbId(Name, track.Id);
                    if (trackDbId == null)
                    {
                        result = false;
                        _logger.LogError("No track with name {Name} ({Id}) found in db for provider {Prov}", track.Name, track.Id, Name);
                        continue;
                    }

                    if (_dbRepository.UpsertProviderPlaylistTrack((int)playlistDbId, (int)trackDbId, ii) == null)
                    {
                        result = false;
                        _logger.LogError("Failed to insert playlist track {Name} ({Id}) into db", track.Name, track.Id);
                    }
                }
            }

            return result;
        }

        protected abstract Task<List<ProviderPlaylistInfo>?> GetUserPlaylistsInfo(
            TargetUserConfiguration target,
            CancellationToken? cancellationToken = null);

        protected abstract Task<ProviderPlaylistInfo?> GetPlaylist(string playlistId, bool includeTracks, CancellationToken? cancellationToken = null);

        protected abstract Task<ProviderTrackInfo?> GetTrack(string trackId, CancellationToken? cancellationToken = null);

        protected abstract string CreatePlaylistState<T>(T data);

        private List<ProviderTrackInfo>? GetCachedPlaylistTracksFromDb(string providerPlaylistId)
        {
            var playlistDbId = _dbRepository.GetProviderPlaylistDbId(Name, providerPlaylistId);
            if (playlistDbId == null)
            {
                return null;
            }

            var result = new List<ProviderTrackInfo>();
            foreach (var (trackDbId, position) in _dbRepository.GetProviderPlaylistTracks((int)playlistDbId))
            {
                var track = _dbRepository.GetProviderTrack(Name, trackDbId);
                if (track == null)
                {
                    _logger.LogError("No track with id {Id} found in db for provider {Prov}", trackDbId, Name);
                    return null;
                }

                if (result.Count <= position)
                {
                    result.AddRange(new ProviderTrackInfo[position + 1 - result.Count]);
                }

                result[position] = track;
            }

            return result;
        }
    }
}
