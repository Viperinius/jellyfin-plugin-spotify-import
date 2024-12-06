using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MediaBrowser.Controller.Playlists;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using Viperinius.Plugin.SpotifyImport.Configuration;
using Viperinius.Plugin.SpotifyImport.Utils;

namespace Viperinius.Plugin.SpotifyImport.Spotify
{
    internal class SpotifyAltPlaylistProvider : GenericPlaylistProvider
    {
        private const string ProviderName = "SpotifyAlt";
        private static readonly Uri _providerUrl = new Uri("https://open.spotify.com");
        private readonly ILogger<SpotifyAltPlaylistProvider> _logger;
        private readonly HttpRequest _httpRequest;

        public SpotifyAltPlaylistProvider(
            ILogger<SpotifyAltPlaylistProvider> logger,
            ILogger<HttpRequest> httpLogger) : base(logger)
        {
            _logger = logger;
            _httpRequest = new HttpRequest(httpLogger);
        }

        public override string Name => ProviderName;

        public override Uri ApiUrl => new Uri("https://api-partner.spotify.com");

        public override object? AuthToken { get; set; }

        public override void SetUpProvider()
        {
            if (string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration.SpotifyCookie))
            {
                _logger.LogWarning("No Spotify session cookie given, Spotify-owned playlists won't be found when listing all user playlists!");
            }

            RefreshAuthToken();
        }

        protected override async Task<List<ProviderPlaylistInfo>?> GetUserPlaylistsInfo(TargetUserConfiguration target, CancellationToken? cancellationToken = null)
        {
            if (((SpotifyAltAuthToken?)AuthToken)?.IsAnonymous ?? true)
            {
                // user library can only be queried if using a session of a logged in user
                return null;
            }

            var pageLimit = 100;
            var offset = 0;
            var totalPlaylistCount = pageLimit;

            var playlists = new List<ProviderPlaylistInfo>();
            while (playlists.Count < totalPlaylistCount && offset < totalPlaylistCount)
            {
                if (cancellationToken?.IsCancellationRequested ?? false)
                {
                    return null;
                }

                var json = await RequestApi("/query", new Dictionary<string, string>
                {
                    { "operationName", "libraryV3" },
                    { "variables", $"{{\"filters\":[\"Playlists\",\"By Spotify\"],\"order\":null,\"textFilter\":\"\",\"features\":[\"LIKED_SONGS\",\"YOUR_EPISODES\"],\"offset\":{offset},\"limit\":{pageLimit}}}" },
                    { "extensions", "{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"50650f72ea32a99b5b46240bee22fea83024eec302478a9a75cfd05a0814ba99\"}}" },
                }).ConfigureAwait(false);

                if (json == null ||
                    !json.Value.TryGetProperty("me", out var jsonMe) ||
                    !jsonMe.TryGetProperty("libraryV3", out var jsonLibrary))
                {
                    return null;
                }

                totalPlaylistCount = GetApiItemsCount(jsonLibrary);

                foreach (var jsonPlaylist in IterateApiItems(jsonLibrary))
                {
                    if (!jsonPlaylist.TryGetProperty("item", out var jsonItem) ||
                        (!jsonItem.TryGetProperty("uri", out var jsonUri) &&
                        !jsonItem.TryGetProperty("_uri", out jsonUri)))
                    {
                        continue;
                    }

                    var playlistId = jsonUri.GetString()?.Replace("spotify:playlist:", string.Empty, StringComparison.InvariantCulture);
                    if (playlistId == null)
                    {
                        continue;
                    }

                    if (!jsonItem.TryGetProperty("data", out var jsonData))
                    {
                        continue;
                    }

                    var playlistInfo = ParsePlaylist(playlistId, jsonData, new List<ProviderTrackInfo>());

                    if (target.OnlyOwnPlaylists && playlistInfo.OwnerId != target.Id)
                    {
                        continue;
                    }

                    playlists.Add(playlistInfo);
                }

                offset += pageLimit;
            }

            return playlists;
        }

        protected override async Task<ProviderPlaylistInfo?> GetPlaylist(string playlistId, CancellationToken? cancellationToken = null)
        {
            var pageLimit = 100;
            var offset = 0;
            var totalTrackCount = pageLimit;

            var tracks = new List<ProviderTrackInfo>();
            JsonElement jsonPlaylist = default;
            while (tracks.Count < totalTrackCount && offset < totalTrackCount)
            {
                if (cancellationToken?.IsCancellationRequested ?? false)
                {
                    return null;
                }

                var json = await RequestApi("/query", new Dictionary<string, string>
                {
                    { "operationName", "fetchPlaylist" },
                    { "variables", $"{{\"uri\":\"spotify:playlist:{playlistId}\",\"offset\":{offset},\"limit\":{pageLimit}}}" },
                    { "extensions", "{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"19ff1327c29e99c208c86d7a9d8f1929cfdf3d3202a0ff4253c821f1901aa94d\"}}" },
                }).ConfigureAwait(false);

                if (json == null || !json.Value.TryGetProperty("playlistV2", out jsonPlaylist))
                {
                    return null;
                }

                if (!jsonPlaylist.TryGetProperty("content", out var jsonContent))
                {
                    return null;
                }

                totalTrackCount = GetApiItemsCount(jsonContent);

                foreach (var jsonTrack in IterateApiItems(jsonContent))
                {
                    var track = ParseTrack(jsonTrack);
                    if (track != null)
                    {
                        tracks.Add(track);
                    }
                }

                offset += pageLimit;
            }

            return ParsePlaylist(playlistId, jsonPlaylist, tracks);
        }

        protected override Task<ProviderTrackInfo?> GetTrack(string trackId, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        private void RefreshAuthToken()
        {
            if (AuthToken != null && ((SpotifyAltAuthToken)AuthToken).ExpirationUnixMs > DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds())
            {
                return;
            }

            // prefer user session over anonymous
            var cookies = new CookieContainer();
            if (!string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration.SpotifyCookie))
            {
                cookies.SetCookies(_providerUrl, $"sp_dc={Plugin.Instance.Configuration.SpotifyCookie}");
                if (cookies.GetAllCookies().First().Expired)
                {
                    cookies = new CookieContainer();
                }
            }

            // get a new bearer token for an anonymous session
            var response = _httpRequest.Get(_providerUrl, cookies: cookies.GetCookieHeader(_providerUrl)).Result;
            if (response != null)
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(WebUtility.HtmlDecode(HttpRequest.GetResponseContentString(response)));
                AuthToken = JsonSerializer.Deserialize<SpotifyAltAuthToken>(htmlDoc.DocumentNode.SelectSingleNode("//script[@id='session']").InnerText);
            }
        }

        private async Task<JsonElement?> RequestApi(string endpoint, Dictionary<string, string> queryParams)
        {
            if (AuthToken == null)
            {
                return null;
            }

            RefreshAuthToken();

            var headers = HttpRequest.CreateHeaders();
            headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ((SpotifyAltAuthToken)AuthToken).Token);
            headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var uriBuilder = new UriBuilder(ApiUrl)
            {
                Path = $"/pathfinder/v1{endpoint}",
                Query = HttpRequest.BuildUrlQuery(queryParams),
            };

            var res = await _httpRequest.Get(uriBuilder.Uri, headers: headers).ConfigureAwait(false);
            if (res == null)
            {
                return null;
            }

            try
            {
                var json = JsonDocument.Parse(HttpRequest.GetResponseContentString(res));
                return json.RootElement.GetProperty("data");
            }
            catch (JsonException)
            {
            }

            return null;
        }

        private int GetApiItemsCount(JsonElement parent)
        {
            if (parent.TryGetProperty("totalCount", out var jsonTotalCount))
            {
                return jsonTotalCount.GetInt32();
            }

            return 0;
        }

        private IEnumerable<JsonElement> IterateApiItems(JsonElement parent)
        {
            if (parent.TryGetProperty("items", out var jsonItems) &&
                jsonItems.ValueKind == JsonValueKind.Array &&
                jsonItems.GetArrayLength() > 0)
            {
                foreach (var item in jsonItems.EnumerateArray())
                {
                    yield return item;
                }
            }
        }

        private ProviderPlaylistInfo ParsePlaylist(string playlistId, JsonElement jsonPlaylist, List<ProviderTrackInfo> tracks)
        {
            var name = string.Empty;
            if (jsonPlaylist.TryGetProperty("name", out var jsonName))
            {
                name = jsonName.GetString() ?? string.Empty;
            }

            var description = string.Empty;
            if (jsonPlaylist.TryGetProperty("description", out var jsonDescription))
            {
                description = jsonDescription.GetString() ?? string.Empty;
            }

            var rawImageUrl = string.Empty;
            if (jsonPlaylist.TryGetProperty("images", out var jsonImages) &&
                jsonImages.TryGetProperty("items", out var jsonImageItems) &&
                jsonImageItems.ValueKind == JsonValueKind.Array && jsonImageItems.GetArrayLength() > 0 &&
                jsonImageItems[0].TryGetProperty("sources", out var jsonImageSources) &&
                jsonImageSources.ValueKind == JsonValueKind.Array && jsonImageSources.GetArrayLength() > 0 &&
                jsonImageSources[0].TryGetProperty("url", out var jsonImageUrl))
            {
                rawImageUrl = jsonImageUrl.GetString() ?? string.Empty;
            }

            var ownerId = string.Empty;
            if (jsonPlaylist.TryGetProperty("ownerV2", out var jsonOwner) &&
                jsonOwner.TryGetProperty("data", out var jsonOwnerData) &&
                jsonOwnerData.TryGetProperty("username", out var jsonOwnerName))
            {
                ownerId = jsonOwnerName.GetString() ?? string.Empty;
            }

            return new ProviderPlaylistInfo
            {
                Id = playlistId,
                Name = name,
                ImageUrl = string.IsNullOrWhiteSpace(rawImageUrl) ? null : new Uri(rawImageUrl),
                Description = description,
                OwnerId = ownerId,
                Tracks = tracks,
                ProviderName = ProviderName.Replace("Alt", string.Empty, StringComparison.InvariantCulture),
            };
        }

        private ProviderTrackInfo? ParseTrack(JsonElement jsonTrack)
        {
            if (jsonTrack.TryGetProperty("itemV2", out var jsonItem) &&
                jsonItem.TryGetProperty("data", out var jsonData))
            {
                var name = string.Empty;
                if (jsonData.TryGetProperty("name", out var jsonName))
                {
                    name = jsonName.GetString() ?? string.Empty;
                }

                var trackNum = 0u;
                if (jsonData.TryGetProperty("trackNumber", out var jsonTrackNum))
                {
                    trackNum = jsonTrackNum.GetUInt32();
                }

                var albumName = string.Empty;
                var albumArtists = new List<string>();
                if (jsonData.TryGetProperty("albumOfTrack", out var jsonAlbum) &&
                    jsonAlbum.TryGetProperty("name", out var jsonAlbumName))
                {
                    albumName = jsonAlbumName.GetString() ?? string.Empty;
                }

                if (jsonData.TryGetProperty("albumOfTrack", out jsonAlbum) &&
                    jsonAlbum.TryGetProperty("artists", out var jsonAlbumArtists) &&
                    jsonAlbumArtists.TryGetProperty("items", out var jsonAlbumArtistItems) &&
                    jsonAlbumArtistItems.ValueKind == JsonValueKind.Array && jsonAlbumArtistItems.GetArrayLength() > 0)
                {
                    foreach (var jsonAlbumArtist in jsonAlbumArtistItems.EnumerateArray())
                    {
                        if (jsonAlbumArtist.TryGetProperty("profile", out var jsonProfile) &&
                            jsonProfile.TryGetProperty("name", out jsonName))
                        {
                            var albumArtist = jsonName.GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(albumArtist))
                            {
                                albumArtists.Add(albumArtist);
                            }
                        }
                    }
                }

                var artists = new List<string>();
                if (jsonData.TryGetProperty("artists", out var jsonArtists) &&
                    jsonArtists.TryGetProperty("items", out var jsonArtistItems) &&
                    jsonArtistItems.ValueKind == JsonValueKind.Array && jsonArtistItems.GetArrayLength() > 0)
                {
                    foreach (var jsonArtist in jsonArtistItems.EnumerateArray())
                    {
                        if (jsonArtist.TryGetProperty("profile", out var jsonProfile) &&
                            jsonProfile.TryGetProperty("name", out jsonName))
                        {
                            var artist = jsonName.GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(artist))
                            {
                                artists.Add(artist);
                            }
                        }
                    }
                }

                return new ProviderTrackInfo
                {
                    Name = name,
                    AlbumName = albumName,
                    AlbumArtistNames = albumArtists,
                    ArtistNames = artists,
                    TrackNumber = trackNum,
                };
            }

            return null;
        }
    }
}