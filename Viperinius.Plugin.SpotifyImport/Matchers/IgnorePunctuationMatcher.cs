using System.Text.RegularExpressions;
using Jellyfin.Extensions;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal partial class IgnorePunctuationMatcher : IItemMatcher<string>
    {
        public bool IsStrict => false;

        public bool Matches(string target, string item)
        {
            // remove parts considered equivalent
            var i = AmpersandAndEquivalenceRegex().Replace(item, " ");
            var t = AmpersandAndEquivalenceRegex().Replace(target, " ");

            i = TheRegex().Replace(i, string.Empty);
            t = TheRegex().Replace(t, string.Empty);

            // replace multiple whitespace chars with one and try to normalise any diacritics
            i = WhitespaceRegex().Replace(i, " ").RemoveDiacritics();
            t = WhitespaceRegex().Replace(t, " ").RemoveDiacritics();

            return new CaseInsensitiveMatcher().Matches(t, i);
        }

        [GeneratedRegex(@"\p{P}")]
        private static partial Regex TheRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        [GeneratedRegex(@"\s+(&|and)\s+", RegexOptions.IgnoreCase)]
        private static partial Regex AmpersandAndEquivalenceRegex();
    }
}
