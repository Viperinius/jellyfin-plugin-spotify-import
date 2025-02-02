using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using Viperinius.Plugin.SpotifyImport.Configuration;
using Viperinius.Plugin.SpotifyImport.Utils;

namespace Viperinius.Plugin.SpotifyImport.Spotify
{
    internal class SpotifyPlaylistProvider : GenericPlaylistProvider
    {
        private const string ProviderName = "Spotify";
        private readonly ILogger<SpotifyPlaylistProvider> _logger;
        private readonly ILogger<SpotifyLogger> _apiLogger;
        private SpotifyClient? _spotifyClient;
        private static readonly SpotifyClientConfig _defaultSpotifyConfig = SpotifyClientConfig.CreateDefault();

        public SpotifyPlaylistProvider(
            DbRepository dbRepository,
            ILogger<SpotifyPlaylistProvider> logger,
            ILogger<SpotifyLogger> apiLogger) : base(dbRepository, logger)
        {
            _logger = logger;
            _apiLogger = apiLogger;
        }

        public override string Name => ProviderName;

        public override Uri ApiUrl => throw new NotImplementedException();

        public override object? AuthToken { get; set; }

        public override void SetUpProvider()
        {
            if (string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration.SpotifyClientId) ||
                Plugin.Instance?.Configuration.SpotifyAuthToken == null ||
                string.IsNullOrEmpty(Plugin.Instance?.Configuration.SpotifyAuthToken.AccessToken))
            {
                _logger.LogError("Missing Spotify auth token or client ID!");
                return;
            }

            var authenticator = new PKCEAuthenticator(
                                        Plugin.Instance!.Configuration.SpotifyClientId,
                                        Plugin.Instance!.Configuration.SpotifyAuthToken);
            authenticator.TokenRefreshed += (ev, token) =>
            {
                Plugin.Instance!.Configuration.SpotifyAuthToken = token;
                Plugin.Instance!.SaveConfiguration();
            };

            var config = _defaultSpotifyConfig.WithHTTPLogger(new SpotifyLogger(_apiLogger))
                                              .WithAuthenticator(authenticator);
            _spotifyClient = new SpotifyClient(config);
        }

        protected override async Task<List<ProviderPlaylistInfo>?> GetUserPlaylistsInfo(
            TargetUserConfiguration target,
            CancellationToken? cancellationToken = null)
        {
            if (_spotifyClient == null)
            {
                return null;
            }

            Paging<FullPlaylist>? spotifyPlaylists = null;

            try
            {
                var playlists = new List<ProviderPlaylistInfo>();
                spotifyPlaylists = await _spotifyClient.Playlists.GetUsers(target.Id, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
                await foreach (var playlist in _spotifyClient.Paginate(spotifyPlaylists).ConfigureAwait(false))
                {
                    if (cancellationToken?.IsCancellationRequested ?? false)
                    {
                        return null;
                    }

                    if (playlist == null)
                    {
                        // playlist can be null if it is owned by Spotify (as of 27.11.2024 the Spotify API does not return those anymore...)
                        continue;
                    }

                    var ownerId = playlist.Owner != null ? playlist.Owner.Id : string.Empty;

                    if (target.OnlyOwnPlaylists && ownerId != target.Id)
                    {
                        continue;
                    }

                    var rawImageUrl = playlist.Images?.FirstOrDefault((Image?)null)?.Url;

                    playlists.Add(new ProviderPlaylistInfo
                    {
                        Id = playlist.Id ?? string.Empty,
                        Name = playlist.Name ?? string.Empty,
                        ImageUrl = string.IsNullOrWhiteSpace(rawImageUrl) ? null : new Uri(rawImageUrl),
                        Description = playlist.Description ?? string.Empty,
                        OwnerId = ownerId,
                        ProviderName = ProviderName,
                        State = CreatePlaylistState(playlist),
                    });
                }

                return playlists;
            }
            catch (APIException e)
            {
                _logger.LogError(e, "Failed to get user playlists for user {Id}", target.Id);
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                LogApiParseException(e, $"user with id {target.Id}", spotifyPlaylists);
            }

            return null;
        }

        protected override async Task<ProviderPlaylistInfo?> GetPlaylist(
            string playlistId,
            bool includeTracks,
            CancellationToken? cancellationToken = null)
        {
            if (_spotifyClient == null)
            {
                return null;
            }

            FullPlaylist? playlist = null;

            try
            {
                playlist = await _spotifyClient.Playlists.Get(playlistId, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                var rawImageUrl = playlist.Images?.FirstOrDefault((Image?)null)?.Url;

                var tracks = new List<ProviderTrackInfo>();
                if (includeTracks && playlist.Tracks != null)
                {
                    await foreach (var track in _spotifyClient.Paginate(playlist.Tracks).ConfigureAwait(false))
                    {
                        if (cancellationToken?.IsCancellationRequested ?? false)
                        {
                            return null;
                        }

                        var trackInfo = GetTrackInfo(track);
                        if (trackInfo != null)
                        {
                            tracks.Add(trackInfo);
                        }
                        else if ((Plugin.Instance?.Configuration.EnableVerboseLogging ?? false) && track != null)
                        {
                            _logger.LogWarning("Encountered invalid track in Spotify playlist ({PlaylistId}) added at: {AddedAt}", playlistId, track.AddedAt);
                        }
                    }
                }

                return new ProviderPlaylistInfo
                {
                    Id = playlist.Id ?? string.Empty,
                    Name = playlist.Name ?? string.Empty,
                    ImageUrl = string.IsNullOrWhiteSpace(rawImageUrl) ? null : new Uri(rawImageUrl),
                    Description = playlist.Description ?? string.Empty,
                    OwnerId = playlist.Owner != null ? playlist.Owner.Id : string.Empty,
                    Tracks = tracks,
                    ProviderName = ProviderName,
                    State = CreatePlaylistState(playlist),
                };
            }
            catch (APIException e)
            {
                _logger.LogError(e, "Failed to get playlist with id {Id}", playlistId);
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                LogApiParseException(e, $"playlist with id {playlistId}", playlist);
            }

            return null;
        }

        protected override Task<ProviderTrackInfo?> GetTrack(string trackId, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        protected override string CreatePlaylistState<T>(T data)
        {
            if (data is FullPlaylist playlist)
            {
                return playlist.SnapshotId ?? string.Empty;
            }

            return string.Empty;
        }

        private static ProviderTrackInfo? GetTrackInfo(PlaylistTrack<IPlayableItem> track)
        {
            if (track == null || track.Track == null)
            {
                return null;
            }

            switch (track.Track.Type)
            {
                case ItemType.Track:
                    {
                        if (track.Track is not FullTrack fullTrack)
                        {
                            break;
                        }

                        var hasIsrc = fullTrack.ExternalIds.TryGetValue("isrc", out var isrc);

                        var id = fullTrack.Id;
                        if (id == null)
                        {
                            id = fullTrack.Uri ?? string.Empty;
                            id = id.Replace("spotify:track:", string.Empty, StringComparison.InvariantCulture);
                            id = id.Replace("spotify:local:", string.Empty, StringComparison.InvariantCulture);
                        }

                        return new ProviderTrackInfo
                        {
                            Id = id,
                            Name = fullTrack.Name,
                            IsrcId = hasIsrc ? isrc : null,
                            TrackNumber = (uint)fullTrack.TrackNumber,
                            AlbumName = fullTrack.Album.Name,
                            AlbumArtistNames = fullTrack.Album.Artists.Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name)).Select(a => a.Name).ToList(),
                            ArtistNames = fullTrack.Artists.Select(a => a.Name).ToList(),
                        };
                    }

                case ItemType.Episode:
                default:
                    break;
            }

            return null;
        }

        private void LogApiParseException(Newtonsoft.Json.JsonException exception, string target, object? obj)
        {
            // for some unknown reason the Spotify API sometimes returns data in a different structure,
            // which currently results in a json parse error in SpotifyAPIWeb
            // see this issue: https://github.com/Viperinius/jellyfin-plugin-spotify-import/issues/18
            // also see this issue in the SpotifyAPIWeb repo: https://github.com/JohnnyCrazy/SpotifyAPI-NET/issues/926

            _logger.LogError(exception, "Encountered json error for {Target}", target);
            string? objString = null;
            try
            {
                if (obj != null)
                {
                    objString = Newtonsoft.Json.Linq.JObject.FromObject(obj).ToString();
                }
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
            }

            _logger.LogInformation("Received: \n{Object}", objString);
        }
    }
}
