using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    /// <summary>
    /// All configurable ways to check matching tracks.
    /// </summary>
    [Flags]
    public enum ItemMatchCriteria
    {
        /// <summary>
        /// Empty element.
        /// </summary>
        None = 0,

        /// <summary>
        /// Name of the track.
        /// </summary>
        TrackName = 1 << 0,

        /// <summary>
        /// Name of the album.
        /// </summary>
        AlbumName = 1 << 1,

        /// <summary>
        /// Names of the artists.
        /// </summary>
        Artists = 1 << 2,

        /// <summary>
        /// Names of the album artists.
        /// </summary>
        AlbumArtists = 1 << 3,
    }
}
