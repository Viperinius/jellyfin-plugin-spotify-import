using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    /// <summary>
    /// Custom item matcher.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public interface IItemMatcher<T>
    {
        /// <summary>
        /// Gets a value indicating whether the matching is strict.
        /// </summary>
        public bool IsStrict { get; }

        /// <summary>
        /// Check if a given item validates.
        /// </summary>
        /// <param name="target">The reference item.</param>
        /// <param name="item">The item to check.</param>
        /// <returns>True if matcher is ok.</returns>
        public bool Matches(T target, T item);
    }
}
