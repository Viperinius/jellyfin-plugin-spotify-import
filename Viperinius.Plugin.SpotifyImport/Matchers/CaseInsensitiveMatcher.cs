using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal class CaseInsensitiveMatcher : IItemMatcher<string>
    {
        public bool IsStrict => true;

        public bool Matches(string target, string item)
        {
            return item.Equals(target, StringComparison.OrdinalIgnoreCase);
        }
    }
}
