using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal class IgnoreParensMatcher : IItemMatcher<string>
    {
        private static readonly Regex _regex = new Regex(@"\s*\([^\)]*\)\s*");

        public bool IsStrict => false;

        public bool Matches(string target, string item)
        {
            var i = _regex.Replace(item, string.Empty);
            var t = _regex.Replace(target, string.Empty);
            return new IgnorePunctuationMatcher().Matches(t, i);
        }
    }
}
