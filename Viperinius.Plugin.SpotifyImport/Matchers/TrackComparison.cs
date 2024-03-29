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

        private static readonly Regex _parensRegex = new Regex(@"\s*\(([^\)]*)\)\s*");

        private static List<string> TrySplitParensContents(string? raw)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return results;
            }

            results.Add(raw);
            foreach (Match match in _parensRegex.Matches(raw))
            {
                results.Add(match.Groups[1].Value);
            }

            return results;
        }

        private static bool Equal(string? jellyfinName, string? providerName, ItemMatchLevel matchLevel)
        {
            var result = false;
            if (string.IsNullOrEmpty(jellyfinName) || string.IsNullOrEmpty(providerName))
            {
                return result;
            }

            var jellyfinCandidates = TrySplitParensContents(jellyfinName);
            var providerCandidates = TrySplitParensContents(providerName);

            foreach (var jellyfinCandidate in jellyfinCandidates)
            {
                foreach (var providerCandidate in providerCandidates)
                {
                    result |= _defaultStringMatcher.Matches(jellyfinCandidate, providerCandidate);

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

                    if (result)
                    {
                        return result;
                    }
                }
            }

            return result;
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
