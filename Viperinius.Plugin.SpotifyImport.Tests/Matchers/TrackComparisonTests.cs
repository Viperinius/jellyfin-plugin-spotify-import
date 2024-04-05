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

        [Fact]
        public void Album_Matches_Default()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, "Album", null, null),
                TrackHelper.CreateJfItem(null, "Album", null, "Just Artist"),
                TrackHelper.CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", null, "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", null),
                TrackHelper.CreateJfItem("track", "Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
                TrackHelper.CreateJfItem("Track", "(Album) here", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_NoMatches_Default()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem(null, "album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, null),
                TrackHelper.CreateJfItem("Track", ".Album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Al'bum", null, null),
                TrackHelper.CreateJfItem("Track", "albu", null, null),
                TrackHelper.CreateJfItem("Track", "(Al)(bum) here", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.Default), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_Matches_CaseInsensitive()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, "album", null, null),
                TrackHelper.CreateJfItem(null, "album", null, "Just Artist"),
                TrackHelper.CreateJfItem(null, "album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "album", null, "Just Artist"),
                TrackHelper.CreateJfItem("Track", "album", "Artist On Album", null),
                TrackHelper.CreateJfItem("track", "album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "album", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_NoMatches_CaseInsensitive()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, null),
                TrackHelper.CreateJfItem("track.", "album.", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, ".Album", null, null),
                TrackHelper.CreateJfItem(null, "Al'bum", null, null),
                TrackHelper.CreateJfItem(null, "Al####bum", null, null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_Matches_NoPunctuation()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, ".album", null, null),
                TrackHelper.CreateJfItem(null, "al,bum", null, "Just Artist"),
                TrackHelper.CreateJfItem(null, "al###bum", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "album", null, "Just Artist"),
                TrackHelper.CreateJfItem("Track", "alb!um", "Artist On Album", null),
                TrackHelper.CreateJfItem("track", "a-lbum", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "albu&m", "Artist on Album", "Just Artist"),
                TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_NoMatches_NoPunctuation()
        {
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album" }, new List<string> { "Just Artist" });
            var items = new List<Audio>
            {
                TrackHelper.CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, null, null, null),
                TrackHelper.CreateJfItem("Track", "albu.", "Artist On Album", "Just Artist"),
                TrackHelper.CreateJfItem(null, ".$Album", null, null),
                TrackHelper.CreateJfItem(null, "Al`bum", null, null),
                TrackHelper.CreateJfItem(null, "Al0bum", null, null),
                TrackHelper.CreateJfItem("Track", "Album (x)", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), TrackHelper.GetErrorString(jf));
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
