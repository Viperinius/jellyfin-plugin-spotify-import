using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;

namespace Viperinius.Plugin.SpotifyImport.Matchers
{
    internal class TrackComparison
    {
        private static readonly DefaultMatcher<string> _defaultStringMatcher = new DefaultMatcher<string>();
        private static readonly CaseInsensitiveMatcher _caseInsensitiveMatcher = new CaseInsensitiveMatcher();
        private static readonly IgnorePunctuationMatcher _punctuationMatcher = new IgnorePunctuationMatcher();
        private static readonly IgnoreParensMatcher _parensMatcher = new IgnoreParensMatcher();

        private static readonly Regex _parensRegex = new Regex(@"\s*[\(\[]([^\)\]]*)[\)\]]\s*$"); // find *last* occurence of (foo) or [foo]

        private static SortedDictionary<int, List<string>> TrySplitParensContents(string? raw)
        {
            var results = new SortedDictionary<int, List<string>>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return results;
            }

            var contentPrio = 1;

            // add the full string, example: "My Title (Abc) (feat. Xyz) [foo]"
            results.Add(contentPrio, new List<string> { raw });
            contentPrio++;

            var parensContents = new List<string>();
            var tmp = raw;
            var match = _parensRegex.Match(tmp);
            while (match.Success && !string.IsNullOrWhiteSpace(tmp))
            {
                parensContents.Add(match.Groups[1].Value);
                tmp = _parensRegex.Replace(tmp, string.Empty);
                match = _parensRegex.Match(tmp);

                // only add to results if tmp still has at least one parentheses block
                if (match.Success)
                {
                    if (!results.ContainsKey(contentPrio))
                    {
                        results.Add(contentPrio, new List<string>());
                    }

                    // add combo of base string and remaining parentheses blocks, example: "My Title (Abc)", "My Title (Abc) (feat. Xyz)"
                    results[contentPrio].Add(tmp);
                }
            }

            contentPrio++;

            // add parentheses content as separate result, example: "Abc", "foo"
            foreach (var parensContent in parensContents)
            {
                if (!results.ContainsKey(contentPrio))
                {
                    results.Add(contentPrio, new List<string>());
                }

                results[contentPrio].Add(parensContent);
            }

            return results;
        }

        private static bool Equal(string? jellyfinName, string? providerName, ItemMatchLevel matchLevel)
        {
            if (string.IsNullOrEmpty(jellyfinName) || string.IsNullOrEmpty(providerName))
            {
                return false;
            }

            var resultsByCombinedPrio = new SortedDictionary<int, bool>();

            // break name into parts
            var jellyfinCandidates = TrySplitParensContents(jellyfinName);
            var providerCandidates = TrySplitParensContents(providerName);

            // try to match combinations of the name parts
            // (realistically there shouldn't exist more than a handful of entries per loop,
            // so this hideous nesting of loops should be fine)
            foreach (var jellyfinCandidateByPrio in jellyfinCandidates)
            {
                foreach (var providerCandidateByPrio in providerCandidates)
                {
                    var combinedPrio = jellyfinCandidateByPrio.Key + providerCandidateByPrio.Key;
                    if (!resultsByCombinedPrio.ContainsKey(combinedPrio))
                    {
                        resultsByCombinedPrio.Add(combinedPrio, false);
                    }

                    foreach (var jellyfinCandidate in jellyfinCandidateByPrio.Value)
                    {
                        foreach (var providerCandidate in providerCandidateByPrio.Value)
                        {
                            var result = _defaultStringMatcher.Matches(jellyfinCandidate, providerCandidate);

                            if (!result && matchLevel >= ItemMatchLevel.IgnoreCase)
                            {
                                result |= _caseInsensitiveMatcher.Matches(jellyfinCandidate, providerCandidate);
                            }

                            if (!result && matchLevel >= ItemMatchLevel.IgnorePunctuationAndCase)
                            {
                                result |= _punctuationMatcher.Matches(jellyfinCandidate, providerCandidate);
                            }

                            if (!result && matchLevel >= ItemMatchLevel.IgnoreParensPunctuationAndCase)
                            {
                                result |= _parensMatcher.Matches(jellyfinCandidate, providerCandidate);
                            }

                            resultsByCombinedPrio[combinedPrio] |= result;
                        }
                    }
                }
            }

            // check if any combo was matched and return the one with the lowest prio value (first one found)
            foreach (var result in resultsByCombinedPrio)
            {
                if (result.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ListContains(IReadOnlyList<string>? jellyfinList, string? providerName, ItemMatchLevel matchLevel)
        {
            if (jellyfinList == null)
            {
                return false;
            }

            return jellyfinList.Where(j => Equal(j, providerName, matchLevel)).Any();
        }

        private static bool ListMatchOneItem(IReadOnlyList<string>? jellyfinList, IReadOnlyList<string> providerList, ItemMatchLevel matchLevel)
        {
            return (jellyfinList?.Any(j => ListContains(providerList, j, matchLevel)) ?? false) || providerList.Any(p => ListContains(jellyfinList, p, matchLevel));
        }

        public static bool TrackNameEqual(Audio jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            return Equal(jfItem.Name, providerItem.Name, matchLevel);
        }

        public static bool AlbumNameEqual(Audio jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            return Equal(jfItem.AlbumEntity?.Name, providerItem.AlbumName, matchLevel) ||
                   Equal(jfItem.Album, providerItem.AlbumName, matchLevel);
        }

        public static bool AlbumNameEqual(MusicAlbum jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            return Equal(jfItem.Name, providerItem.AlbumName, matchLevel);
        }

        public static bool AlbumArtistOneContained(Audio jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            return ListMatchOneItem(jfItem.AlbumEntity?.Artists, providerItem.AlbumArtistNames, matchLevel) ||
                   ListMatchOneItem(jfItem.AlbumArtists, providerItem.AlbumArtistNames, matchLevel);
        }

        public static bool AlbumArtistOneContained(MusicAlbum jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            return ListMatchOneItem(jfItem.Artists, providerItem.AlbumArtistNames, matchLevel);
        }

        public static bool ArtistOneContained(Audio jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            return ListMatchOneItem(jfItem.Artists, providerItem.ArtistNames, matchLevel);
        }

        public static bool ArtistOneContained(MusicArtist jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            return ListMatchOneItem(new List<string> { jfItem.Name }, providerItem.ArtistNames, matchLevel);
        }
    }
}
