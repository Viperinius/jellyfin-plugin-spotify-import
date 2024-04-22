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
    public class TrackComparisonTests : IDisposable
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

            if (shouldMatch)
            {
                Assert.True(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
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

            if (shouldMatch)
            {
                Assert.True(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
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

            if (shouldMatch)
            {
                Assert.True(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
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

            if (shouldMatch)
            {
                Assert.True(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCase), TrackHelper.GetErrorString(jf));
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
                    Assert.True(TrackComparison.TrackNameEqual(jf, prov, level), TrackHelper.GetErrorString(jf));
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
                    Assert.False(TrackComparison.TrackNameEqual(jf, prov, level), TrackHelper.GetErrorString(jf));
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

            if (shouldMatch)
            {
                Assert.True(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
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

            if (shouldMatch)
            {
                Assert.True(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
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

            if (shouldMatch)
            {
                Assert.True(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
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

            if (shouldMatch)
            {
                Assert.True(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
            else
            {
                Assert.False(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreParensPunctuationAndCase), TrackHelper.GetErrorString(jf));
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
                    Assert.True(TrackComparison.AlbumNameEqual(jf, prov, level), TrackHelper.GetErrorString(jf));
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
                    Assert.False(TrackComparison.AlbumNameEqual(jf, prov, level), TrackHelper.GetErrorString(jf));
                }
            }
        }

        [Fact]
        public void AlbumArtist_Matches_Default()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "No one" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "No one", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, "Artist On Album", null),
                TrackHelper.CreateJfItem(null, null, "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", null),
                TrackHelper.CreateJfItem("track", "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
                TrackHelper.CreateJfItem("Track", "Album", "No one", "Just artist"),
                TrackHelper.CreateJfItem("Track", "Album", "a (Artist On Album) z", "Just artist"),
                TrackHelper.CreateJfItem("Track", "Album", "(No one) (ok)", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_NoMatches_Default()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "No one" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem(null, "Album", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, "Album", "no one", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, null),
                TrackHelper.CreateJfItem("Track", "Album", ".Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On' Album", null),
                TrackHelper.CreateJfItem("Track", "Album", "artist Album", null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_Matches_CaseInsensitive()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "No one" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "no one", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, "Artist on Album", null),
                TrackHelper.CreateJfItem(null, null, "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, "Album", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Album", null),
                TrackHelper.CreateJfItem("track", "Album", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "album", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
                TrackHelper.CreateJfItem("Track", "Album", "no ONE", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_NoMatches_CaseInsensitive()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "No one" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", null, "Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, null),
                TrackHelper.CreateJfItem("track.", "album.", "Artist on. Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, ".Artist On Album", null),
                TrackHelper.CreateJfItem(null, null, ".No one", null),
                TrackHelper.CreateJfItem(null, null, "Artist On' Album", null),
                TrackHelper.CreateJfItem(null, null, "Artist O####n Album", null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_Matches_NoPunctuation()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "No one" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "album", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, ".Artist on Album", null),
                TrackHelper.CreateJfItem(null, null, "Artist on, Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, "Artist o###n Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", null, "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist O!n Album", null),
                TrackHelper.CreateJfItem("track", "Album", "Artist O-n Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Al&bum", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
                TrackHelper.CreateJfItem("Track", "Album", "No on!e", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_NoMatches_NoPunctuation()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "No one" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", null, "Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, null),
                TrackHelper.CreateJfItem("Track", "Album", "Artist .lbum", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, ".$Artist On Album", null),
                TrackHelper.CreateJfItem(null, null, "Artist`On Album", null),
                TrackHelper.CreateJfItem(null, null, "Artist0On Album", null),
                TrackHelper.CreateJfItem(null, null, "Artist On Album (y)", null),
                TrackHelper.CreateJfItem(null, null, "No. One(Two)", null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumArtistOneContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_Matches_Default()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist", "Artist 2" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, "Just Artist"),
                TrackHelper.CreateJfItem(null, null, "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", null, "Just Artist"),
                TrackHelper.CreateJfItem("track", "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Album", "Artist 2"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Album", "ABC (Artist 2)"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Album", "%&% (Just Artist) ((("),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_NoMatches_Default()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist", "Artist 2" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem(null, "Album", "Artist On Album", "Just artist"),
                TrackHelper.CreateJfItem(null, null, null, null),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", ".Just Artist"),
                TrackHelper.CreateJfItem("Track", null, null, "Just 'Artist"),
                TrackHelper.CreateJfItem("Track", null, null, "Jus artist"),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_Matches_CaseInsensitive()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist", "Artist 2" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
                TrackHelper.CreateJfItem(null, null, null, "Just artist"),
                TrackHelper.CreateJfItem(null, "Album", null, "Just artist"),
                TrackHelper.CreateJfItem(null, "Album", "Artist On Album", "Just artist"),
                TrackHelper.CreateJfItem("Track", "Album", null, "Just artist"),
                TrackHelper.CreateJfItem("Track", null, "Artist On Album", "Just artist"),
                TrackHelper.CreateJfItem("track", "Album", "Artist On Album", "Just artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Album", "Just artist"),
                TrackHelper.CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "album", "Artist On Album", "Artist 2"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_NoMatches_CaseInsensitive()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist", "Artist 2" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", null),
                TrackHelper.CreateJfItem(null, null, null, null),
                TrackHelper.CreateJfItem("track.", "album.", "Artist On Album", "Just artist."),
                TrackHelper.CreateJfItem(null, null, null, ".Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, "Just 'Artist"),
                TrackHelper.CreateJfItem(null, null, null, "Just ###Artist"),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_Matches_NoPunctuation()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist", "Artist 2" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
                TrackHelper.CreateJfItem(null, null, null, ".Just artist"),
                TrackHelper.CreateJfItem(null, null, null, "Just ,artist"),
                TrackHelper.CreateJfItem(null, null, "Artist On Album", "Just### artist"),
                TrackHelper.CreateJfItem("Track", "Album", null, "Just artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just !Artist"),
                TrackHelper.CreateJfItem("track", "Album", "Artist On Album", "Just- Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Album", "Just& artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Artist 2"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Artist 2!"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_NoMatches_NoPunctuation()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist", "Artist 2" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", null),
                TrackHelper.CreateJfItem(null, null, null, null),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just .rtist"),
                TrackHelper.CreateJfItem(null, null, null, ".$Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, "Just `Artist"),
                TrackHelper.CreateJfItem(null, null, null, "Just0 Artist"),
                TrackHelper.CreateJfItem(null, null, null, "Just Artist (z)"),
                TrackHelper.CreateJfItem(null, null, null, "Artist 2 (z)"),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.ArtistOneContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
        }
    }
}
