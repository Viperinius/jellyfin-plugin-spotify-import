using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Spotify
{
    [Serializable]
    internal class SpotifyAltAuthToken
    {
        public SpotifyAltAuthToken()
        {
            Token = string.Empty;
            ExpirationUnixMs = 0;
            IsAnonymous = true;
            ClientId = string.Empty;
        }

        [JsonPropertyName("accessToken")]
        public string Token { get; set; }

        [JsonPropertyName("accessTokenExpirationTimestampMs")]
        public long ExpirationUnixMs { get; set; }

        [JsonPropertyName("isAnonymous")]
        public bool IsAnonymous { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }
    }
}
