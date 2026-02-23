using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OtpNet;
using Viperinius.Plugin.SpotifyImport.Configuration;
using Viperinius.Plugin.SpotifyImport.Utils;

namespace Viperinius.Plugin.SpotifyImport.Spotify
{
    internal class SpotifyAltPlaylistProvider : GenericPlaylistProvider
    {
        private const string ProviderName = "SpotifyAlt";
        private const int ApiCallDelayMs = 250;
        private static readonly Uri _providerUrl = new Uri(Plugin.SpotifyBaseUrl);
        private readonly ILogger<SpotifyAltPlaylistProvider> _logger;
        private readonly HttpRequest _httpRequest;

        public SpotifyAltPlaylistProvider(
            DbRepository dbRepository,
            ILogger<SpotifyAltPlaylistProvider> logger,
            ILogger<HttpRequest> httpLogger) : base(dbRepository, logger)
        {
            _logger = logger;
            _httpRequest = new HttpRequest(httpLogger);
        }

        public override string Name => ProviderName;

        public override Uri ApiUrl => new Uri("https://api-partner.spotify.com");

        public Uri ApiUrl2 => new Uri("https://spclient.wg.spotify.com");

        public override object? AuthToken { get; set; }

        public override bool IsSetUp { get; protected set; }

        public override void SetUpProvider()
        {
            if (string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration.SpotifyCookie))
            {
                _logger.LogWarning("No Spotify session cookie given, Spotify-owned playlists won't be found when listing all user playlists!");
            }

            RefreshAuthToken();
            IsSetUp = true;
        }

        protected override async Task<List<ProviderPlaylistInfo>?> GetUserPlaylistsInfo(TargetUserConfiguration target, CancellationToken? cancellationToken = null)
        {
            var playlists = new List<ProviderPlaylistInfo>();
            var totalPlaylistCount = 1;

            if (!string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration.SpotifyUserId) && target.Id == Plugin.Instance.Configuration.SpotifyUserId && !(((SpotifyAltAuthToken?)AuthToken)?.IsAnonymous ?? true))
            {
                // the targeted user is the same as the authenticated one, so we can query the library directly

                var pageLimit = 100;
                var offset = 0;
                totalPlaylistCount = pageLimit;

                while (playlists.Count < totalPlaylistCount && offset < totalPlaylistCount)
                {
                    if (cancellationToken?.IsCancellationRequested ?? false)
                    {
                        return null;
                    }

                    var body = $@"{{
                        ""variables"": {{
                            ""limit"": {pageLimit},
                            ""offset"": {offset},
                            ""filters"": [""Playlists""],
                            ""order"": null,
                            ""textFilter"": """",
                            ""features"": [""LIKED_SONGS"", ""YOUR_EPISODES_V2"", ""PRERELEASES"", ""EVENTS""],
                            ""flatten"": true,
                            ""expandedFolders"": [],
                            ""folderUri"": null,
                            ""includeFoldersWhenFlattening"": false
                        }},
                        ""operationName"": ""libraryV3"",
                        ""extensions"": {{
                            ""persistedQuery"": {{
                                ""version"": 1,
                                ""sha256Hash"": ""9f4da031f81274d572cfedaf6fc57a737c84b43d572952200b2c36aaa8fec1c6""
                            }}
                        }}
                    }}";

                    var json = await RequestApi("/query", new Dictionary<string, string>(), body).ConfigureAwait(false);

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

                        if (string.IsNullOrEmpty(playlistInfo.Name) || (target.OnlyOwnPlaylists && playlistInfo.OwnerId != target.Id))
                        {
                            continue;
                        }

                        playlists.Add(playlistInfo);
                    }

                    offset += pageLimit;
                    await Task.Delay(ApiCallDelayMs).ConfigureAwait(false);
                }

                return playlists;
            }
            else
            {
                // we can only get the public playlists

                // get the actual playlist count first
                var queryParams = new Dictionary<string, string>
                {
                    { "playlist_limit", $"{totalPlaylistCount}" },
                    { "artist_limit", "0" },
                    { "episode_limit", "0" },
                    { "market", "from_token" },
                };
                var endpoint = $"/user-profile-view/v3/profile/{target.Id}";
                var json = await RequestApi2(endpoint, queryParams).ConfigureAwait(false);

                if (json == null ||
                    !json.Value.TryGetProperty("total_public_playlists_count", out var jsonCount))
                {
                    return null;
                }

                totalPlaylistCount = jsonCount.GetInt32();
                if (totalPlaylistCount > 1)
                {
                    // re-request all playlists
                    queryParams["playlist_limit"] = $"{totalPlaylistCount}";

                    if (cancellationToken?.IsCancellationRequested ?? false)
                    {
                        return null;
                    }

                    json = await RequestApi2(endpoint, queryParams).ConfigureAwait(false);
                    if (json == null)
                    {
                        return null;
                    }
                }

                if (!json.Value.TryGetProperty("public_playlists", out var jsonPlaylists))
                {
                    return null;
                }

                foreach (var jsonPlaylist in jsonPlaylists.EnumerateArray())
                {
                    if (!jsonPlaylist.TryGetProperty("uri", out var jsonUri) ||
                        !jsonPlaylist.TryGetProperty("owner_uri", out var jsonOwner) ||
                        !jsonPlaylist.TryGetProperty("name", out var jsonName))
                    {
                        continue;
                    }

                    var playlistId = jsonUri.GetString()?.Replace("spotify:playlist:", string.Empty, StringComparison.InvariantCulture);
                    if (playlistId == null)
                    {
                        continue;
                    }

                    var ownerId = jsonOwner.GetString()?.Replace("spotify:user:", string.Empty, StringComparison.InvariantCulture);
                    if (ownerId == null)
                    {
                        continue;
                    }

                    var name = jsonName.GetString();
                    if (name == null)
                    {
                        continue;
                    }

                    var playlistInfo = new ProviderPlaylistInfo
                    {
                        Id = playlistId,
                        Name = name,
                        OwnerId = ownerId,
                        ProviderName = ProviderName,
                        State = CreatePlaylistState(jsonPlaylist),
                    };

                    if (target.OnlyOwnPlaylists && playlistInfo.OwnerId != target.Id)
                    {
                        continue;
                    }

                    playlists.Add(playlistInfo);
                }

                return playlists;
            }
        }

        protected override async Task<ProviderPlaylistInfo?> GetPlaylist(string playlistId, bool includeTracks, CancellationToken? cancellationToken = null)
        {
            if (playlistId == SpotifyPlaylistProvider.SavedTracksFakePlaylistId)
            {
                return await GetSavedTracks(cancellationToken).ConfigureAwait(false);
            }

            var pageLimit = 50;
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

                var body = $@"{{
                    ""variables"": {{
                        ""limit"": {pageLimit},
                        ""offset"": {offset},
                        ""uri"": ""spotify:playlist:{playlistId}""
                    }},
                    ""operationName"": ""fetchPlaylist"",
                    ""extensions"": {{
                        ""persistedQuery"": {{
                            ""version"": 1,
                            ""sha256Hash"": ""19ff1327c29e99c208c86d7a9d8f1929cfdf3d3202a0ff4253c821f1901aa94d""
                        }}
                    }}
                }}";

                var json = await RequestApi("/query", new Dictionary<string, string>(), body).ConfigureAwait(false);

                if (json == null || !json.Value.TryGetProperty("playlistV2", out jsonPlaylist))
                {
                    return null;
                }

                if (!jsonPlaylist.TryGetProperty("content", out var jsonContent))
                {
                    return null;
                }

                if (!includeTracks)
                {
                    break;
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
                await Task.Delay(ApiCallDelayMs).ConfigureAwait(false);
            }

            return ParsePlaylist(playlistId, jsonPlaylist, tracks);
        }

        protected override Task<ProviderTrackInfo?> GetTrack(string trackId, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        protected override string CreatePlaylistState<T>(T data)
        {
            if (data is JsonElement playlist && playlist.TryGetProperty("revisionId", out var jsonRevision))
            {
                return jsonRevision.GetString() ?? string.Empty;
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

        private async Task<ProviderPlaylistInfo?> GetSavedTracks(CancellationToken? cancellationToken = null)
        {
            if (((SpotifyAltAuthToken?)AuthToken)?.IsAnonymous ?? true)
            {
                _logger.LogError("Cant fetch saved tracks from library: No user session token found.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration.SpotifySavedTracksDisplayName))
            {
                _logger.LogError("Invalid display name for saved tracks playlist configured. Please change it in the plugin settings.");
                return null;
            }

            var pageLimit = 50;
            var offset = 0;
            var totalTrackCount = pageLimit;

            var tracksByDate = new List<(string, ProviderTrackInfo)>();
            while (tracksByDate.Count < totalTrackCount && offset < totalTrackCount)
            {
                if (cancellationToken?.IsCancellationRequested ?? false)
                {
                    return null;
                }

                var body = $@"{{
                    ""variables"": {{
                        ""limit"": {pageLimit},
                        ""offset"": {offset}
                    }},
                    ""operationName"": ""fetchLibraryTracks"",
                    ""extensions"": {{
                        ""persistedQuery"": {{
                            ""version"": 1,
                            ""sha256Hash"": ""087278b20b743578a6262c2b0b4bcd20d879c503cc359a2285baf083ef944240""
                        }}
                    }}
                }}";

                var json = await RequestApi("/query", new Dictionary<string, string>(), body).ConfigureAwait(false);

                if (json == null ||
                    !json.Value.TryGetProperty("me", out var jsonMe) ||
                    !jsonMe.TryGetProperty("library", out var jsonLibrary))
                {
                    return null;
                }

                if (!jsonLibrary.TryGetProperty("tracks", out var jsonTracks))
                {
                    return null;
                }

                totalTrackCount = GetApiItemsCount(jsonTracks);
                foreach (var jsonTrack in IterateApiItems(jsonTracks))
                {
                    var addedAt = string.Empty;
                    if (jsonTrack.TryGetProperty("addedAt", out var jsonAddedAt) &&
                        jsonAddedAt.TryGetProperty("isoString", out var jsonAddedAtString))
                    {
                        addedAt = jsonAddedAtString.GetString() ?? string.Empty;
                    }

                    var track = ParseTrack(jsonTrack);
                    if (track != null)
                    {
                        tracksByDate.Add((addedAt, track));
                    }
                    else if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogWarning("Encountered invalid track in Spotify Saved Tracks added at: {AddedAt}", addedAt);
                    }
                }

                offset += pageLimit;
                await Task.Delay(ApiCallDelayMs).ConfigureAwait(false);
            }

            var tracks = tracksByDate.OrderBy(t => t.Item1).Select(t => t.Item2).ToList();

            return new ProviderPlaylistInfo
            {
                Id = SpotifyPlaylistProvider.SavedTracksFakePlaylistId,
                Name = Plugin.Instance!.Configuration.SpotifySavedTracksDisplayName,
                ImageUrl = string.IsNullOrWhiteSpace(Plugin.Instance!.Configuration.SpotifySavedTracksImageUrl) ? null : new Uri(Plugin.Instance!.Configuration.SpotifySavedTracksImageUrl),
                Description = string.Empty,
                OwnerId = string.Empty,
                Tracks = tracks,
                ProviderName = ProviderName,
                State = CreatePlaylistState(tracks),
            };
        }

        // computation heavily inspired by generate_totp in https://github.com/misiektoja/spotify_monitor/blob/b1abf1b451e511b333664c68ca17b48b199b7353/spotify_monitor.py#L1118
        private (Totp, long)? GenerateTotp(IEnumerable<byte> cipherBytes)
        {
            // compute secret from cipher bytes first
            var transformedBytes = cipherBytes.Select((b, ii) => b ^ ((ii % 33) + 9));
            var utf8Bytes = Encoding.UTF8.GetBytes(string.Join(string.Empty, transformedBytes));

            var response = _httpRequest.Head(_providerUrl).Result;
            if (response != null)
            {
                var serverTime = response.Headers.Date?.ToUnixTimeSeconds();
                if (serverTime == null)
                {
                    return null;
                }

                var totp = new Totp(utf8Bytes, step: 30, totpSize: 6);
                return (totp, (long)serverTime);
            }

            return null;
        }

        private DumpedSecretsResultJson? TryRetrieveScrapedSecrets()
        {
            var dumpedSecretsUrlStr = Plugin.Instance?.Configuration.SpotifyTotpSecretsUrl;
            if (!Uri.TryCreate(dumpedSecretsUrlStr, UriKind.Absolute, out var dumpedSecretsUrl))
            {
                _logger.LogError("Failed to retrieve pre-scraped secrets: Invalid {Name} ({Value})", nameof(Plugin.Instance.Configuration.SpotifyTotpSecretsUrl), dumpedSecretsUrlStr);
                return null;
            }

            var response = _httpRequest.Get(dumpedSecretsUrl).Result;
            if (response != null)
            {
                try
                {
                    var dump = JsonSerializer.Deserialize<DumpedSecretsResultJson[]>(response.Content.ReadAsStringAsync().Result);
                    // use newest version
                    return dump?.Aggregate((x, y) => x.Version > y.Version ? x : y);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to retrieve pre-scraped secrets");
                }
            }

            return null;
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
                    _logger.LogWarning("Spotify cookie is expired since {ExpiredAt}!", cookies.GetAllCookies().First().Expires);
                    cookies = new CookieContainer();
                }
            }

            var dumpedSecret = TryRetrieveScrapedSecrets();
            if (dumpedSecret == null)
            {
                _logger.LogError("Failed to find TOTP secrets");
                return;
            }

            var totpVersion = dumpedSecret.Value.Version;
            var totpCipher = dumpedSecret.Value.Secret.ToArray();

            var totpResult = GenerateTotp(totpCipher);
            if (totpResult == null)
            {
                _logger.LogError("Failed to generate TOTP");
                return;
            }

            var (totp, serverTime) = totpResult.Value;
            var otp = totp.ComputeTotp(DateTime.UnixEpoch.AddSeconds(serverTime));
            var clientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // get a new bearer token for a session
            var uriBuilder = new UriBuilder(_providerUrl)
            {
                Path = "/api/token",
                Query = $"reason=init&productType=web-player&totp={otp}&totpServer={otp}&totpVer={totpVersion}&sTime={serverTime}&cTime={clientTime}"
            };
            var response = _httpRequest.Get(uriBuilder.Uri, cookies: cookies.GetCookieHeader(_providerUrl)).Result;
            if (response != null)
            {
                AuthToken = JsonSerializer.Deserialize<SpotifyAltAuthToken>(response.Content.ReadAsStringAsync().Result);
            }
            else
            {
                _logger.LogError("Failed to refresh auth token");
            }
        }

        private async Task<JsonElement?> RequestApi(string endpoint, Dictionary<string, string> queryParams, string jsonContent)
        {
            var json = await RequestApiInternal(ApiUrl, $"/pathfinder/v2{endpoint}", queryParams, true, () =>
            {
                return new System.Net.Http.StringContent(jsonContent, Encoding.UTF8, "application/json");
            }).ConfigureAwait(false);
            return json?.GetProperty("data");
        }

        private async Task<JsonElement?> RequestApi2(string endpoint, Dictionary<string, string> queryParams)
        {
            return await RequestApiInternal(ApiUrl2, endpoint, queryParams, false).ConfigureAwait(false);
        }

        private async Task<JsonElement?> RequestApiInternal(Uri baseUri, string path, Dictionary<string, string> queryParams, bool usePost, Func<System.Net.Http.HttpContent>? getContent = null)
        {
            if (AuthToken == null)
            {
                return null;
            }

            RefreshAuthToken();

            var headers = HttpRequest.CreateHeaders();
            headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ((SpotifyAltAuthToken)AuthToken).Token);
            headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var uriBuilder = new UriBuilder(baseUri)
            {
                Path = path,
                Query = HttpRequest.BuildUrlQuery(queryParams),
            };

            System.Net.Http.HttpResponseMessage? res;
            if (usePost)
            {
                res = await _httpRequest.Post(uriBuilder.Uri, getContent: getContent, headers: headers).ConfigureAwait(false);
            }
            else
            {
                res = await _httpRequest.Get(uriBuilder.Uri, headers: headers).ConfigureAwait(false);
            }

            if (res == null)
            {
                return null;
            }

            try
            {
                var json = JsonDocument.Parse(HttpRequest.GetResponseContentString(res));
                return json.RootElement;
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
                ProviderName = ProviderName,
                State = CreatePlaylistState(jsonPlaylist),
            };
        }

        private ProviderTrackInfo? ParseTrack(JsonElement jsonTrack)
        {
            if ((jsonTrack.TryGetProperty("itemV2", out var jsonItem) || jsonTrack.TryGetProperty("track", out jsonItem)) &&
                jsonItem.TryGetProperty("data", out var jsonData))
            {
                var id = string.Empty;
                if (jsonData.TryGetProperty("uri", out var jsonUri) || jsonItem.TryGetProperty("_uri", out jsonUri))
                {
                    id = jsonUri.GetString() ?? string.Empty;
                    id = id.Replace("spotify:track:", string.Empty, StringComparison.InvariantCulture);
                    id = id.Replace("spotify:local:", string.Empty, StringComparison.InvariantCulture);
                }

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
                    Id = id,
                    Name = name,
                    AlbumName = albumName,
                    AlbumArtistNames = albumArtists,
                    ArtistNames = artists,
                    TrackNumber = trackNum,
                };
            }

            return null;
        }

        private struct DumpedSecretsResultJson
        {
            [JsonPropertyName("version")]
            public int Version { get; set; }

            [JsonPropertyName("secret")]
            public List<byte> Secret { get; set; }
        }
    }
}
