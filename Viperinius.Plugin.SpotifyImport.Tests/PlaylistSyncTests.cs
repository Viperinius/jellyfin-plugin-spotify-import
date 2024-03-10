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

        public MediaBrowser.Controller.Entities.Audio.Audio? WrapGetMatchingTrack(ProviderTrackInfo trackInfo, out ItemMatchCriteria failedCriterium)
        {
            return GetMatchingTrack(trackInfo, out failedCriterium);
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

        private void SetUpLibManagerMock(MediaBrowser.Controller.Library.ILibraryManager libManagerMock, MediaBrowser.Controller.Entities.BaseItem? item)
        {
            var list = new List<(MediaBrowser.Controller.Entities.BaseItem, MediaBrowser.Model.Dto.ItemCounts)>();
            if (item != null)
            {
                list.Add((item, new MediaBrowser.Model.Dto.ItemCounts()));
            }

            libManagerMock
                .GetArtists(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => new MediaBrowser.Model.Querying.QueryResult<(MediaBrowser.Controller.Entities.BaseItem, MediaBrowser.Model.Dto.ItemCounts)>(list));
        }

        private string GetErrorString(ItemMatchCriteria actual, ItemMatchCriteria? expected)
        {
            return $"ItemMatchCriteria -> Actual: {actual}; Expected: {expected}";
        }

        private void CheckItem(
            bool shouldMatch,
            ProviderTrackInfo prov,
            MediaBrowser.Controller.Entities.Audio.Audio audio,
            MediaBrowser.Controller.Entities.Audio.MusicAlbum album,
            MediaBrowser.Controller.Entities.Audio.MusicArtist artist,
            ItemMatchCriteria? expectedFailedCriteria = null)
        {
            var loggerMock = Substitute.For<ILogger<PlaylistSync>>();
            var plManagerMock = Substitute.For<MediaBrowser.Controller.Playlists.IPlaylistManager>();
            var userManagerMock = Substitute.For<MediaBrowser.Controller.Library.IUserManager>();
            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            SetUpLibManagerMock(libManagerMock, artist);
            var wrapper = new PlaylistSyncWrapper(loggerMock, plManagerMock, libManagerMock, userManagerMock, new List<ProviderPlaylistInfo>(), new Dictionary<string, string>());

            ItemMatchCriteria failedCrit;
            if (shouldMatch)
            {
                Assert.True(wrapper.WrapGetMatchingTrack(prov, out failedCrit) == audio, $"{TrackHelper.GetErrorString(audio)}\n{AlbumHelper.GetErrorString(album)}\n{ArtistHelper.GetErrorString(artist)}");
            }
            else
            {
                Assert.True(wrapper.WrapGetMatchingTrack(prov, out failedCrit) == null, $"{TrackHelper.GetErrorString(audio)}\n{AlbumHelper.GetErrorString(album)}\n{ArtistHelper.GetErrorString(artist)}");
            }

            if (expectedFailedCriteria != null)
            {
                Assert.True(
                    shouldMatch ? failedCrit == ItemMatchCriteria.None : failedCrit.HasFlag(expectedFailedCriteria),
                    GetErrorString(failedCrit, expectedFailedCriteria));
            }
            else
            {
                Assert.True(shouldMatch ? failedCrit == ItemMatchCriteria.None : failedCrit != ItemMatchCriteria.None);
            }
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
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Artist 2")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Albtist", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("track", "album", "artist On Album", "just Artist")),
                (false, TrackHelper.CreateAllJfItems("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
                (false, TrackHelper.CreateAllJfItems("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st (CD) Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist);
            }
        }
        
        [Fact]
        public void TrackMatching_Respects_Level_IgnoreCase()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnoreCase;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("track", "album", "artist On Album", "just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "albtist", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
                (false, TrackHelper.CreateAllJfItems("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st (CD) Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Level_IgnoreCasePunctuation()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnorePunctuationAndCase;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("track", "album", "artist On Album", "just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "ALB-TIST", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
                (false, TrackHelper.CreateAllJfItems("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st (CD) Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Level_IgnoreCasePunctuationParens()
        {
            SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnoreParensPunctuationAndCase;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("track", "album", "artist On Album", "just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "al.btist(b)", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
                (true, TrackHelper.CreateAllJfItems("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st Artist(CD)")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_TrackName()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("track", "Album", "Artist On Album", "Just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist, ItemMatchCriteria.TrackName);
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("track", "Album", "Artist On Album", "Just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist, ItemMatchCriteria.TrackName);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_Album()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("Track", "album", "Artist On Album", "Just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist, ItemMatchCriteria.AlbumName);
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "album", "Artist On Album", "Just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist, ItemMatchCriteria.AlbumName);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_Artist()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Artist 2")),
                (false, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist, ItemMatchCriteria.Artists);
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.AlbumName;
            jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Artist 2")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist, ItemMatchCriteria.Artists);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_AlbumArtist()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Albtist", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("Track", "Album", "artist On Album", "Just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist, ItemMatchCriteria.AlbumArtists);
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, (MediaBrowser.Controller.Entities.Audio.Audio Track, MediaBrowser.Controller.Entities.Audio.MusicAlbum Album, MediaBrowser.Controller.Entities.Audio.MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Albtist", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "artist On Album", "Just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist, ItemMatchCriteria.AlbumArtists);
            }
        }
    }
}
