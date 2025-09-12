#pragma warning disable CA1002 // Do not expose generic lists
#pragma warning disable CA2227 // Collection properties should be read only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport
{
    /// <summary>
    /// Track info supplied by provider.
    /// </summary>
    [Serializable]
    public class ProviderTrackInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderTrackInfo"/> class.
        /// </summary>
        public ProviderTrackInfo()
        {
            Id = string.Empty;
            Name = string.Empty;
            AlbumName = string.Empty;
            AlbumArtistNames = new List<string>();
            ArtistNames = new List<string>();
        }

        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the track name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the ISRC id.
        /// </summary>
        [JsonIgnore]
        public string? IsrcId { get; set; }

        /// <summary>
        /// Gets or sets the track number.
        /// </summary>
        [JsonIgnore]
        public uint TrackNumber { get; set; }

        /// <summary>
        /// Gets or sets the album name.
        /// </summary>
        public string AlbumName { get; set; }

        /// <summary>
        /// Gets or sets the album artists.
        /// </summary>
        public List<string> AlbumArtistNames { get; set; }

        /// <summary>
        /// Gets or sets the artists.
        /// </summary>
        public List<string> ArtistNames { get; set; }
    }
}
