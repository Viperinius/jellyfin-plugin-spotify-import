using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;

namespace Viperinius.Plugin.SpotifyImport.Spotify
{
    internal class SpotifyPlaylistProvider : GenericPlaylistProvider
    {
        private readonly ILogger<SpotifyPlaylistProvider> _logger;
        private readonly ILogger<SpotifyLogger> _apiLogger;
        private SpotifyClient? _spotifyClient;
        private static SpotifyClientConfig _defaultSpotifyConfig = SpotifyClientConfig.CreateDefault();

        public SpotifyPlaylistProvider(
            ILogger<SpotifyPlaylistProvider> logger,
            ILogger<SpotifyLogger> apiLogger) : base(logger)
        {
            _logger = logger;
            _apiLogger = apiLogger;
        }

        public override string Name => "Spotify";

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

        protected override async Task<ProviderPlaylistInfo?> GetPlaylist(string playlistId, CancellationToken? cancellationToken = null)
        {
            if (_spotifyClient == null)
            {
                return null;
            }

            try
            {
                var playlist = await _spotifyClient.Playlists.Get(playlistId, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                var rawImageUrl = playlist.Images?.FirstOrDefault((Image?)null)?.Url;

                var tracks = new List<ProviderTrackInfo>();
                if (playlist.Tracks != null)
                {
                    await foreach (var track in _spotifyClient.Paginate(playlist.Tracks))
                    {
                        var trackInfo = GetTrackInfo(track);
                        if (trackInfo != null)
                        {
                            tracks.Add(trackInfo);
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
                    Tracks = tracks
                };
            }
            catch (APIException e)
            {
                _logger.LogError(e, "Failed to get playlist with id {Id}", playlistId);
            }

            return null;
        }

        protected override Task<ProviderTrackInfo?> GetTrack(string trackId, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        private static ProviderTrackInfo? GetTrackInfo(PlaylistTrack<IPlayableItem> track)
        {
            switch (track.Track.Type)
            {
                case ItemType.Track:
                    {
                        if (track.Track is not FullTrack fullTrack)
                        {
                            break;
                        }

                        var hasIsrc = fullTrack.ExternalIds.TryGetValue("isrc", out var isrc);

                        return new ProviderTrackInfo
                        {
                            Name = fullTrack.Name,
                            IsrcId = hasIsrc ? isrc : null,
                            TrackNumber = (uint)fullTrack.TrackNumber,
                            AlbumName = fullTrack.Album.Name,
                            AlbumArtistName = fullTrack.Album.Artists.First().Name,
                            ArtistName = fullTrack.Artists.First().Name,
                        };
                    }

                case ItemType.Episode:
                default:
                    break;
            }

            return null;
        }
    }
}
