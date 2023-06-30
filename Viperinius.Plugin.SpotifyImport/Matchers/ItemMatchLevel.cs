using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    /// <summary>
    /// All configurable matchers to check matching tracks.
    /// </summary>
    public enum ItemMatchLevel
    {
        /// <summary>
        /// Simple check for equality.
        /// </summary>
        Default,

        /// <summary>
        /// Case insensitive equality check.
        /// </summary>
        IgnoreCase,

        /// <summary>
        /// Case insensitive equality check minus any punctuation.
        /// </summary>
        IgnorePunctuationAndCase,
    }
}
