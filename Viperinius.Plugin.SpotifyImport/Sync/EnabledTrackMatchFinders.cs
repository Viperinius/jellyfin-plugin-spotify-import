using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Sync
{
    /// <summary>
    /// All configurable track match finders.
    /// </summary>
    [Flags]
    public enum EnabledTrackMatchFinders
    {
        /// <summary>
        /// Empty element.
        /// </summary>
        None = 0,

        /// <summary>
        /// Based on MusicBrainz ids.
        /// </summary>
        MusicBrainz = 1 << 0,
    }
}
