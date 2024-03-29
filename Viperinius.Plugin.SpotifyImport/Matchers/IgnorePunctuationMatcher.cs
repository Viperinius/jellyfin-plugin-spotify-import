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
        private static readonly Regex _regex = new Regex(@"\p{P}");
        private static readonly Regex _whitespaceRegex = new Regex(@"\s+");

        public bool IsStrict => false;

        public bool Matches(string target, string item)
        {
            var i = _regex.Replace(item, string.Empty);
            var t = _regex.Replace(target, string.Empty);

            // replace multiple whitespace chars with one
            i = _whitespaceRegex.Replace(i, " ");
            t = _whitespaceRegex.Replace(t, " ");

            return new CaseInsensitiveMatcher().Matches(t, i);
        }
    }
}
