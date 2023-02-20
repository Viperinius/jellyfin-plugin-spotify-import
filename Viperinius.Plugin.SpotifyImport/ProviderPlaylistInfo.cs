using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport
{
    internal class ProviderPlaylistInfo
    {
        public ProviderPlaylistInfo()
        {
            Id = string.Empty;
            Name = string.Empty;
            Description = string.Empty;
            OwnerId = string.Empty;
            Tracks = new List<ProviderTrackInfo>();
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public Uri? ImageUrl { get; set; }

        public string Description { get; set; }

        public string OwnerId { get; set; }

        public List<ProviderTrackInfo> Tracks { get; set; }
    }
}
