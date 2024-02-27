using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport
{
    [Serializable]
    internal class ProviderTrackInfo
    {
        public ProviderTrackInfo()
        {
            Name = string.Empty;
            AlbumName = string.Empty;
            AlbumArtistNames = new List<string>();
            ArtistNames = new List<string>();
        }

        public string Name { get; set; }

        [JsonIgnore]
        public string? IsrcId { get; set; }

        [JsonIgnore]
        public uint TrackNumber { get; set; }

        public string AlbumName { get; set; }

        public List<string> AlbumArtistNames { get; set; }

        public List<string> ArtistNames { get; set; }
    }
}
