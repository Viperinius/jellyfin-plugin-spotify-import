using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal class AlbumFromTrackMatcher : IItemMatcher<string>
    {
        private static readonly Regex _regex = new Regex(@"\s*(?:[\([\[]From\s(?:[^""]+\s)?""([^\)\]""]*)""[\)\]]|-\sFrom\s(?:[^""]+\s)?""([^\)\]""]*)"")"); // find patterns like 'xyz (From "abc")', 'xyz [From "abc"]' and 'xyz - From "abc"'

        public bool IsStrict => false;

        public bool Matches(string target, string item)
        {
            var i = _regex.Replace(item, string.Empty);
            return new IgnoreParensMatcher().Matches(target, i);
        }

        public static bool TryGetAlbumNameFromTrack(string item, out string album)
        {
            album = string.Empty;
            var match = _regex.Match(item);
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
    }
}
