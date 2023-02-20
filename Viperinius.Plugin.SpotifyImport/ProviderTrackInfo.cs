using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport
{
    internal class ProviderTrackInfo
    {
        public ProviderTrackInfo()
        {
            Name = string.Empty;
            AlbumName = string.Empty;
            AlbumArtistName = string.Empty;
            ArtistName = string.Empty;
        }

        public string Name { get; set; }

        public string? IsrcId { get; set; }

        public uint TrackNumber { get; set; }

        public string AlbumName { get; set; }

        public string AlbumArtistName { get; set; }

        public string ArtistName { get; set; }
    }
}
