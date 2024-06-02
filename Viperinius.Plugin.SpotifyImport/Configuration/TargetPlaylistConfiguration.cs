using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Configuration
{
    /// <summary>
    /// Holds the information about a configured playlist.
    /// </summary>
    public class TargetPlaylistConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TargetPlaylistConfiguration"/> class.
        /// </summary>
        public TargetPlaylistConfiguration()
        {
            Id = string.Empty;
            Name = string.Empty;
            UserName = string.Empty;
        }

        /// <summary>
        /// Gets or sets the playlist ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the targeted Jellyfin playlist name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the targeted Jellyfin user name.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to delete and recreate the playlist from scratch each run.
        /// </summary>
        public bool RecreateFromScratch { get; set; }
    }
}
