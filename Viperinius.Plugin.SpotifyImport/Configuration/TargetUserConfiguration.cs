using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Configuration
{
    /// <summary>
    /// Holds the information about a configured user.
    /// </summary>
    public class TargetUserConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TargetUserConfiguration"/> class.
        /// </summary>
        public TargetUserConfiguration()
        {
            Id = string.Empty;
            UserName = string.Empty;
        }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the targeted Jellyfin user name.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether only original playlists should be collected.
        /// </summary>
        public bool OnlyOwnPlaylists { get; set; }
    }
}
