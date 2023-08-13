using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Configuration
{
    /// <summary>
    /// Type the configuration describes.
    /// </summary>
    public enum TargetConfigurationType
    {
        /// <summary>
        /// Configuration contains a playlist id.
        /// </summary>
        Playlist,

        /// <summary>
        /// Configuration contains a user id.
        /// </summary>
        User,
    }

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
        /// Gets or sets the type of the configured id.
        /// </summary>
        public TargetConfigurationType Type { get; set; }
    }
}
