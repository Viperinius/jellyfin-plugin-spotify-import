using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;
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
            Dictionary<string, string> userPlaylistIds,
            ManualMapStore manualMapStore)
            : base(logger, playlistManager, libraryManager, userManager, playlists, userPlaylistIds, manualMapStore)
        {
        }

        public Audio? WrapGetMatchingTrack(ProviderTrackInfo trackInfo, out ItemMatchCriteria failedCriterium)
        {
            return GetMatchingTrack(trackInfo, out failedCriterium);
        }
    }

    public class PlaylistSyncTests : IDisposable
    {
        private void SetUpLibManagerMock(MediaBrowser.Controller.Library.ILibraryManager libManagerMock, List<MediaBrowser.Controller.Entities.BaseItem> items)
        {
            var list = new List<(MediaBrowser.Controller.Entities.BaseItem, MediaBrowser.Model.Dto.ItemCounts)>();
            foreach (var item in items)
            {
                list.Add((item, new MediaBrowser.Model.Dto.ItemCounts()));
            }

            libManagerMock
                .GetArtists(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => new MediaBrowser.Model.Querying.QueryResult<(MediaBrowser.Controller.Entities.BaseItem, MediaBrowser.Model.Dto.ItemCounts)>(list));
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
            Audio audio,
            MusicAlbum album,
            MusicArtist artist,
            ItemMatchCriteria? expectedFailedCriteria = null)
        {
            var loggerMock = Substitute.For<ILogger<PlaylistSync>>();
            var plManagerMock = Substitute.For<MediaBrowser.Controller.Playlists.IPlaylistManager>();
            var userManagerMock = Substitute.For<MediaBrowser.Controller.Library.IUserManager>();
            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            SetUpLibManagerMock(libManagerMock, artist);
            var wrapper = new PlaylistSyncWrapper(loggerMock, plManagerMock, libManagerMock, userManagerMock, new List<ProviderPlaylistInfo>(), new Dictionary<string, string>(), new ManualMapStore(Substitute.For<ILogger<ManualMapStore>>()));

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
            TrackHelper.SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Artist 2")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Albtist", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("track", "album", "artist On Album", "just Artist")),
                (false, TrackHelper.CreateAllJfItems("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
                (false, TrackHelper.CreateAllJfItems("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st (CD) Artist")),
                (false, TrackHelper.CreateAllJfItems("Trxck", "Xlbu", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("Trxck", "Xlbum", "Zrtist On Album", "Just ArtisAB")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist);
            }
        }
        
        [Fact]
        public void TrackMatching_Respects_Level_IgnoreCase()
        {
            TrackHelper.SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnoreCase;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("track", "album", "artist On Album", "just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "albtist", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
                (false, TrackHelper.CreateAllJfItems("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st (CD) Artist")),
                (false, TrackHelper.CreateAllJfItems("Trxck", "Xlbu", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("Trxck", "Xlbum", "Zrtist On Album", "Just ArtisAB")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Level_IgnoreCasePunctuation()
        {
            TrackHelper.SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnorePunctuationAndCase;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("track", "album", "artist On Album", "just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "ALB-TIST", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
                (false, TrackHelper.CreateAllJfItems("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st (CD) Artist")),
                (false, TrackHelper.CreateAllJfItems("Trxck", "Xlbu", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("Trxck", "Xlbum", "Zrtist On Album", "Just ArtisAB")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Level_IgnoreCasePunctuationParens()
        {
            TrackHelper.SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnoreParensPunctuationAndCase;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("track", "album", "artist On Album", "just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "al.btist(b)", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
                (true, TrackHelper.CreateAllJfItems("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st Artist(CD)")),
                (false, TrackHelper.CreateAllJfItems("Trxck", "Xlbu", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("Trxck", "Xlbum", "Zrtist On Album", "Just ArtisAB")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Level_Fuzzy()
        {
            TrackHelper.SetValidPluginInstance();

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Fuzzy;
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("track", "album", "artist On Album", "just Artist")),
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "al.btist(b)", "Just Artist")),
                (true, TrackHelper.CreateAllJfItems("Tra-ck", "Al-bum", "Ar-tist On Album", "Ju-st Artist")),
                (true, TrackHelper.CreateAllJfItems("Track (Special Edition)", "Al-bum (Live)", "(AB) Ar-tist On Album", "Ju-st Artist(CD)")),
                (true, TrackHelper.CreateAllJfItems("Trxck", "Xlbu", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("Trxck", "Xlbum", "Zrtist On Album", "Just ArtisAB")),
                (false, TrackHelper.CreateAllJfItems("Trxckyz", "Xlbums", "Artist On Album", "Just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist);
            }
        }

        [Fact]
        public void TrackMatching_Respects_Criteria_TrackName()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("track", "Album", "Artist On Album", "Just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist, ItemMatchCriteria.TrackName);
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
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
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
            {
                (true, TrackHelper.CreateAllJfItems("Track", "Album", "Artist On Album", "Just Artist")),
                (false, TrackHelper.CreateAllJfItems("Track", "album", "Artist On Album", "Just Artist")),
            };

            foreach (var (isMatch, item) in jfItems)
            {
                CheckItem(isMatch, prov, item.Track, item.Album, item.Artist, ItemMatchCriteria.AlbumName);
            }

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)ItemMatchCriteria.Artists;
            jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
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
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
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
            jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
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
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Default;

            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
            var prov = TrackHelper.CreateProviderItem("Track", "Album", new List<string> { "Artist On Album", "Albtist" }, new List<string> { "Just Artist", "Artist 2" });
            var jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
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
            jfItems = new List<(bool IsMatch, (Audio Track, MusicAlbum Album, MusicArtist Artist) Item)>
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

        [Theory]
        [ClassData(typeof(FindTrackMatchDataRegression))]
        public void FindTrackMatch_Regression(
            bool shouldMatch,
            string? jfTrack,
            string? jfAlbum,
            string? jfAlbumArtist,
            string? jfArtist,
            string? provTrack,
            string? provAlbum,
            List<string> provAlbumArtists,
            List<string> provArtists,
            ItemMatchLevel matchLevel,
            ItemMatchCriteria matchCriteria)
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = matchLevel;
            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)matchCriteria;

            var prov = TrackHelper.CreateProviderItem(provTrack ?? "", provAlbum ?? "", provAlbumArtists, provArtists);
            var jf = TrackHelper.CreateAllJfItems(jfTrack, jfAlbum, jfAlbumArtist, jfArtist);
            CheckItem(shouldMatch, prov, jf.Item1, jf.Item2, jf.Item3, null);
        }

        class FindTrackMatchDataRegression : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                var allCriteria = ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists;

                // from issue #4
                yield return new object[] { false, "My Friends Over You", "Sticks and Stones", "New Found Glory", "New Found Glory", "My Friends Over You", "Sticks And Stones", new List<string> { "New Found Glory" }, new List<string> { "New Found Glory" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { true, "My Friends Over You", "Sticks and Stones", "New Found Glory", "New Found Glory", "My Friends Over You", "Sticks And Stones", new List<string> { "New Found Glory" }, new List<string> { "New Found Glory" }, ItemMatchLevel.IgnoreCase, allCriteria };
                yield return new object[] { false, "The Best of Me", "Say It Like You Mean It", "The Starting Line", "The Starting Line", "The Best Of Me", "Say It Like You Mean It", new List<string> { "The Starting Line" }, new List<string> { "The Starting Line" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { true, "The Best of Me", "Say It Like You Mean It", "The Starting Line", "The Starting Line", "The Best Of Me", "Say It Like You Mean It", new List<string> { "The Starting Line" }, new List<string> { "The Starting Line" }, ItemMatchLevel.IgnoreCase, allCriteria };
                yield return new object[] { false, "Darkside", "a modern tragedy, vol. 2", "grandson", "grandson", "Darkside", "a modern tragedy vol. 2", new List<string> { "grandson" }, new List<string> { "grandson" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { false, "Darkside", "a modern tragedy, vol. 2", "grandson", "grandson", "Darkside", "a modern tragedy vol. 2", new List<string> { "grandson" }, new List<string> { "grandson" }, ItemMatchLevel.IgnoreCase, allCriteria };
                yield return new object[] { true, "Darkside", "a modern tragedy, vol. 2", "grandson", "grandson", "Darkside", "a modern tragedy vol. 2", new List<string> { "grandson" }, new List<string> { "grandson" }, ItemMatchLevel.IgnorePunctuationAndCase, allCriteria };
                // from issue #20
                yield return new object[] { false, "Yes or No", "Yes or No", "GroovyRoom", "GroovyRoom", "Yes or No (Feat. 허윤진 of LE SSERAFIM, Crush)", "Yes or No", new List<string> { "GroovyRoom" }, new List<string> { "GroovyRoom", "HUH YUNJIN", "Crush" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { false, "Yes or No", "Yes or No", "GroovyRoom", "GroovyRoom", "Yes or No (Feat. 허윤진 of LE SSERAFIM, Crush)", "Yes or No", new List<string> { "GroovyRoom" }, new List<string> { "GroovyRoom", "HUH YUNJIN", "Crush" }, ItemMatchLevel.IgnoreCase, allCriteria };
                yield return new object[] { false, "Yes or No", "Yes or No", "GroovyRoom", "GroovyRoom", "Yes or No (Feat. 허윤진 of LE SSERAFIM, Crush)", "Yes or No", new List<string> { "GroovyRoom" }, new List<string> { "GroovyRoom", "HUH YUNJIN", "Crush" }, ItemMatchLevel.IgnorePunctuationAndCase, allCriteria };
                yield return new object[] { true, "Yes or No", "Yes or No", "GroovyRoom", "GroovyRoom", "Yes or No (Feat. 허윤진 of LE SSERAFIM, Crush)", "Yes or No", new List<string> { "GroovyRoom" }, new List<string> { "GroovyRoom", "HUH YUNJIN", "Crush" }, ItemMatchLevel.IgnoreParensPunctuationAndCase, allCriteria };
                yield return new object[] { true, "GODS", "GODS", "NewJeans", "NewJeans", "GODS", "GODS", new List<string> { "League of Legends", "NewJeans" }, new List<string> { "League of Legends", "NewJeans" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { false, "ONE SPARK", "With YOU\u2010th", "TWICE", "TWICE", "ONE SPARK", "With YOU-th", new List<string> { "TWICE" }, new List<string> { "TWICE" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { false, "ONE SPARK", "With YOU\u2010th", "TWICE", "TWICE", "ONE SPARK", "With YOU-th", new List<string> { "TWICE" }, new List<string> { "TWICE" }, ItemMatchLevel.IgnoreCase, allCriteria };
                yield return new object[] { true, "ONE SPARK", "With YOU\u2010th", "TWICE", "TWICE", "ONE SPARK", "With YOU-th", new List<string> { "TWICE" }, new List<string> { "TWICE" }, ItemMatchLevel.IgnorePunctuationAndCase, allCriteria };
                yield return new object[] { false, "Wife", "Wife", "(G)I\u2010DLE", "(G)I\u2010DLE", "Wife", "Wife", new List<string> { "(G)I-DLE" }, new List<string> { "(G)I-DLE" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { false, "Wife", "Wife", "(G)I\u2010DLE", "(G)I\u2010DLE", "Wife", "Wife", new List<string> { "(G)I-DLE" }, new List<string> { "(G)I-DLE" }, ItemMatchLevel.IgnoreCase, allCriteria };
                yield return new object[] { true, "Wife", "Wife", "(G)I\u2010DLE", "(G)I\u2010DLE", "Wife", "Wife", new List<string> { "(G)I-DLE" }, new List<string> { "(G)I-DLE" }, ItemMatchLevel.IgnorePunctuationAndCase, allCriteria };
                yield return new object[] { false, "Super Lady", "2", "(G)I\u2010DLE", "(G)I\u2010DLE", "Super Lady", "2", new List<string> { "(G)I-DLE" }, new List<string> { "(G)I-DLE" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { false, "Super Lady", "2", "(G)I\u2010DLE", "(G)I\u2010DLE", "Super Lady", "2", new List<string> { "(G)I-DLE" }, new List<string> { "(G)I-DLE" }, ItemMatchLevel.IgnoreCase, allCriteria };
                yield return new object[] { true, "Super Lady", "2", "(G)I\u2010DLE", "(G)I\u2010DLE", "Super Lady", "2", new List<string> { "(G)I-DLE" }, new List<string> { "(G)I-DLE" }, ItemMatchLevel.IgnorePunctuationAndCase, allCriteria };
                yield return new object[] { false, "\uB77D (\u6A02) (LALALALA)", "\u6A02-STAR", "Stray Kids", "Stray Kids", "LALALALA", "ROCK-STAR", new List<string> { "Stray Kids" }, new List<string> { "Stray Kids" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { true, "\uB77D (\u6A02) (LALALALA)", "\u6A02-STAR", "Stray Kids", "Stray Kids", "LALALALA", "ROCK-STAR", new List<string> { "Stray Kids" }, new List<string> { "Stray Kids" }, ItemMatchLevel.Default, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists };
                yield return new object[] { false, "Super", "FML", "SEVENTEEN", "SEVENTEEN", "Super", "SEVENTEEN 10th Mini Album 'FML'", new List<string> { "SEVENTEEN" }, new List<string> { "SEVENTEEN" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { true, "Super", "FML", "SEVENTEEN", "SEVENTEEN", "Super", "SEVENTEEN 10th Mini Album 'FML'", new List<string> { "SEVENTEEN" }, new List<string> { "SEVENTEEN" }, ItemMatchLevel.Default, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists };
                // from issue #24
                yield return new object[] { true, "Another Love", "Another Love", "OsTEKKe", "OsTEKKe", "Another Love", "Another Love", new List<string> { "OsTEKKe" }, new List<string> { "OsTEKKe" }, ItemMatchLevel.Default, allCriteria };
                // from issue #27
                yield return new object[] { false, "Potions - Acoustic", "Potions (Acoustic)", "SLANDER", "SLANDER", "Potions (Acoustic)", "Potions (Acoustic)", new List<string> { "SLANDER", "Said The Sky", "JT Roach" }, new List<string> { "JT Roach", "SLANDER", "Said The Sky" }, ItemMatchLevel.Default, allCriteria };
                yield return new object[] { false, "Potions - Acoustic", "Potions (Acoustic)", "SLANDER", "SLANDER", "Potions (Acoustic)", "Potions (Acoustic)", new List<string> { "SLANDER", "Said The Sky", "JT Roach" }, new List<string> { "JT Roach", "SLANDER", "Said The Sky" }, ItemMatchLevel.IgnoreCase, allCriteria };
                yield return new object[] { true, "Potions - Acoustic", "Potions (Acoustic)", "SLANDER", "SLANDER", "Potions (Acoustic)", "Potions (Acoustic)", new List<string> { "SLANDER", "Said The Sky", "JT Roach" }, new List<string> { "JT Roach", "SLANDER", "Said The Sky" }, ItemMatchLevel.IgnorePunctuationAndCase, allCriteria };
                yield return new object[] { true, "Potions - Acoustic", "Potions (Acoustic)", "SLANDER", "SLANDER", "Potions (Acoustic)", "Potions (Acoustic)", new List<string> { "SLANDER", "Said The Sky", "JT Roach" }, new List<string> { "JT Roach", "SLANDER", "Said The Sky" }, ItemMatchLevel.IgnorePunctuationAndCase, allCriteria };
                yield return new object[] { true, "Great Valley - Approaching Nirvana Remix", "Great Valley", "Veela", "Veela", "Great Valley (Approaching Nirvana Remix)", "Great Valley", new List<string> { "Veela" }, new List<string> { "Veela" }, ItemMatchLevel.IgnorePunctuationAndCase, allCriteria };
                yield return new object[] { true, "Matches - Acoustic [feat. Aaron Richards]", "Matches (The Remixes) [feat. Aaron Richards]", "Ephixa", "Ephixa", "Matches (Acoustic) [feat. Aaron Richards]", "Matches (The Remixes) [feat. Aaron Richards]", new List<string> { "Ephixa" }, new List<string> { "Ephixa" }, ItemMatchLevel.IgnorePunctuationAndCase, allCriteria };
                yield return new object[] { true, "Matches (feat. Aaron Richards)", "Matches (The Remixes) [feat. Aaron Richards]", "Ephixa", "Ephixa", "Matches (Acoustic) [feat. Aaron Richards]", "Matches (The Remixes) [feat. Aaron Richards]", new List<string> { "Ephixa" }, new List<string> { "Ephixa" }, ItemMatchLevel.IgnorePunctuationAndCase, allCriteria };
                // from issue #33
                yield return new object[] { true, "Akhiyaan Gulaab", "Teri Baaton Mein Aisa Uljha Jiya", "Mitraz", "Mitraz", "Akhiyaan Gulaab (From \"Teri Baaton Mein Aisa Uljha Jiya\")", "Akhiyaan Gulaab (From \"Teri Baaton Mein Aisa Uljha Jiya\")", new List<string> { "Mitraz" }, new List<string> { "Mitraz" }, ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName };
                yield return new object[] { true, "Akhiyaan Gulaab", "Teri Baaton Mein Aisa Uljha Jiya", "Mitraz", "Mitraz", "Akhiyaan Gulaab (From \"Teri Baaton Mein Aisa Uljha Jiya\")", "Akhiyaan Gulaab (From \"Teri Baaton Mein Aisa Uljha Jiya\")", new List<string> { "Mitraz" }, new List<string> { "Mitraz" }, ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists };
                yield return new object[] { true, "Akhiyaan Gulaab", "Teri Baaton Mein Aisa Uljha Jiya", "Mitraz", "Mitraz", "Akhiyaan Gulaab (From \"Teri Baaton Mein Aisa Uljha Jiya\")", "Akhiyaan Gulaab (From \"Teri Baaton Mein Aisa Uljha Jiya\")", new List<string> { "Mitraz" }, new List<string> { "Mitraz" }, ItemMatchLevel.IgnoreParensPunctuationAndCase, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists };
                yield return new object[] { false, "Akhiyaan Gulaab", "Teri Baaton Mein Aisa Uljha Jiya", "Mitraz", "Mitraz", "Akhiyaan Gulaab (From \"Teri Baaton Mein Aisa Uljha Jiya\")", "Akhiyaan Gulaab (From \"Teri Baaton Mein Aisa Uljha Jiya\")", new List<string> { "Mitraz" }, new List<string> { "Mitraz" }, ItemMatchLevel.IgnoreParensPunctuationAndCase, allCriteria };
                yield return new object[] { true, "Khoya Tu Kahaan", "Blind", "Vishal Dadlani", "Vishal Dadlani", "Khoya Tu Kahaan - From \"Blind\"", "Khoya Tu Kahaan (From \"Blind\")", new List<string> { "Various Artists" }, new List<string> { "Vishal Dadlani", "Shor Police", "Clinton Cerejo", "Bianca Gomes", "Shloke Lal" }, ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName };
                yield return new object[] { true, "Khoya Tu Kahaan", "Blind", "Vishal Dadlani", "Vishal Dadlani", "Khoya Tu Kahaan - From \"Blind\"", "Khoya Tu Kahaan (From \"Blind\")", new List<string> { "Various Artists" }, new List<string> { "Vishal Dadlani", "Shor Police", "Clinton Cerejo", "Bianca Gomes", "Shloke Lal" }, ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack, ItemMatchCriteria.TrackName | ItemMatchCriteria.Artists };
                yield return new object[] { false, "Khoya Tu Kahaan", "Blind", "Vishal Dadlani", "Vishal Dadlani", "Khoya Tu Kahaan - From \"Blind\"", "Khoya Tu Kahaan (From \"Blind\")", new List<string> { "Various Artists" }, new List<string> { "Vishal Dadlani", "Shor Police", "Clinton Cerejo", "Bianca Gomes", "Shloke Lal" }, ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists };
                yield return new object[] { false, "Khoya Tu Kahaan", "Blind", "Vishal Dadlani", "Vishal Dadlani", "Khoya Tu Kahaan - From \"Blind\"", "Khoya Tu Kahaan (From \"Blind\")", new List<string> { "Various Artists" }, new List<string> { "Vishal Dadlani", "Shor Police", "Clinton Cerejo", "Bianca Gomes", "Shloke Lal" }, ItemMatchLevel.IgnoreParensPunctuationAndCase, ItemMatchCriteria.TrackName | ItemMatchCriteria.Artists };
                yield return new object[] { false, "Khoya Tu Kahaan", "Blind", "Vishal Dadlani", "Vishal Dadlani", "Khoya Tu Kahaan - From \"Blind\"", "Khoya Tu Kahaan (From \"Blind\")", new List<string> { "Various Artists" }, new List<string> { "Vishal Dadlani", "Shor Police", "Clinton Cerejo", "Bianca Gomes", "Shloke Lal" }, ItemMatchLevel.IgnoreParensPunctuationAndCase, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists };
                yield return new object[] { false, "Khoya Tu Kahaan", "Blind", "Vishal Dadlani", "Vishal Dadlani", "Khoya Tu Kahaan - From \"Blind\"", "Khoya Tu Kahaan (From \"Blind\")", new List<string> { "Various Artists" }, new List<string> { "Vishal Dadlani", "Shor Police", "Clinton Cerejo", "Bianca Gomes", "Shloke Lal" }, ItemMatchLevel.IgnoreParensPunctuationAndCase, allCriteria };
                yield return new object[] { false, "Har kisi ko", "Boss", "Arijit Singh", "Arijit Singh", "Har Kisi Ko (From \"Boss)", "Love Dose Arijit Singh", new List<string> { "Arijit Singh" }, new List<string> { "Arijit Singh", "Neeti Mohan" }, ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName };
                yield return new object[] { true, "Har kisi ko", "Boss", "Arijit Singh", "Arijit Singh", "Har Kisi Ko (From \"Boss\")", "Love Dose Arijit Singh", new List<string> { "Arijit Singh" }, new List<string> { "Arijit Singh", "Neeti Mohan" }, ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName };
                yield return new object[] { true, "My Track", "XYZ", "Arijit Singh", "Arijit Singh", "My Track (From the Netflix movie \"XYZ\")", "Love Dose Arijit Singh", new List<string> { "Arijit Singh" }, new List<string> { "Arijit Singh", "Neeti Mohan" }, ItemMatchLevel.IgnoreParensPunctuationAndCaseUseAlbumFromTrack, ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName };
                yield return new object[] { false, "My Track", "XYZ", "Arijit Singh", "Arijit Singh", "My Track (From the Netflix movie \"XYZ\")", "Love Dose Arijit Singh", new List<string> { "Arijit Singh" }, new List<string> { "Arijit Singh", "Neeti Mohan" }, ItemMatchLevel.IgnoreParensPunctuationAndCase, allCriteria };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void FindTrackMatch_FromMultipleTrackCandidates()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnoreParensPunctuationAndCase;
            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);

            var prov = TrackHelper.CreateProviderItem("Matches - Acoustic [feat. Aaron Richards]", "Matches (The Remixes) [feat. Aaron Richards]", new List<string> { "Ephixa" }, new List<string> { "Ephixa" });

            var jfTrackCorrect = TrackHelper.CreateJfItem("Matches (Acoustic) [feat. Aaron Richards]", "Matches (The Remixes) [feat. Aaron Richards]", "Ephixa", "Ephixa");
            var jfAlbumCorrect = AlbumHelper.CreateJfItem("Matches (The Remixes) [feat. Aaron Richards]", "Ephixa", new List<Audio> { jfTrackCorrect });
            var jfTrackOther1 = TrackHelper.CreateJfItem("Matches [feat. Aaron Richards]", "Matches", "Ephixa", "Ephixa");
            var jfTrackOther2 = TrackHelper.CreateJfItem("Matches", "Matches", "Ephixa", "Ephixa");
            var jfTrackOther3 = TrackHelper.CreateJfItem("Matches (Acoustic)", "Matches", "Ephixa", "Ephixa");
            var jfAlbumOther1 = AlbumHelper.CreateJfItem("Matches", "Ephixa", new List<Audio> { jfTrackOther1 });
            var jfAlbumOther2 = AlbumHelper.CreateJfItem("Matches", "Ephixa", new List<Audio> { jfTrackOther2 });
            var jfAlbumOther3 = AlbumHelper.CreateJfItem("Matches", "Ephixa", new List<Audio> { jfTrackOther3 });

            var jfArtist = ArtistHelper.CreateJfItem("Ephixa", new List<MusicAlbum> { jfAlbumOther1, jfAlbumOther2, jfAlbumOther3, jfAlbumCorrect });

            var loggerMock = Substitute.For<ILogger<PlaylistSync>>();
            var plManagerMock = Substitute.For<MediaBrowser.Controller.Playlists.IPlaylistManager>();
            var userManagerMock = Substitute.For<MediaBrowser.Controller.Library.IUserManager>();
            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            SetUpLibManagerMock(libManagerMock, jfArtist);
            var wrapper = new PlaylistSyncWrapper(loggerMock, plManagerMock, libManagerMock, userManagerMock, new List<ProviderPlaylistInfo>(), new Dictionary<string, string>(), new ManualMapStore(Substitute.For<ILogger<ManualMapStore>>()));

            var result = wrapper.WrapGetMatchingTrack(prov, out var failedCrit);
            Assert.NotNull(result);
            Assert.Equal(result.Name, jfTrackCorrect.Name);
            Assert.Contains("Acoustic", result.Name);
            Assert.Contains("Richards", result.Name);
            Assert.True(failedCrit == ItemMatchCriteria.None);

            jfArtist = ArtistHelper.CreateJfItem("Ephixa", new List<MusicAlbum> { jfAlbumCorrect, jfAlbumOther1, jfAlbumOther2, jfAlbumOther3 });
            SetUpLibManagerMock(libManagerMock, jfArtist);
            wrapper = new PlaylistSyncWrapper(loggerMock, plManagerMock, libManagerMock, userManagerMock, new List<ProviderPlaylistInfo>(), new Dictionary<string, string>(), new ManualMapStore(Substitute.For<ILogger<ManualMapStore>>()));

            result = wrapper.WrapGetMatchingTrack(prov, out failedCrit);
            Assert.Equal(result, jfTrackCorrect);
            Assert.Contains("Acoustic", result!.Name);
            Assert.Contains("Richards", result!.Name);
            Assert.True(failedCrit == ItemMatchCriteria.None);

            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.Fuzzy;
            result = wrapper.WrapGetMatchingTrack(prov, out failedCrit);
            Assert.Equal(result, jfTrackCorrect);
            Assert.Contains("Acoustic", result!.Name);
            Assert.Contains("Richards", result!.Name);
            Assert.True(failedCrit == ItemMatchCriteria.None);
        }

        [Fact]
        public void FindTrackMatch_FromMultipleArtistCandidates()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.ItemMatchLevel = ItemMatchLevel.IgnoreParensPunctuationAndCase;
            Plugin.Instance!.Configuration.ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.Artists);

            var prov = TrackHelper.CreateProviderItem("You Make My Dreams (Come True)", "Voices", new List<string> { "Daryl Hall & John Oates" }, new List<string> { "Daryl Hall & John Oates" });

            var (jfTrackCorrect, jfAlbumCorrect, jfArtistCorrect) = TrackHelper.CreateAllJfItems("You Make My Dreams (Come True)", "Voices", "Daryl Hall & John Oates", "Daryl Hall & John Oates");
            jfTrackCorrect.Id = Guid.Parse("99999999-0000-0000-0000-000000000000");
            jfArtistCorrect.Id = Guid.Parse("00000000-0000-0000-0000-000000000010");

            var jfArtistOther1 = ArtistHelper.CreateJfItem("Daryl Hall & John Oates", new List<MusicAlbum>());
            jfArtistOther1.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var jfArtistOther2 = ArtistHelper.CreateJfItem("Daryl Hall & John Oates", new List<MusicAlbum>());
            jfArtistOther2.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");

            var loggerMock = Substitute.For<ILogger<PlaylistSync>>();
            var plManagerMock = Substitute.For<MediaBrowser.Controller.Playlists.IPlaylistManager>();
            var userManagerMock = Substitute.For<MediaBrowser.Controller.Library.IUserManager>();
            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            SetUpLibManagerMock(libManagerMock, new List<MediaBrowser.Controller.Entities.BaseItem>
            {
                jfArtistOther1,
                jfArtistCorrect,
                jfArtistOther2
            });
            var wrapper = new PlaylistSyncWrapper(loggerMock, plManagerMock, libManagerMock, userManagerMock, new List<ProviderPlaylistInfo>(), new Dictionary<string, string>(), new ManualMapStore(Substitute.For<ILogger<ManualMapStore>>()));

            var result = wrapper.WrapGetMatchingTrack(prov, out var failedCrit);
            Assert.NotNull(result);
            Assert.Equal(result.Name, jfTrackCorrect.Name);
            Assert.Equal(result.Id, jfTrackCorrect.Id);
            Assert.True(failedCrit == ItemMatchCriteria.None);
        }
    }
}
