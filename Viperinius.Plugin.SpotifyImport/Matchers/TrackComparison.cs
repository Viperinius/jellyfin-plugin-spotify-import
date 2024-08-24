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
        private static readonly AlbumFromTrackMatcher _albumFromTrackMatcher = new AlbumFromTrackMatcher();
        private static readonly FuzzyMatcher _fuzzyMatcher = new FuzzyMatcher();

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
                    if (!results.TryGetValue(contentPrio, out var thisPrioList))
                    {
                        thisPrioList = new List<string>();
                        results.Add(contentPrio, thisPrioList);
                    }

                    // add combo of base string and remaining parentheses blocks, example: "My Title (Abc)", "My Title (Abc) (feat. Xyz)"
                    thisPrioList.Add(tmp);
                }
            }

            contentPrio++;

            // add parentheses content as separate result, example: "Abc", "foo"
            foreach (var parensContent in parensContents)
            {
                if (!results.TryGetValue(contentPrio, out var thisPrioList))
                {
                    thisPrioList = new List<string>();
                    results.Add(contentPrio, thisPrioList);
                }

                thisPrioList.Add(parensContent);
            }

            return results;
        }

        private static Result Equal(string? jellyfinName, string? providerName, ItemMatchLevel matchLevel)
        {
            if (string.IsNullOrEmpty(jellyfinName) || string.IsNullOrEmpty(providerName))
            {
                return new Result(false);
            }

            var resultsByCombinedPrio = new SortedDictionary<int, Result>();

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
                    if (!resultsByCombinedPrio.TryGetValue(combinedPrio, out var thisResult))
                    {
                        thisResult = new Result(false);
                        resultsByCombinedPrio.Add(combinedPrio, thisResult);
                    }

                    foreach (var jellyfinCandidate in jellyfinCandidateByPrio.Value)
                    {
                        foreach (var providerCandidate in providerCandidateByPrio.Value)
                        {
                            var result = _defaultStringMatcher.Matches(jellyfinCandidate, providerCandidate);
                            ItemMatchLevel? resultLevel = result ? ItemMatchLevel.Default : null;

                            if (!result && matchLevel >= ItemMatchLevel.IgnoreCase)
                            {
                                result |= _caseInsensitiveMatcher.Matches(jellyfinCandidate, providerCandidate);
                                resultLevel = result ? ItemMatchLevel.IgnoreCase : null;
                            }

                            if (!result && matchLevel >= ItemMatchLevel.IgnorePunctuationAndCase)
                            {
                                result |= _punctuationMatcher.Matches(jellyfinCandidate, providerCandidate);
                                resultLevel = result ? ItemMatchLevel.IgnorePunctuationAndCase : null;
                            }

                            if (!result && matchLevel >= ItemMatchLevel.IgnoreParensPunctuationAndCase)
                            {
                                result |= _parensMatcher.Matches(jellyfinCandidate, providerCandidate);
                                resultLevel = result ? ItemMatchLevel.IgnoreParensPunctuationAndCase : null;
                            }

                            if (!result && matchLevel >= ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack)
                            {
                                result |= _albumFromTrackMatcher.Matches(jellyfinCandidate, providerCandidate);
                                resultLevel = result ? ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack : null;
                            }

                            if (!result && matchLevel >= ItemMatchLevel.Fuzzy)
                            {
                                result |= _fuzzyMatcher.Matches(jellyfinCandidate, providerCandidate);
                                resultLevel = result ? ItemMatchLevel.Fuzzy : null;
                            }

                            thisResult.ComparisonResult |= result;
                            if (thisResult.MatchedLevel == null || thisResult.MatchedLevel > resultLevel)
                            {
                                thisResult.MatchedLevel = resultLevel;
                            }

                            if (thisResult.MatchedPrio == null || thisResult.MatchedPrio > combinedPrio)
                            {
                                thisResult.MatchedPrio = combinedPrio;
                            }
                        }
                    }
                }
            }

            // check if any combo was matched and return the one with the lowest prio value (first one found)
            foreach (var result in resultsByCombinedPrio)
            {
                if (result.Value.ComparisonResult && result.Value.MatchedLevel != null)
                {
                    return result.Value;
                }
            }

            return new Result(false);
        }

        private static bool ListContains(IReadOnlyList<string>? jellyfinList, string? providerName, ItemMatchLevel matchLevel)
        {
            if (jellyfinList == null)
            {
                return false;
            }

            return jellyfinList.Where(j => Equal(j, providerName, matchLevel).ComparisonResult).Any();
        }

        private static bool ListMatchOneItem(IReadOnlyList<string>? jellyfinList, IReadOnlyList<string> providerList, ItemMatchLevel matchLevel)
        {
            return (jellyfinList?.Any(j => ListContains(providerList, j, matchLevel)) ?? false) || providerList.Any(p => ListContains(jellyfinList, p, matchLevel));
        }

        public static Result TrackNameEqual(Audio jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            return Equal(jfItem.Name, providerItem.Name, matchLevel);
        }

        private static Result AlbumNameEqualInner(string? jfName, string? providerName, string? providerTrackName, ItemMatchLevel matchLevel)
        {
            if (!string.IsNullOrEmpty(providerTrackName) &&
                matchLevel >= ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack &&
                AlbumFromTrackMatcher.TryGetAlbumNameFromTrack(providerTrackName, out var foundAlbum))
            {
                var result = Equal(jfName, foundAlbum, matchLevel);
                if (result.ComparisonResult)
                {
                    return result;
                }
            }

            return Equal(jfName, providerName, matchLevel);
        }

        public static Result AlbumNameEqual(Audio jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            var resultEntity = AlbumNameEqualInner(jfItem.AlbumEntity?.Name, providerItem.AlbumName, providerItem.Name, matchLevel);
            if (resultEntity.ComparisonResult)
            {
                return resultEntity;
            }

            return AlbumNameEqualInner(jfItem.Album, providerItem.AlbumName, providerItem.Name, matchLevel);
        }

        public static Result AlbumNameEqual(MusicAlbum jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            return AlbumNameEqualInner(jfItem.Name, providerItem.AlbumName, providerItem.Name, matchLevel);
        }

        public static bool AlbumArtistOneContained(Audio jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            var correctedMatchLevel = matchLevel >= ItemMatchLevel.Fuzzy ? ItemMatchLevel.IgnoreParensPunctuationAndCase : matchLevel;
            return ListMatchOneItem(jfItem.AlbumEntity?.Artists, providerItem.AlbumArtistNames, correctedMatchLevel) ||
                   ListMatchOneItem(jfItem.AlbumArtists, providerItem.AlbumArtistNames, correctedMatchLevel);
        }

        public static bool AlbumArtistOneContained(MusicAlbum jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            var correctedMatchLevel = matchLevel >= ItemMatchLevel.Fuzzy ? ItemMatchLevel.IgnoreParensPunctuationAndCase : matchLevel;
            return ListMatchOneItem(jfItem.Artists, providerItem.AlbumArtistNames, correctedMatchLevel);
        }

        public static bool ArtistOneContained(Audio jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            var correctedMatchLevel = matchLevel >= ItemMatchLevel.Fuzzy ? ItemMatchLevel.IgnoreParensPunctuationAndCase : matchLevel;
            return ListMatchOneItem(jfItem.Artists, providerItem.ArtistNames, correctedMatchLevel);
        }

        public static bool ArtistOneContained(MusicArtist jfItem, ProviderTrackInfo providerItem, ItemMatchLevel matchLevel)
        {
            var correctedMatchLevel = matchLevel >= ItemMatchLevel.Fuzzy ? ItemMatchLevel.IgnoreParensPunctuationAndCase : matchLevel;
            return ListMatchOneItem(new List<string> { jfItem.Name }, providerItem.ArtistNames, correctedMatchLevel);
        }

        public class Result
        {
            public Result(bool result, ItemMatchLevel? level = null, int? prio = null)
            {
                ComparisonResult = result;
                MatchedLevel = level;
                MatchedPrio = prio;
            }

            public bool ComparisonResult { get; set; }

            public ItemMatchLevel? MatchedLevel { get; set; }

            public int? MatchedPrio { get; set; }
        }
    }
}
