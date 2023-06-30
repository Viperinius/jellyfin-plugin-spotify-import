using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Viperinius.Plugin.SpotifyImport.Tests.TestHelpers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests
{
    internal class PlaylistSyncWrapper : PlaylistSync
    {
        public PlaylistSyncWrapper(
            ILogger<PlaylistSync> logger,
            MediaBrowser.Controller.Playlists.IPlaylistManager playlistManager,
            MediaBrowser.Controller.Library.ILibraryManager libraryManager,
            MediaBrowser.Controller.Library.IUserManager userManager,
            List<ProviderPlaylistInfo> playlists)
            : base(logger, playlistManager, libraryManager, userManager, playlists)
        {
        }

        public static bool WrapItemMatchesTrackInfo(MediaBrowser.Controller.Entities.Audio.Audio audioItem, ProviderTrackInfo trackInfo)
        {
            return ItemMatchesTrackInfo(audioItem, trackInfo);
        }
    }

    public class PlaylistSyncTests : IDisposable
    {
        private void SetValidPluginInstance()
        {
            if (Plugin.Instance == null)
            {
                var mockAppPaths = new Mock<MediaBrowser.Common.Configuration.IApplicationPaths>();
                mockAppPaths.SetupGet(m => m.PluginsPath).Returns(() => string.Empty);
                mockAppPaths.SetupGet(m => m.PluginConfigurationsPath).Returns(() => string.Empty);
                var mockXmlSerializer = new Mock<MediaBrowser.Model.Serialization.IXmlSerializer>();
                mockXmlSerializer.Setup(m => m.DeserializeFromFile(It.IsAny<Type>(), It.IsAny<string>()))
                                 .Returns(() => new Configuration.PluginConfiguration());

                _ = new Plugin(mockAppPaths.Object, mockXmlSerializer.Object);
            }
            System.Threading.Thread.Sleep(100);
        }

        public void Dispose()
        {
            TrackHelper.ClearAlbums();
        }

        [Fact]
        public void TrackMatching_Respects_Level_Default()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateJfItem("track", "album", "artist On Album", "just Artist")),
                (false, TrackHelper.CreateJfItem("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }
        }

        [Fact]
        public void TrackMatching_Respects_Level_IgnoreCase()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnoreCase;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("track", "album", "artist On Album", "just Artist")),
                (false, TrackHelper.CreateJfItem("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }
        }

        [Fact]
        public void TrackMatching_Respects_Level_IgnoreCasePunctuation()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnorePunctuationAndCase;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("track", "album", "artist On Album", "just Artist")),
                (true, TrackHelper.CreateJfItem("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_TrackName()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateJfItem("track", "Album", "Artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("track", "Album", "Artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_Album()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateJfItem("Track", "album", "Artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("Track", "album", "Artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_Artist()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.AlbumName;
            jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_AlbumArtist()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateJfItem("Track", "Album", "artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("Track", "Album", "artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
            }
        }
    }
}
