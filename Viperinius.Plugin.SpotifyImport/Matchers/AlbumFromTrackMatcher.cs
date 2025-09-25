using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal partial class AlbumFromTrackMatcher : IItemMatcher<string>
    {
        public bool IsStrict => false;

        public bool Matches(string target, string item)
        {
            var i = TheRegex().Replace(item, string.Empty);
            return new IgnoreParensMatcher().Matches(target, i);
        }

        public static bool TryGetAlbumNameFromTrack(string item, out string album)
        {
            album = string.Empty;
            var match = TheRegex().Match(item);
            if (!match.Success)
            {
                return false;
            }

            if (match.Groups[1].Success)
            {
                // matched 'abc' coming from '(From "abc")' or '[From "abc"]'
                album = match.Groups[1].Value;
                return true;
            }

            if (match.Groups[2].Success)
            {
                // matched 'abc' coming from '- From "abc"'
                album = match.Groups[2].Value;
                return true;
            }

            return false;
        }

        [GeneratedRegex(@"\s*(?:[\([\[]From\s(?:[^""]+\s)?""([^\)\]""]*)""[\)\]]|-\sFrom\s(?:[^""]+\s)?""([^\)\]""]*)"")")] // find patterns like 'xyz (From "abc")', 'xyz [From "abc"]' and 'xyz - From "abc"'
        private static partial Regex TheRegex();
    }
}
