using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal class DefaultMatcher<T> : IItemMatcher<T>
    {
        public bool IsStrict => true;

        public bool Matches(T target, T item)
        {
            return item?.Equals(target) ?? false;
        }
    }
}
