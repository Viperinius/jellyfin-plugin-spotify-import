using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Moq;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Matchers
{
    public class TrackComparisonTests
    {
        private readonly Dictionary<Guid, MusicAlbum> _albums;
        private readonly Mock<ILibraryManager> _libManagerMock;

        public TrackComparisonTests()
        {
            _albums = new Dictionary<Guid, MusicAlbum>();
            _libManagerMock = new Mock<ILibraryManager>();
#pragma warning disable CS8603 // null return
            _libManagerMock.Setup(m => m.GetItemById(It.IsAny<Guid>())).Returns((Guid guid) => _albums.ContainsKey(guid) ? _albums[guid] : null);
#pragma warning restore CS8603 // null return
            BaseItem.LibraryManager = _libManagerMock.Object;
        }

        private Audio CreateJfItem(string? trackName, string? albumName, string? albumArtist, string? artist)
        {
            var audio = new Audio()
            {
                Name = trackName
            };

            if (albumName != null || albumArtist != null)
            {
                var album = new MusicAlbum();
                album.Name = albumName;
                if (albumArtist != null)
                {
                    album.Artists = new List<string> { "abc", albumArtist, "lius987grvalsiuRHH" };
                }

                var albumId = Guid.NewGuid();
                audio.ParentId = albumId;
                _albums.Add(albumId, album);

                var bla = audio.GetParent();
            }

            if (artist != null)
            {
                audio.Artists = new List<string> { "abc", artist, "dwoeirg87fadaDUG$ASD" };
            }

            return audio;
        }

        private ProviderTrackInfo CreateProviderItem(string trackName, string albumName, string albumArtist, string artist)
        {
            return new ProviderTrackInfo()
            {
                Name = trackName,
                AlbumName = albumName,
                AlbumArtistName = albumArtist,
                ArtistName = artist
            };
        }

        private string GetErrorString(Audio audio)
        {
            return $"at Audio{{Name='{audio.Name}',Album='{audio.AlbumEntity?.Name}',AlbumArtist=[{audio.AlbumEntity?.Artists}],Artists=[{audio.Artists}]}}";
        }

        [Fact]
        public void Track_Matches_Default()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", null, null, null),
                CreateJfItem("Track", null, null, "Just Artist"),
                CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "Album", null, "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", null),
                CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist on Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.Default), GetErrorString(jf));
            }
        }

        [Fact]
        public void Track_NoMatches_Default()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                CreateJfItem(null, null, null, null),
                CreateJfItem("track", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem(".Track", null, null, null),
                CreateJfItem("Tr'ack", null, null, null),
                CreateJfItem("Tr####ack", null, null, null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.Default), GetErrorString(jf));
            }
        }

        [Fact]
        public void Track_Matches_CaseInsensitive()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("track", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem("track", null, null, null),
                CreateJfItem("track", null, null, "Just Artist"),
                CreateJfItem("track", null, "Artist On Album", "Just Artist"),
                CreateJfItem("track", "Album", null, "Just Artist"),
                CreateJfItem("track", "Album", "Artist On Album", null),
                CreateJfItem("track", "album", "Artist On Album", "Just Artist"),
                CreateJfItem("track", "Album", "Artist on Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnoreCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Track_NoMatches_CaseInsensitive()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                CreateJfItem(null, null, null, null),
                CreateJfItem("track.", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem(".Track", null, null, null),
                CreateJfItem("Tr'ack", null, null, null),
                CreateJfItem("Tr####ack", null, null, null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnoreCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Track_Matches_NoPunctuation()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("track", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem(".track", null, null, null),
                CreateJfItem("tr,ack", null, null, "Just Artist"),
                CreateJfItem("tr###ack", null, "Artist On Album", "Just Artist"),
                CreateJfItem("track", "Album", null, "Just Artist"),
                CreateJfItem("trac!k", "Album", "Artist On Album", null),
                CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                CreateJfItem("tr-ack", "Album", "Artist on Album", "Just Artist"),
                CreateJfItem("tr&ack", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Track_NoMatches_NoPunctuation()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                CreateJfItem(null, null, null, null),
                CreateJfItem("trac.", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem(".$Track", null, null, null),
                CreateJfItem("Tr`ack", null, null, null),
                CreateJfItem("Tr0ack", null, null, null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.TrackNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_Matches_Default()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem(null, "Album", null, null),
                CreateJfItem(null, "Album", null, "Just Artist"),
                CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "Album", null, "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", null),
                CreateJfItem("track", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist on Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.Default), GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_NoMatches_Default()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem(null, "album", "Artist On Album", "Just Artist"),
                CreateJfItem(null, null, null, null),
                CreateJfItem("Track", ".Album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "Al'bum", null, null),
                CreateJfItem("Track", "albu", null, null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.Default), GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_Matches_CaseInsensitive()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                CreateJfItem(null, "album", null, null),
                CreateJfItem(null, "album", null, "Just Artist"),
                CreateJfItem(null, "album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "album", null, "Just Artist"),
                CreateJfItem("Track", "album", "Artist On Album", null),
                CreateJfItem("track", "album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "album", "Artist on Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_NoMatches_CaseInsensitive()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                CreateJfItem(null, null, null, null),
                CreateJfItem("track.", "album.", "Artist On Album", "Just Artist"),
                CreateJfItem(null, ".Album", null, null),
                CreateJfItem(null, "Al'bum", null, null),
                CreateJfItem(null, "Al####bum", null, null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnoreCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_Matches_NoPunctuation()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                CreateJfItem(null, ".album", null, null),
                CreateJfItem(null, "al,bum", null, "Just Artist"),
                CreateJfItem(null, "al###bum", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "album", null, "Just Artist"),
                CreateJfItem("Track", "alb!um", "Artist On Album", null),
                CreateJfItem("track", "a-lbum", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "albu&m", "Artist on Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Album_NoMatches_NoPunctuation()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                CreateJfItem(null, null, null, null),
                CreateJfItem("Track", "albu.", "Artist On Album", "Just Artist"),
                CreateJfItem(null, ".$Album", null, null),
                CreateJfItem(null, "Al`bum", null, null),
                CreateJfItem(null, "Al0bum", null, null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumNameEqual(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_Matches_Default()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem(null, null, "Artist On Album", null),
                CreateJfItem(null, null, "Artist On Album", "Just Artist"),
                CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", null),
                CreateJfItem("track", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumArtistContained(jf, prov, ItemMatchLevel.Default), GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_NoMatches_Default()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem(null, "Album", "Artist on Album", "Just Artist"),
                CreateJfItem(null, null, null, null),
                CreateJfItem("Track", "Album", ".Artist On Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On' Album", null),
                CreateJfItem("Track", "Album", "artist Album", null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumArtistContained(jf, prov, ItemMatchLevel.Default), GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_Matches_CaseInsensitive()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", "Artist on Album", "Just Artist"),
                CreateJfItem(null, null, "Artist on Album", null),
                CreateJfItem(null, null, "Artist on Album", "Just Artist"),
                CreateJfItem(null, "Album", "Artist on Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist on Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist on Album", null),
                CreateJfItem("track", "Album", "Artist on Album", "Just Artist"),
                CreateJfItem("Track", "album", "Artist on Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumArtistContained(jf, prov, ItemMatchLevel.IgnoreCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_NoMatches_CaseInsensitive()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", null, "Just Artist"),
                CreateJfItem(null, null, null, null),
                CreateJfItem("track.", "album.", "Artist on. Album", "Just Artist"),
                CreateJfItem(null, null, ".Artist On Album", null),
                CreateJfItem(null, null, "Artist On' Album", null),
                CreateJfItem(null, null, "Artist O####n Album", null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumArtistContained(jf, prov, ItemMatchLevel.IgnoreCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_Matches_NoPunctuation()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "album", "Artist on Album", "Just Artist"),
                CreateJfItem(null, null, ".Artist on Album", null),
                CreateJfItem(null, null, "Artist on, Album", "Just Artist"),
                CreateJfItem(null, null, "Artist o###n Album", "Just Artist"),
                CreateJfItem("Track", null, "Artist on Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist O!n Album", null),
                CreateJfItem("track", "Album", "Artist O-n Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist on Al&bum", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.AlbumArtistContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void AlbumArtist_NoMatches_NoPunctuation()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", null, "Just Artist"),
                CreateJfItem(null, null, null, null),
                CreateJfItem("Track", "Album", "Artist .lbum", "Just Artist"),
                CreateJfItem(null, null, ".$Artist On Album", null),
                CreateJfItem(null, null, "Artist`On Album", null),
                CreateJfItem(null, null, "Artist0On Album", null),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.AlbumArtistContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_Matches_Default()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem(null, null, null, "Just Artist"),
                CreateJfItem(null, null, "Artist On Album", "Just Artist"),
                CreateJfItem(null, "Album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", null, "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "Album", null, "Just Artist"),
                CreateJfItem("track", "Album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
                CreateJfItem("Track", "Album", "Artist on Album", "Just Artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.ArtistContained(jf, prov, ItemMatchLevel.Default), GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_NoMatches_Default()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem(null, "Album", "Artist On Album", "Just artist"),
                CreateJfItem(null, null, null, null),
                CreateJfItem("Track", "Album", "Artist On Album", ".Just Artist"),
                CreateJfItem("Track", null, null, "Just 'Artist"),
                CreateJfItem("Track", null, null, "Jus artist"),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.ArtistContained(jf, prov, ItemMatchLevel.Default), GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_Matches_CaseInsensitive()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
                CreateJfItem(null, null, null, "Just artist"),
                CreateJfItem(null, "Album", null, "Just artist"),
                CreateJfItem(null, "Album", "Artist On Album", "Just artist"),
                CreateJfItem("Track", "Album", null, "Just artist"),
                CreateJfItem("Track", null, "Artist On Album", "Just artist"),
                CreateJfItem("track", "Album", "Artist On Album", "Just artist"),
                CreateJfItem("Track", "Album", "Artist on Album", "Just artist"),
                CreateJfItem("Track", "album", "Artist On Album", "Just Artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.ArtistContained(jf, prov, ItemMatchLevel.IgnoreCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_NoMatches_CaseInsensitive()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", "Artist On Album", null),
                CreateJfItem(null, null, null, null),
                CreateJfItem("track.", "album.", "Artist On Album", "Just artist."),
                CreateJfItem(null, null, null, ".Just Artist"),
                CreateJfItem(null, null, null, "Just 'Artist"),
                CreateJfItem(null, null, null, "Just ###Artist"),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.ArtistContained(jf, prov, ItemMatchLevel.IgnoreCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_Matches_NoPunctuation()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", "Artist On Album", "Just artist"),
                CreateJfItem(null, null, null, ".Just artist"),
                CreateJfItem(null, null, null, "Just ,artist"),
                CreateJfItem(null, null, "Artist On Album", "Just### artist"),
                CreateJfItem("Track", "Album", null, "Just artist"),
                CreateJfItem("Track", "Album", "Artist On Album", "Just !Artist"),
                CreateJfItem("track", "Album", "Artist On Album", "Just- Artist"),
                CreateJfItem("Track", "Album", "Artist on Album", "Just& artist"),
                CreateJfItem("Track", "Album", "Artist On Album", "Just Artist"),
            };

            foreach (var jf in items)
            {
                Assert.True(TrackComparison.ArtistContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), GetErrorString(jf));
            }
        }

        [Fact]
        public void Artist_NoMatches_NoPunctuation()
        {
            var prov = CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var items = new List<Audio>
            {
                CreateJfItem("Track", "Album", "Artist On Album", null),
                CreateJfItem(null, null, null, null),
                CreateJfItem("Track", "Album", "Artist On Album", "Just .rtist"),
                CreateJfItem(null, null, null, ".$Just Artist"),
                CreateJfItem(null, null, null, "Just `Artist"),
                CreateJfItem(null, null, null, "Just0 Artist"),
            };

            foreach (var jf in items)
            {
                Assert.False(TrackComparison.ArtistContained(jf, prov, ItemMatchLevel.IgnorePunctuationAndCase), GetErrorString(jf));
            }
        }
    }
}
