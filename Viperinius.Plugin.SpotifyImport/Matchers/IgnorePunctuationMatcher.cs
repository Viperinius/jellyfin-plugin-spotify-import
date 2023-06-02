using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal class IgnorePunctuationMatcher : IItemMatcher<string>
    {
        private readonly string _pattern = @"\p{P}";

        public bool IsStrict => false;

        public bool Matches(string target, string item)
        {
            var i = Regex.Replace(item, _pattern, string.Empty);
            var t = Regex.Replace(target, _pattern, string.Empty);
            return new CaseInsensitiveMatcher().Matches(t, i);
        }
    }
}
