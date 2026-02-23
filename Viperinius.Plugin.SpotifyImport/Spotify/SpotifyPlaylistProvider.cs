using System;
using System.Collections.Generic;
using System.Linq;
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

        public override bool IsSetUp { get; protected set; }

        public static string SavedTracksFakePlaylistId => "MyLikedSongs";

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
            IsSetUp = true;
        }

        protected override Task<List<ProviderPlaylistInfo>?> GetUserPlaylistsInfo(
            TargetUserConfiguration target,
            CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        protected override async Task<ProviderPlaylistInfo?> GetPlaylist(
            string playlistId,
            bool includeTracks,
            CancellationToken? cancellationToken = null)
        {
            if (!IsSetUp || _spotifyClient == null)
            {
                return null;
            }

            if (playlistId == SavedTracksFakePlaylistId)
            {
                return await GetSavedTracks(cancellationToken).ConfigureAwait(false);
            }

            FullPlaylist? playlist = null;

            try
            {
                playlist = await _spotifyClient.Playlists.Get(playlistId, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                var rawImageUrl = playlist.Images?.FirstOrDefault((Image?)null)?.Url;

                var tracks = new List<ProviderTrackInfo>();
#pragma warning disable CS0618 // Type or member is obsolete
                if (includeTracks && (playlist.Tracks != null || playlist.Items != null))
                {
                    var apiTracks = playlist.Tracks != null ? playlist.Tracks : playlist.Items;
#pragma warning restore CS0618 // Type or member is obsolete
                    await foreach (var track in _spotifyClient.Paginate(apiTracks!).ConfigureAwait(false))
                    {
                        if (cancellationToken?.IsCancellationRequested ?? false)
                        {
                            return null;
                        }

                        var trackInfo = track != null ? GetTrackInfo(track.Track) : null;
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

            if (data is List<ProviderTrackInfo> tracks)
            {
                // see https://stackoverflow.com/a/8094931
                unchecked
                {
                    var hash = 19;
                    foreach (var track in tracks)
                    {
                        hash = (hash * 31) + track.GetHashCode();
                    }

                    return hash.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return string.Empty;
        }

        private static ProviderTrackInfo? GetTrackInfo(IPlayableItem track)
        {
            if (track == null)
            {
                return null;
            }

            switch (track.Type)
            {
                case ItemType.Track:
                    {
                        if (track is not FullTrack fullTrack)
                        {
                            break;
                        }

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
                            TrackNumber = (uint)fullTrack.TrackNumber,
                            AlbumName = fullTrack.Album.Name,
                            AlbumArtistNames = fullTrack.Album.Artists.Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name)).Select(a => a.Name).ToList(),
                            ArtistNames = fullTrack.Artists.Select(a => a.Name).ToList(),
                        };
                    }

                case ItemType.Chapter:
                case ItemType.Episode:
                default:
                    break;
            }

            return null;
        }

        private async Task<ProviderPlaylistInfo?> GetSavedTracks(CancellationToken? cancellationToken = null)
        {
            if (!IsSetUp || _spotifyClient == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration.SpotifySavedTracksDisplayName))
            {
                _logger.LogError("Invalid display name for saved tracks playlist configured. Please change it in the plugin settings.");
                return null;
            }

            Paging<SavedTrack>? savedTracks = null;

            try
            {
                savedTracks = await _spotifyClient.Library.GetTracks(cancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                var tracks = new List<ProviderTrackInfo>();
                await foreach (var track in _spotifyClient.Paginate(savedTracks).ConfigureAwait(false))
                {
                    if (cancellationToken?.IsCancellationRequested ?? false)
                    {
                        return null;
                    }

                    var trackInfo = track != null ? GetTrackInfo(track.Track) : null;
                    if (trackInfo != null)
                    {
                        tracks.Add(trackInfo);
                    }
                    else if ((Plugin.Instance?.Configuration.EnableVerboseLogging ?? false) && track != null)
                    {
                        _logger.LogWarning("Encountered invalid track in Spotify Saved Tracks added at: {AddedAt}", track.AddedAt);
                    }
                }

                return new ProviderPlaylistInfo
                {
                    Id = SavedTracksFakePlaylistId,
                    Name = Plugin.Instance!.Configuration.SpotifySavedTracksDisplayName,
                    ImageUrl = string.IsNullOrWhiteSpace(Plugin.Instance!.Configuration.SpotifySavedTracksImageUrl) ? null : new Uri(Plugin.Instance!.Configuration.SpotifySavedTracksImageUrl),
                    Description = string.Empty,
                    OwnerId = string.Empty,
                    Tracks = tracks,
                    ProviderName = ProviderName,
                    State = CreatePlaylistState(tracks),
                };
            }
            catch (APIException e)
            {
                _logger.LogError(e, "Failed to get saved tracks of user");
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                LogApiParseException(e, $"saved tracks", savedTracks);
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
