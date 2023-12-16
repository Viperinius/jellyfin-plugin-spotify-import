using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
            List<ProviderPlaylistInfo> playlists,
            Dictionary<string, string> userPlaylistIds)
            : base(logger, playlistManager, libraryManager, userManager, playlists, userPlaylistIds)
        {
        }

        public static bool WrapItemMatchesTrackInfo(MediaBrowser.Controller.Entities.Audio.Audio audioItem, ProviderTrackInfo trackInfo, out ItemMatchCriteria failedCriterium)
        {
            return ItemMatchesTrackInfo(audioItem, trackInfo, out failedCriterium);
        }
    }

    public class PlaylistSyncTests : IDisposable
    {
        private void SetValidPluginInstance()
        {
            if (Plugin.Instance == null)
            {
                var mockAppPaths = Substitute.For<MediaBrowser.Common.Configuration.IApplicationPaths>();
                mockAppPaths.PluginsPath.Returns(string.Empty);
                mockAppPaths.PluginConfigurationsPath.Returns(string.Empty);
                var mockXmlSerializer = Substitute.For<MediaBrowser.Model.Serialization.IXmlSerializer>();
                mockXmlSerializer.DeserializeFromFile(Arg.Any<Type>(), Arg.Any<string>())
                                 .Returns(_ => new Configuration.PluginConfiguration());

                _ = new Plugin(mockAppPaths, mockXmlSerializer);
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
                (false, TrackHelper.CreateJfItem("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st (CD) Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                Assert.True(item.IsMatch ? failedCrit == ItemMatchCriteria.None : failedCrit != ItemMatchCriteria.None);
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
                (false, TrackHelper.CreateJfItem("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st (CD) Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                Assert.True(item.IsMatch ? failedCrit == ItemMatchCriteria.None : failedCrit != ItemMatchCriteria.None);
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
                (false, TrackHelper.CreateJfItem("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st (CD) Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                Assert.True(item.IsMatch ? failedCrit == ItemMatchCriteria.None : failedCrit != ItemMatchCriteria.None);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Level_IgnoreCasePunctuationParens()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnoreParensPunctuationAndCase;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("track", "album", "artist On Album", "just Artist")),
                (true, TrackHelper.CreateJfItem("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
                (true, TrackHelper.CreateJfItem("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st Artist(CD)")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                Assert.True(item.IsMatch ? failedCrit == ItemMatchCriteria.None : failedCrit != ItemMatchCriteria.None);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_TrackName()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateJfItem("track", "Album", "Artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                if (item.IsMatch)
                {
                    Assert.True(failedCrit == ItemMatchCriteria.None);
                }
                else
                {
                    Assert.True(failedCrit == ItemMatchCriteria.TrackName);
                }
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("track", "Album", "Artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                if (item.IsMatch)
                {
                    Assert.True(failedCrit == ItemMatchCriteria.None);
                }
                else
                {
                    Assert.True(failedCrit == ItemMatchCriteria.TrackName);
                }
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_Album()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateJfItem("Track", "album", "Artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                if (item.IsMatch)
                {
                    Assert.True(failedCrit == ItemMatchCriteria.None);
                }
                else
                {
                    Assert.True(failedCrit == ItemMatchCriteria.AlbumName);
                }
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("Track", "album", "Artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                if (item.IsMatch)
                {
                    Assert.True(failedCrit == ItemMatchCriteria.None);
                }
                else
                {
                    Assert.True(failedCrit == ItemMatchCriteria.AlbumName);
                }
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_Artist()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                if (item.IsMatch)
                {
                    Assert.True(failedCrit == ItemMatchCriteria.None);
                }
                else
                {
                    Assert.True(failedCrit == ItemMatchCriteria.Artists);
                }
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.AlbumName;
            jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                if (item.IsMatch)
                {
                    Assert.True(failedCrit == ItemMatchCriteria.None);
                }
                else
                {
                    Assert.True(failedCrit == ItemMatchCriteria.Artists);
                }
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_AlbumArtist()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", "Artist On Album", "Just Artist");
            var jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateJfItem("Track", "Album", "artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                if (item.IsMatch)
                {
                    Assert.True(failedCrit == ItemMatchCriteria.None);
                }
                else
                {
                    Assert.True(failedCrit == ItemMatchCriteria.AlbumArtists);
                }
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, MediaBrowser.Controller.Entities.Audio.Audio Item)>
            {
                (true, TrackHelper.CreateJfItem("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateJfItem("Track", "Album", "artist On Album", "Just Artist")),
            };

            foreach (var item in jfItems)
            {
                Assert.True(PlaylistSyncWrapper.WrapItemMatchesTrackInfo(item.Item, prov, out var failedCrit) == item.IsMatch, TrackHelper.GetErrorString(item.Item));
                if (item.IsMatch)
                {
                    Assert.True(failedCrit == ItemMatchCriteria.None);
                }
                else
                {
                    Assert.True(failedCrit == ItemMatchCriteria.AlbumArtists);
                }
            }
        }
    }
}
