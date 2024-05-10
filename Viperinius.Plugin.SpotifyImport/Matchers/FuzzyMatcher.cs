using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fastenshtein;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal class FuzzyMatcher : IItemMatcher<string>
    {
        public bool IsStrict => false;

        public bool Matches(string target, string item)
        {
            var maxDistance = Plugin.Instance?.Configuration.MaxFuzzyCharDifference ?? 0;
            return Levenshtein.Distance(target, item) <= maxDistance;
        }
    }
}
