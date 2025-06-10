using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Viperinius.Plugin.SpotifyImport.Tests.TestHelpers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Matchers
{
    public sealed class TrackComparisonTests : IDisposable
    {
        public void Dispose()
        {
            TrackHelper.ClearAlbums();
        }

        [Theory]
        [ClassData(typeof(TrackDefaultDataMatch))]
        [ClassData(typeof(TrackDefaultDataNoMatch))]
        public void Track_Default(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem(provName, "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem(jfName, "Album", "Artist On Album", "Just Artist");

            var result = TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.Default);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.Default, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class TrackDefaultDataMatch : IEnumerable<object[]>
        {
            public IEnumerable<object[]> GetDefault()
            {
                yield return new object[] { "Track", "Track", true };
                yield return new object[] { "foo (Track)", "Track", true };
                yield return new object[] { "foo (Track) (bar)", "Track", true };
                yield return new object[] { "foo [Track] (bar)", "Track", true };
                yield return new object[] { "foo [Track] [bar]", "Track", true };
                yield return new object[] { "Track (hello)", "Track (hello)", true };
                yield return new object[] { "Track - here2", "Track - here2", true };
                yield return new object[] { "Track [y]", "Track [y]", true };
            }

            public virtual IEnumerator<object[]> GetEnumerator() => GetDefault().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        class TrackDefaultDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "track", "Track", false };
                yield return new object[] { ".Track", "Track", false };
                yield return new object[] { "Tr'ack", "Track", false };
                yield return new object[] { "Tr####ack", "Track", false };
                yield return new object[] { "Track (x)", "Track", false };
                yield return new object[] { "foo (track)", "Track", false };
                yield return new object[] { "foo (tra) (ck)", "Track", false };
                yield return new object[] { "Track", "Track (From \"abc\")", false };
                yield return new object[] { "Track", "Track (From xyz1 \"abc\")", false };
                yield return new object[] { "Track", "Track [From \"abc\"]", false };
                yield return new object[] { "Track", "Track - From \"abc\"", false };
                yield return new object[] { "Abcéè Dçô Xyz2", "Abcee Dco Xyz2", false };
                yield return new object[] { "ầbcee Đưo Xạ2", "abcee Duo Xa2", false };
                yield return new object[] { "Aæc Xøyñ2", "Aaec Xoyn2", false };
                yield return new object[] { "Abcä DöÖ Xüß2", "Abca DoO Xuß2", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate&ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(TrackCaseInsensitiveDataMatch))]
        [ClassData(typeof(TrackCaseInsensitiveDataNoMatch))]
        public void Track_CaseInsensitive(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem(provName, "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem(jfName, "Album", "Artist On Album", "Just Artist");

            var result = TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnoreCase);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.IgnoreCase, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class TrackCaseInsensitiveDataMatch : TrackDefaultDataMatch
        {
            public IEnumerable<object[]> GetCaseInsensitive()
            {
                yield return new object[] { "track", "Track", true };
                yield return new object[] { "foo (track)", "Track", true };
                yield return new object[] { "foo (track) (bar)", "Track", true };
                yield return new object[] { "foo [track] (bar)", "Track", true };
                yield return new object[] { "foo [track] [bar]", "Track", true };
                yield return new object[] { "track (hello)", "Track (hello)", true };
                yield return new object[] { "track - here2", "Track - here2", true };
                yield return new object[] { "track [y]", "Track [y]", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
            }
        }
        class TrackCaseInsensitiveDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "track.", "Track", false };
                yield return new object[] { ".Track", "Track", false };
                yield return new object[] { "Tr'ack", "Track", false };
                yield return new object[] { "Tr####ack", "Track", false };
                yield return new object[] { "Track (x)", "Track", false };
                yield return new object[] { "foo (-track)", "Track", false };
                yield return new object[] { "foo (tra) (ck)", "Track", false };
                yield return new object[] { "Track", "Track (From \"abc\")", false };
                yield return new object[] { "Track", "Track (From xyz1 \"abc\")", false };
                yield return new object[] { "Track", "Track [From \"abc\"]", false };
                yield return new object[] { "Track", "Track - From \"abc\"", false };
                yield return new object[] { "Abcéè Dçô Xyz2", "Abcee Dco Xyz2", false };
                yield return new object[] { "ầbcee Đưo Xạ2", "abcee Duo Xa2", false };
                yield return new object[] { "Aæc Xøyñ2", "Aaec Xoyn2", false };
                yield return new object[] { "Abcä DöÖ Xüß2", "Abca DoO Xuß2", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate&ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(TrackNoPunctuationDataMatch))]
        [ClassData(typeof(TrackNoPunctuationDataNoMatch))]
        public void Track_NoPunctuation(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem(provName, "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem(jfName, "Album", "Artist On Album", "Just Artist");

            var result = TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.IgnorePunctuationAndCase, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class TrackNoPunctuationDataMatch : TrackCaseInsensitiveDataMatch
        {
            public IEnumerable<object[]> GetNoPunctuation()
            {
                yield return new object[] { ".track", "Track", true };
                yield return new object[] { "tr,ack", "Track", true };
                yield return new object[] { "tr###ack", "Track", true };
                yield return new object[] { "Trac!k", "Track", true };
                yield return new object[] { "tr-ack", "Track", true };
                yield return new object[] { "tr&ack", "Track", true };
                yield return new object[] { "foo (tr.ack)", "Track", true };
                yield return new object[] { "foo (trac-k) (bar)", "Track", true };
                yield return new object[] { "foo [t,rack] (bar)", "Track", true };
                yield return new object[] { "foo [trac.k] [bar]", "Track", true };
                yield return new object[] { "Tr'ack (hello)", "Track (hello)", true };
                yield return new object[] { "tr-ack - here2", "Track - here2", true };
                yield return new object[] { "Tra\"ck [y]", "Track [y]", true };
                yield return new object[] { "Track (hello)", "Track - hello", true };
                yield return new object[] { "Track (hello) (foo)", "Track - hello (foo)", true };
                yield return new object[] { "Abcéè Dçô Xyz2", "Abcee Dco Xyz2", true };
                yield return new object[] { "ầbcee Đưo Xạ2", "abcee Duo Xa2", true };
                yield return new object[] { "Aæc Xøyñ2", "Aaec Xoyn2", true };
                yield return new object[] { "Abcä DöÖ Xüß2", "Abca DoO Xuß2", true };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate&ver", true };
                yield return new object[] { "Banda And Whate&ver", "Banda & Whate&ver", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoPunctuation())
                {
                    yield return obj;
                }
            }
        }
        class TrackNoPunctuationDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "trac.", "Track", false };
                yield return new object[] { ".$Track", "Track", false };
                yield return new object[] { "Tr`ack", "Track", false };
                yield return new object[] { "Tr0ack", "Track", false };
                yield return new object[] { "Track (x)", "Track", false };
                yield return new object[] { "foo (tra) (ck)", "Track", false };
                yield return new object[] { "Track", "Track (From \"abc\")", false };
                yield return new object[] { "Track", "Track (From xyz1 \"abc\")", false };
                yield return new object[] { "Track", "Track [From \"abc\"]", false };
                yield return new object[] { "Track", "Track - From \"abc\"", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(TrackNoParensDataMatch))]
        [ClassData(typeof(TrackNoParensDataNoMatch))]
        public void Track_NoParens(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem(provName, "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem(jfName, "Album", "Artist On Album", "Just Artist");

            var result = TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCase);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.IgnoreParensPunctuationAndCase, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class TrackNoParensDataMatch : TrackNoPunctuationDataMatch
        {
            public IEnumerable<object[]> GetNoParens()
            {
                yield return new object[] { "Track (abc)", "Track", true };
                yield return new object[] { "(asdkas) track", "Track", true };
                yield return new object[] { "Tra (b) ck", "Track", true };
                yield return new object[] { "a (b) (Track)", "Track", true };
                yield return new object[] { "a (b) [c] (Track)", "Track", true };
                yield return new object[] { "a (b) [c)] (Track)", "Track", true };
                yield return new object[] { "track [123]", "Track", true };
                yield return new object[] { "Track", "Track (From \"abc\")", true };
                yield return new object[] { "Track", "Track (From xyz1 \"abc\")", true };
                yield return new object[] { "Track", "Track [From \"abc\"]", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoPunctuation())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoParens())
                {
                    yield return obj;
                }
            }
        }
        class TrackNoParensDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "trac.", "Track", false };
                yield return new object[] { ".$Track", "Track", false };
                yield return new object[] { "Tr`ack", "Track", false };
                yield return new object[] { "Tr0ack", "Track", false };
                yield return new object[] { "Trackb (x)", "Track", false };
                yield return new object[] { "foo (tra) (ck)", "Track", false };
                yield return new object[] { "Track (hello)", "Track - hello (foo)", false };
                yield return new object[] { "Track", "Track - From \"abc\"", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(TrackNoParensAlbumFromTrackDataMatch))]
        [ClassData(typeof(TrackNoParensAlbumFromTrackDataNoMatch))]
        public void Track_NoParensAlbumFromTrack(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem(provName, "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem(jfName, "Album", "Artist On Album", "Just Artist");

            var result = TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class TrackNoParensAlbumFromTrackDataMatch : TrackNoParensDataMatch
        {
            public IEnumerable<object[]> GetNoParensAlbumFromTrack()
            {
                yield return new object[] { "Track", "Track - From \"abc\"", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoPunctuation())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoParens())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoParensAlbumFromTrack())
                {
                    yield return obj;
                }
            }
        }
        class TrackNoParensAlbumFromTrackDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "trac.", "Track", false };
                yield return new object[] { ".$Track", "Track", false };
                yield return new object[] { "Tr`ack", "Track", false };
                yield return new object[] { "Tr0ack", "Track", false };
                yield return new object[] { "Trackb (x)", "Track", false };
                yield return new object[] { "foo (tra) (ck)", "Track", false };
                yield return new object[] { "Track (hello)", "Track - hello (foo)", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(TrackFuzzyDataMatch))]
        [ClassData(typeof(TrackFuzzyDataNoMatch))]
        public void Track_Fuzzy(string jfName, string provName, bool shouldMatch)
        {
            TrackHelper.SetValidPluginInstance();
            var prov = TrackHelper.CreateProviderItem(provName, "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem(jfName, "Album", "Artist On Album", "Just Artist");

            var result = TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.Fuzzy);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.Fuzzy, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class TrackFuzzyDataMatch : TrackNoParensDataMatch
        {
            public IEnumerable<object[]> GetFuzzy()
            {
                yield return new object[] { "Track 2", "Track", true };
                yield return new object[] { "abcdefghijklmnopqrstuvwxyz", "abcdefghijklmnopqrstuvwxyz1", true };
                yield return new object[] { "abcdefghijklmnopqrstuvwxyz", "abcdefghijklmnopqrstuvwxyz12", true };
                yield return new object[] { "abcdefghijklmnopqrstuvwxyz", "abcdefghijklmnopqrstuvwxy", true };
                yield return new object[] { "abcdefghijklmnopqrstuvwxyz", "abcdefghijklmnopqrstuvwx", true };
                yield return new object[] { "abcdefghijklmnopqrstuvwxyz", "abcdefghijklmnopqrstuvwx12", true };
                yield return new object[] { ".$Track", "Track", true };
                yield return new object[] { "Tr`ack", "Track", true };
                yield return new object[] { "Tr0ack", "Track", true };
                yield return new object[] { "trac.", "Track", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoPunctuation())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoParens())
                {
                    yield return obj;
                }
                foreach (var obj in GetFuzzy())
                {
                    yield return obj;
                }
            }
        }
        class TrackFuzzyDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "Track123", "Track", false };
                yield return new object[] { "Trackb (x)", "Track", false };
                yield return new object[] { "foo (tra) (ck)", "Track", false };
                yield return new object[] { "Track (hello)", "Track - hello (foo)", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void Track_Ignores_Other_Fields()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", null, null, null),
                TrackHelper.CreateJfItem("Track", null, null, "Just Artist"),
                TrackHelper.CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", null, "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", null),
            };

            foreach (var level in Enum.GetValues<ItemMatchLevel>())
            {
                foreach (var jf in items)
                {
                    Assert.True(TrackComparison.TrackNameEqual(jf, prov, level).ComparisonResult, TrackHelper.GetErrorString(jf));
                }
            }

            items = new List<Audio>
            {
                TrackHelper.CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, null),
            };

            foreach (var level in Enum.GetValues<ItemMatchLevel>())
            {
                foreach (var jf in items)
                {
                    Assert.False(TrackComparison.TrackNameEqual(jf, prov, level).ComparisonResult, TrackHelper.GetErrorString(jf));
                }
            }
        }

        [Theory]
        [ClassData(typeof(AlbumDefaultDataMatch))]
        [ClassData(typeof(AlbumDefaultDataNoMatch))]
        public void Album_Default(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", provName, new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem("Track", jfName, "Artist On Album", "Just Artist");

            var result = TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.Default);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.Default, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class AlbumDefaultDataMatch : IEnumerable<object[]>
        {
            public IEnumerable<object[]> GetDefault()
            {
                yield return new object[] { "Album", "Album", true };
                yield return new object[] { "foo (Album)", "Album", true };
                yield return new object[] { "foo (Album) (bar)", "Album", true };
                yield return new object[] { "foo [Album] (bar)", "Album", true };
                yield return new object[] { "foo [Album] [bar]", "Album", true };
                yield return new object[] { "Album (hello)", "Album (hello)", true };
                yield return new object[] { "Album - here2", "Album - here2", true };
                yield return new object[] { "Album [y]", "Album [y]", true };
            }

            public virtual IEnumerator<object[]> GetEnumerator() => GetDefault().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        class AlbumDefaultDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "album", "Album", false };
                yield return new object[] { ".Album", "Album", false };
                yield return new object[] { "Al'bum", "Album", false };
                yield return new object[] { "Al####bum", "Album", false };
                yield return new object[] { "Albu", "Album", false };
                yield return new object[] { "Album (x)", "Album", false };
                yield return new object[] { "foo (album)", "Album", false };
                yield return new object[] { "foo (al) (bum)", "Album", false };
                yield return new object[] { "(Album) foo", "Album", false };
                yield return new object[] { "Abcéè Dçô Xyz2", "Abcee Dco Xyz2", false };
                yield return new object[] { "ầbcee Đưo Xạ2", "abcee Duo Xa2", false };
                yield return new object[] { "Aæc Xøyñ2", "Aaec Xoyn2", false };
                yield return new object[] { "Abcä DöÖ Xüß2", "Abca DoO Xuß2", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate&ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(AlbumCaseInsensitiveDataMatch))]
        [ClassData(typeof(AlbumCaseInsensitiveDataNoMatch))]
        public void Album_CaseInsensitive(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", provName, new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem("Track", jfName, "Artist On Album", "Just Artist");

            var result = TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreCase);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.IgnoreCase, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class AlbumCaseInsensitiveDataMatch : AlbumDefaultDataMatch
        {
            public IEnumerable<object[]> GetCaseInsensitive()
            {
                yield return new object[] { "album", "Album", true };
                yield return new object[] { "foo (album)", "Album", true };
                yield return new object[] { "foo (album) (bar)", "Album", true };
                yield return new object[] { "foo [album] (bar)", "Album", true };
                yield return new object[] { "foo [album] [bar]", "Album", true };
                yield return new object[] { "album (hello)", "Album (hello)", true };
                yield return new object[] { "album - here2", "Album - here2", true };
                yield return new object[] { "album [y]", "Album [y]", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
            }
        }
        class AlbumCaseInsensitiveDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "album.", "Album", false };
                yield return new object[] { ".Album", "Album", false };
                yield return new object[] { "Al'bum", "Album", false };
                yield return new object[] { "Al####bum", "Album", false };
                yield return new object[] { "Albu", "Album", false };
                yield return new object[] { "Album (x)", "Album", false };
                yield return new object[] { "foo (-album)", "Album", false };
                yield return new object[] { "foo (al) (bum)", "Album", false };
                yield return new object[] { "(Album) foo", "Album", false };
                yield return new object[] { "Abcéè Dçô Xyz2", "Abcee Dco Xyz2", false };
                yield return new object[] { "ầbcee Đưo Xạ2", "abcee Duo Xa2", false };
                yield return new object[] { "Aæc Xøyñ2", "Aaec Xoyn2", false };
                yield return new object[] { "Abcä DöÖ Xüß2", "Abca DoO Xuß2", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate&ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(AlbumNoPunctuationDataMatch))]
        [ClassData(typeof(AlbumNoPunctuationDataNoMatch))]
        public void Album_NoPunctuation(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", provName, new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem("Track", jfName, "Artist On Album", "Just Artist");

            var result = TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.IgnorePunctuationAndCase, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class AlbumNoPunctuationDataMatch : AlbumCaseInsensitiveDataMatch
        {
            public IEnumerable<object[]> GetNoPunctuation()
            {
                yield return new object[] { ".album", "Album", true };
                yield return new object[] { "al,bum", "Album", true };
                yield return new object[] { "al###bum", "Album", true };
                yield return new object[] { "Albu!m", "Album", true };
                yield return new object[] { "al-bum", "Album", true };
                yield return new object[] { "al&bum", "Album", true };
                yield return new object[] { "foo (al.bum)", "Album", true };
                yield return new object[] { "foo (albu-m) (bar)", "Album", true };
                yield return new object[] { "foo [a,lbum] (bar)", "Album", true };
                yield return new object[] { "foo [albu.m] [bar]", "Album", true };
                yield return new object[] { "Al'bum (hello)", "Album (hello)", true };
                yield return new object[] { "al-bum - here2", "Album - here2", true };
                yield return new object[] { "Alb\"um [y]", "Album [y]", true };
                yield return new object[] { "Album (hello)", "Album - hello", true };
                yield return new object[] { "Album (hello) (foo)", "Album - hello (foo)", true };
                yield return new object[] { "Abcéè Dçô Xyz2", "Abcee Dco Xyz2", true };
                yield return new object[] { "ầbcee Đưo Xạ2", "abcee Duo Xa2", true };
                yield return new object[] { "Aæc Xøyñ2", "Aaec Xoyn2", true };
                yield return new object[] { "Abcä DöÖ Xüß2", "Abca DoO Xuß2", true };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate&ver", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoPunctuation())
                {
                    yield return obj;
                }
            }
        }
        class AlbumNoPunctuationDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "albu.", "Album", false };
                yield return new object[] { ".$Album", "Album", false };
                yield return new object[] { "Al`bum", "Album", false };
                yield return new object[] { "Al0bum", "Album", false };
                yield return new object[] { "Album (x)", "Album", false };
                yield return new object[] { "foo (alb) (um)", "Album", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(AlbumNoParensDataMatch))]
        [ClassData(typeof(AlbumNoParensDataNoMatch))]
        public void Album_NoParens(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", provName, new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem("Track", jfName, "Artist On Album", "Just Artist");

            var result = TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCase);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.IgnoreParensPunctuationAndCase, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class AlbumNoParensDataMatch : AlbumNoPunctuationDataMatch
        {
            public IEnumerable<object[]> GetNoParens()
            {
                yield return new object[] { "Album (abc)", "Album", true };
                yield return new object[] { "(asdkas) album", "Album", true };
                yield return new object[] { "Alb (b) um", "Album", true };
                yield return new object[] { "a (b) (Album)", "Album", true };
                yield return new object[] { "a (b) [c] (Album)", "Album", true };
                yield return new object[] { "a (b) [c)] (Album)", "Album", true };
                yield return new object[] { "album [123]", "Album", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoPunctuation())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoParens())
                {
                    yield return obj;
                }
            }
        }
        class AlbumNoParensDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "albu.", "Album", false };
                yield return new object[] { ".$Album", "Album", false };
                yield return new object[] { "Al`bum", "Album", false };
                yield return new object[] { "Al0bum", "Album", false };
                yield return new object[] { "Albumb (x)", "Album", false };
                yield return new object[] { "foo (alb) (um)", "Album", false };
                yield return new object[] { "Album (hello)", "Album - hello (foo)", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(AlbumNoParensAlbumFromTrackDataMatch))]
        [ClassData(typeof(AlbumNoParensAlbumFromTrackDataNoMatch))]
        public void Album_NoParensAlbumFromTrack(string jfName, string provName, bool shouldMatch)
        {
            var provTrackNames = new List<string> { "Track" };
            if (provName == "#usefromtrack#" && shouldMatch)
            {
                provTrackNames[0] = $"Track (From \"{jfName}\")";
                provTrackNames.Add($"Track [From \"{jfName}\"]");
                provTrackNames.Add($"Track - From \"{jfName}\"");
                provTrackNames.Add($"Track (From eurfbsdf \"{jfName}\")");
                provTrackNames.Add($"Track [From 4ro8idaf \"{jfName}\"]");
                provTrackNames.Add($"Track - From wseaEREto5isf \"{jfName}\"");
            }
            if (provName == "#usefromtrack#" && !shouldMatch)
            {
                provTrackNames.Add($"Track [From \"{jfName}]");
                provTrackNames.Add($"Track - From {jfName}\"");
                provTrackNames.Add($"Track (from eurfbsdf \"{jfName}\")");
                provTrackNames.Add($"Track From \"{jfName}\"");
            }

            foreach (var provTrackName in provTrackNames)
            {
                var prov = TrackHelper.CreateProviderItem(provTrackName, provName, new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
                var jf = TrackHelper.CreateJfItem("Track", jfName, "Artist On Album", "Just Artist");

                var result = TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack);
                if (shouldMatch)
                {
                    Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                    Assert.True(result.MatchedLevel <= ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack, TrackHelper.GetErrorString(jf));
                }
                else
                {
                    Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                    Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
                }
            }
        }

        class AlbumNoParensAlbumFromTrackDataMatch : AlbumNoParensDataMatch
        {
            public IEnumerable<object[]> GetNoParensAlbumFromTrack()
            {
                yield return new object[] { "My Great Album", "#usefromtrack#", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoPunctuation())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoParens())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoParensAlbumFromTrack())
                {
                    yield return obj;
                }
            }
        }
        class AlbumNoParensAlbumFromTrackDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "albu.", "Album", false };
                yield return new object[] { ".$Album", "Album", false };
                yield return new object[] { "Al`bum", "Album", false };
                yield return new object[] { "Al0bum", "Album", false };
                yield return new object[] { "Albumb (x)", "Album", false };
                yield return new object[] { "foo (alb) (um)", "Album", false };
                yield return new object[] { "Album (hello)", "Album - hello (foo)", false };
                yield return new object[] { "My Great Album", "#usefromtrack#", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(AlbumFuzzyDataMatch))]
        [ClassData(typeof(AlbumFuzzyDataNoMatch))]
        public void Album_Fuzzy(string jfName, string provName, bool shouldMatch)
        {
            TrackHelper.SetValidPluginInstance();
            var prov = TrackHelper.CreateProviderItem("Track", provName, new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var jf = TrackHelper.CreateJfItem("Track", jfName, "Artist On Album", "Just Artist");

            var result = TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.Fuzzy);
            if (shouldMatch)
            {
                Assert.True(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel <= ItemMatchLevel.Fuzzy, TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(result.ComparisonResult, TrackHelper.GetErrorString(jf));
                Assert.True(result.MatchedLevel == null, TrackHelper.GetErrorString(jf));
            }
        }

        class AlbumFuzzyDataMatch : AlbumNoParensDataMatch
        {
            public IEnumerable<object[]> GetFuzzy()
            {
                yield return new object[] { "Album 2", "Album", true };
                yield return new object[] { ".$Album", "Album", true };
                yield return new object[] { "Al`bum", "Album", true };
                yield return new object[] { "Al0bum", "Album", true };
                yield return new object[] { "albu.", "Album", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoPunctuation())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoParens())
                {
                    yield return obj;
                }
                foreach (var obj in GetFuzzy())
                {
                    yield return obj;
                }
            }
        }
        class AlbumFuzzyDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "Album123", "Album", false };
                yield return new object[] { "Albumb (x)", "Album", false };
                yield return new object[] { "foo (alb) (um)", "Album", false };
                yield return new object[] { "Album (hello)", "Album - hello (foo)", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void Album_Ignores_Other_Fields()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem(null, "Album", null, null),
                TrackHelper.CreateJfItem(null, "Album", null, "Just Artist"),
                TrackHelper.CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", null, "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", null),
            };

            foreach (var level in Enum.GetValues<ItemMatchLevel>())
            {
                foreach (var jf in items)
                {
                    Assert.True(TrackComparison.AlbumNameEqual(jf, prov, level).ComparisonResult, TrackHelper.GetErrorString(jf));
                }
            }

            items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, null),
            };

            foreach (var level in Enum.GetValues<ItemMatchLevel>())
            {
                foreach (var jf in items)
                {
                    Assert.False(TrackComparison.AlbumNameEqual(jf, prov, level).ComparisonResult, TrackHelper.GetErrorString(jf));
                }
            }
        }

        [Theory]
        [ClassData(typeof(AnyArtistDefaultDataMatch))]
        [ClassData(typeof(AnyArtistDefaultDataNoMatch))]
        public void AlbumArtist_Default(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "aaaaaaa", provName, "bbbbbbb" }, new List<string> { "Non-existent", "Artist 2" });
            var jf = TrackHelper.CreateJfItem("Track", "Album", jfName, "Non-existent");

            if (shouldMatch)
            {
                Assert.True(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
        }

        [Theory]
        [ClassData(typeof(AnyArtistDefaultDataMatch))]
        [ClassData(typeof(AnyArtistDefaultDataNoMatch))]
        public void Artist_Default(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Non-existent", "Artist 2" }, new List<string> { "aaaaaaa", provName, "bbbbbbb" });
            var jf = TrackHelper.CreateJfItem("Track", "Album", "Non-existent", jfName);

            if (shouldMatch)
            {
                Assert.True(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
        }

        class AnyArtistDefaultDataMatch : IEnumerable<object[]>
        {
            public IEnumerable<object[]> GetDefault()
            {
                yield return new object[] { "Just Artist", "Just Artist", true };
                yield return new object[] { "foo (Just Artist)", "Just Artist", true };
                yield return new object[] { "foo (Just Artist) (bar)", "Just Artist", true };
                yield return new object[] { "foo [Just Artist] (bar)", "Just Artist", true };
                yield return new object[] { "foo [Just Artist] [bar]", "Just Artist", true };
                yield return new object[] { "Just Artist (hello)", "Just Artist (hello)", true };
                yield return new object[] { "(Just Artist) (hello)", "Just Artist (hello)", true };
                yield return new object[] { "Just Artist - here2", "Just Artist - here2", true };
                yield return new object[] { "Just Artist [y]", "Just Artist [y]", true };
            }

            public virtual IEnumerator<object[]> GetEnumerator() => GetDefault().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        class AnyArtistDefaultDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "just artist", "Just Artist", false };
                yield return new object[] { ".Just Artist", "Just Artist", false };
                yield return new object[] { "Just'Artist", "Just Artist", false };
                yield return new object[] { "Just####Artist", "Just Artist", false };
                yield return new object[] { "Jus Artist", "Just Artist", false };
                yield return new object[] { "Just Artist (x)", "Just Artist", false };
                yield return new object[] { "foo (just artist)", "Just Artist", false };
                yield return new object[] { "foo (just) (artist)", "Just Artist", false };
                yield return new object[] { "(Just Artist) foo", "Just Artist", false };
                yield return new object[] { "a (Just Artist) z", "Just Artist", false };
                yield return new object[] { "Abcéè Dçô Xyz2", "Abcee Dco Xyz2", false };
                yield return new object[] { "ầbcee Đưo Xạ2", "abcee Duo Xa2", false };
                yield return new object[] { "Aæc Xøyñ2", "Aaec Xoyn2", false };
                yield return new object[] { "Abcä DöÖ Xüß2", "Abca DoO Xuß2", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate&ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(AnyArtistCaseInsensitiveDataMatch))]
        [ClassData(typeof(AnyArtistCaseInsensitiveDataNoMatch))]
        public void Artist_CaseInsensitive(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Non-existent", "Artist 2" }, new List<string> { "aaaaaaa", provName, "bbbbbbb" });
            var jf = TrackHelper.CreateJfItem("Track", "Album", "Non-existent", jfName);

            if (shouldMatch)
            {
                Assert.True(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Theory]
        [ClassData(typeof(AnyArtistCaseInsensitiveDataMatch))]
        [ClassData(typeof(AnyArtistCaseInsensitiveDataNoMatch))]
        public void AlbumArtist_CaseInsensitive(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "aaaaaaa", provName, "bbbbbbb" }, new List<string> { "Non-existent", "Artist 2" });
            var jf = TrackHelper.CreateJfItem("Track", "Album", jfName, "Non-existent");

            if (shouldMatch)
            {
                Assert.True(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
        }

        class AnyArtistCaseInsensitiveDataMatch : AnyArtistDefaultDataMatch
        {
            public IEnumerable<object[]> GetCaseInsensitive()
            {
                yield return new object[] { "just artist", "Just Artist", true };
                yield return new object[] { "foo (just Artist)", "Just Artist", true };
                yield return new object[] { "foo (just artist) (bar)", "Just Artist", true };
                yield return new object[] { "foo [just artist] (bar)", "Just Artist", true };
                yield return new object[] { "foo [just artist] [bar]", "Just Artist", true };
                yield return new object[] { "just artist (hello)", "Just Artist (hello)", true };
                yield return new object[] { "just artist - here2", "Just Artist - here2", true };
                yield return new object[] { "just artist [y]", "Just Artist [y]", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
            }
        }
        class AnyArtistCaseInsensitiveDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "just artist.", "Just Artist", false };
                yield return new object[] { ".Just Artist", "Just Artist", false };
                yield return new object[] { "Just' Artist", "Just Artist", false };
                yield return new object[] { "Just####Artist", "Just Artist", false };
                yield return new object[] { "Jus Artist", "Just Artist", false };
                yield return new object[] { "Just Artist (x)", "Just Artist", false };
                yield return new object[] { "foo (-album)", "Just Artist", false };
                yield return new object[] { "foo (al) (bum)", "Just Artist", false };
                yield return new object[] { "(Just Artist) foo", "Just Artist", false };
                yield return new object[] { "Abcéè Dçô Xyz2", "Abcee Dco Xyz2", false };
                yield return new object[] { "ầbcee Đưo Xạ2", "abcee Duo Xa2", false };
                yield return new object[] { "Aæc Xøyñ2", "Aaec Xoyn2", false };
                yield return new object[] { "Abcä DöÖ Xüß2", "Abca DoO Xuß2", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate&ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(AnyArtistNoPunctuationDataMatch))]
        [ClassData(typeof(AnyArtistNoPunctuationDataNoMatch))]
        public void Artist_NoPunctuation(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Non-existent", "Artist 2" }, new List<string> { "aaaaaaa", provName, "bbbbbbb" });
            var jf = TrackHelper.CreateJfItem("Track", "Album", "Non-existent", jfName);

            if (shouldMatch)
            {
                Assert.True(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Theory]
        [ClassData(typeof(AnyArtistNoPunctuationDataMatch))]
        [ClassData(typeof(AnyArtistNoPunctuationDataNoMatch))]
        public void AlbumArtist_NoPunctuation(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "aaaaaaa", provName, "bbbbbbb" }, new List<string> { "Non-existent", "Artist 2" });
            var jf = TrackHelper.CreateJfItem("Track", "Album", jfName, "Non-existent");

            if (shouldMatch)
            {
                Assert.True(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
        }

        class AnyArtistNoPunctuationDataMatch : AnyArtistCaseInsensitiveDataMatch
        {
            public IEnumerable<object[]> GetNoPunctuation()
            {
                yield return new object[] { ".just artist", "Just Artist", true };
                yield return new object[] { "ju,st artist", "Just Artist", true };
                yield return new object[] { "just a###rtist", "Just Artist", true };
                yield return new object[] { "Just Artis!t", "Just Artist", true };
                yield return new object[] { "just ar-tist", "Just Artist", true };
                yield return new object[] { "jus&t Artist", "Just Artist", true };
                yield return new object[] { "foo (just a.rtist)", "Just Artist", true };
                yield return new object[] { "foo (Just arti-st) (bar)", "Just Artist", true };
                yield return new object[] { "foo [just a,rtist] (bar)", "Just Artist", true };
                yield return new object[] { "foo [ju.st artist] [bar]", "Just Artist", true };
                yield return new object[] { "Ju'st Artist (hello)", "Just Artist (hello)", true };
                yield return new object[] { "jus-t artist - here2", "Just Artist - here2", true };
                yield return new object[] { "just ar\"tist [y]", "Just Artist [y]", true };
                yield return new object[] { "Just Artist (hello)", "Just Artist - hello", true };
                yield return new object[] { "Just Artist (hello) (foo)", "Just Artist - hello (foo)", true };
                yield return new object[] { "Abcéè Dçô Xyz2", "Abcee Dco Xyz2", true };
                yield return new object[] { "ầbcee Đưo Xạ2", "abcee Duo Xa2", true };
                yield return new object[] { "Aæc Xøyñ2", "Aaec Xoyn2", true };
                yield return new object[] { "Abcä DöÖ Xüß2", "Abca DoO Xuß2", true };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate&ver", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoPunctuation())
                {
                    yield return obj;
                }
            }
        }
        class AnyArtistNoPunctuationDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "jus. artist", "Just Artist", false };
                yield return new object[] { ".$Just Artist", "Just Artist", false };
                yield return new object[] { "Just Ar`tist", "Just Artist", false };
                yield return new object[] { "Just A0rtist", "Just Artist", false };
                yield return new object[] { "Just Artist (x)", "Just Artist", false };
                yield return new object[] { "foo (just) (artist)", "Just Artist", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(AnyArtistNoParensDataMatch))]
        [ClassData(typeof(AnyArtistNoParensDataNoMatch))]
        public void Artist_NoParens(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Non-existent", "Artist 2" }, new List<string> { "aaaaaaa", provName, "bbbbbbb" });
            var jf = TrackHelper.CreateJfItem("Track", "Album", "Non-existent", jfName);

            if (shouldMatch)
            {
                Assert.True(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Theory]
        [ClassData(typeof(AnyArtistNoParensDataMatch))]
        [ClassData(typeof(AnyArtistNoParensDataNoMatch))]
        public void AlbumArtist_NoParens(string jfName, string provName, bool shouldMatch)
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "aaaaaaa", provName, "bbbbbbb" }, new List<string> { "Non-existent", "Artist 2" });
            var jf = TrackHelper.CreateJfItem("Track", "Album", jfName, "Non-existent");

            if (shouldMatch)
            {
                Assert.True(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Theory]
        // fuzzy matching for artists is disabled, should match the same things as NoParens
        [ClassData(typeof(AnyArtistNoParensDataMatch))]
        [ClassData(typeof(AnyArtistNoParensDataNoMatch))]
        public void Artist_Fuzzy(string jfName, string provName, bool shouldMatch)
        {
            TrackHelper.SetValidPluginInstance();
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Non-existent", "Artist 2" }, new List<string> { "aaaaaaa", provName, "bbbbbbb" });
            var jf = TrackHelper.CreateJfItem("Track", "Album", "Non-existent", jfName);

            if (shouldMatch)
            {
                Assert.True(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.Fuzzy), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.Fuzzy), TrackHelper.GetErrorString(jf));
            }
        }

        [Theory]
        // fuzzy matching for artists is disabled, should match the same things as NoParens
        [ClassData(typeof(AnyArtistNoParensDataMatch))]
        [ClassData(typeof(AnyArtistNoParensDataNoMatch))]
        public void AlbumArtist_Fuzzy(string jfName, string provName, bool shouldMatch)
        {
            TrackHelper.SetValidPluginInstance();
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "aaaaaaa", provName, "bbbbbbb" }, new List<string> { "Non-existent", "Artist 2" });
            var jf = TrackHelper.CreateJfItem("Track", "Album", jfName, "Non-existent");

            if (shouldMatch)
            {
                Assert.True(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.Fuzzy), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.Fuzzy), TrackHelper.GetErrorString(jf));
            }
        }

        class AnyArtistNoParensDataMatch : AnyArtistNoPunctuationDataMatch
        {
            public IEnumerable<object[]> GetNoParens()
            {
                yield return new object[] { "Just Artist (abc)", "Just Artist", true };
                yield return new object[] { "(asdkas) just artist", "Just Artist", true };
                yield return new object[] { "Just A (b) rtist", "Just Artist", true };
                yield return new object[] { "a (b) (Just Artist)", "Just Artist", true };
                yield return new object[] { "a (b) [c] (Just Artist)", "Just Artist", true };
                yield return new object[] { "a (b) [c)] (Just Artist)", "Just Artist", true };
                yield return new object[] { "just artist [123]", "Just Artist", true };
            }

            public override IEnumerator<object[]> GetEnumerator()
            {
                foreach (var obj in GetDefault())
                {
                    yield return obj;
                }
                foreach (var obj in GetCaseInsensitive())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoPunctuation())
                {
                    yield return obj;
                }
                foreach (var obj in GetNoParens())
                {
                    yield return obj;
                }
            }
        }
        class AnyArtistNoParensDataNoMatch : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { "just Artis.", "Just Artist", false };
                yield return new object[] { ".$Just Artist", "Just Artist", false };
                yield return new object[] { "Just Art`ist", "Just Artist", false };
                yield return new object[] { "Just Arti0st", "Just Artist", false };
                yield return new object[] { "Just Artistb (x)", "Just Artist", false };
                yield return new object[] { "foo (alb) (um)", "Just Artist", false };
                yield return new object[] { "Just Artist (hello)", "Just Artist - hello (foo)", false };
                yield return new object[] { "Banda and Whate&ver", "Banda & Whate ver", false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void AlbumArtist_Ignores_Other_Fields()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "No one" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem(null, null, "Artist On Album", null),
                TrackHelper.CreateJfItem(null, null, "No one", "Just Artist"),
                TrackHelper.CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "No one", null),
            };

            foreach (var level in Enum.GetValues<ItemMatchLevel>())
            {
                foreach (var jf in items)
                {
                    Assert.True(TrackComparison.AlbumArtistOneContained(jf, prov, level), TrackHelper.GetErrorString(jf));
                }
            }

            items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", null, "Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, null),
            };

            foreach (var level in Enum.GetValues<ItemMatchLevel>())
            {
                foreach (var jf in items)
                {
                    Assert.False(TrackComparison.AlbumArtistOneContained(jf, prov, level), TrackHelper.GetErrorString(jf));
                }
            }
        }

        [Fact]
        public void Artist_Ignores_Other_Fields()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist", "Artist 2" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem(null, null, null, "Just Artist"),
                TrackHelper.CreateJfItem(null, null, "Artist On Album", "Artist 2"),
                TrackHelper.CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", null, "Artist 2"),
            };

            foreach (var level in Enum.GetValues<ItemMatchLevel>())
            {
                foreach (var jf in items)
                {
                    Assert.True(TrackComparison.ArtistOneContained(jf, prov, level), TrackHelper.GetErrorString(jf));
                }
            }

            items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", null),
                TrackHelper.CreateJfItem(null, null, null, null),
            };

            foreach (var level in Enum.GetValues<ItemMatchLevel>())
            {
                foreach (var jf in items)
                {
                    Assert.False(TrackComparison.ArtistOneContained(jf, prov, level), TrackHelper.GetErrorString(jf));
                }
            }
        }
    }
}
