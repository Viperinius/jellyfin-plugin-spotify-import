using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using Viperinius.Plugin.SpotifyImport.Sync;
using Viperinius.Plugin.SpotifyImport.Tests.Db;
using Viperinius.Plugin.SpotifyImport.Tests.TestHelpers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Sync
{
    public class CacheFinderTests
    {
        [Fact]
        public void IsEnabledByDefault()
        {
            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            using var db = DbRepositoryWrapper.GetInstance();

            var finder = new CacheFinder(libManagerMock, db);
            Assert.True(finder.IsEnabled);
        }

        [Fact]
        public void FindTrackOk()
        {
            TrackHelper.SetValidPluginInstance();

            var correctProviderId = "asdiue8va";
            var correctProviderTrackInfo = new ProviderTrackInfo
            {
                Name = "48agWO$ga",
                AlbumName = "3948gasdaef30q9",
                ArtistNames = new List<string> { "d38aSDAS" },
                AlbumArtistNames = new List<string> { "oeif49" },
                TrackNumber = 1,
                IsrcId = "aeioda98r",
            };
            var correctJfId = Guid.NewGuid();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemById<MediaBrowser.Controller.Entities.Audio.Audio>(Arg.Any<Guid>())
                .Returns(info => new MediaBrowser.Controller.Entities.Audio.Audio { Id = info.ArgAt<Guid>(0) });

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var otherTrackId = db.InsertProviderTrack(correctProviderId, new ProviderTrackInfo());
            var correctTrackId = db.InsertProviderTrack(correctProviderId, correctProviderTrackInfo);
            db.InsertProviderTrack("abc", correctProviderTrackInfo);

            db.InsertProviderTrackMatch((long)correctTrackId!, Guid.NewGuid().ToString(), SpotifyImport.Matchers.ItemMatchLevel.IgnoreCase, SpotifyImport.Matchers.ItemMatchCriteria.AlbumName);
            db.InsertProviderTrackMatch((long)correctTrackId!, correctJfId.ToString(), Plugin.Instance!.Configuration.ItemMatchLevel, Plugin.Instance!.Configuration.ItemMatchCriteria);
            db.InsertProviderTrackMatch((long)otherTrackId!, Guid.NewGuid().ToString(), SpotifyImport.Matchers.ItemMatchLevel.Fuzzy, SpotifyImport.Matchers.ItemMatchCriteria.TrackName);

            var finder = new CacheFinder(libManagerMock, db);
            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.NotNull(result);
            Assert.Equal(correctJfId, result.Id);
        }

        [Fact]
        public void FindTrackPluginNotSet()
        {
            var correctProviderId = "asdiue8va";
            var correctProviderTrackInfo = new ProviderTrackInfo
            {
                Name = "48agWO$ga",
                AlbumName = "3948gasdaef30q9",
                ArtistNames = new List<string> { "d38aSDAS" },
                AlbumArtistNames = new List<string> { "oeif49" },
                TrackNumber = 1,
                IsrcId = "aeioda98r",
            };

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemById<MediaBrowser.Controller.Entities.Audio.Audio>(Arg.Any<Guid>())
                .Returns(info => new MediaBrowser.Controller.Entities.Audio.Audio { Id = info.ArgAt<Guid>(0) });

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var finder = new CacheFinder(libManagerMock, db);
            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }

        [Fact]
        public void FindTrackNoExistingMatch()
        {
            TrackHelper.SetValidPluginInstance();

            var correctProviderId = "asdiue8va";
            var correctProviderTrackInfo = new ProviderTrackInfo
            {
                Name = "48agWO$ga",
                AlbumName = "3948gasdaef30q9",
                ArtistNames = new List<string> { "d38aSDAS" },
                AlbumArtistNames = new List<string> { "oeif49" },
                TrackNumber = 1,
                IsrcId = "aeioda98r",
            };

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemById<MediaBrowser.Controller.Entities.Audio.Audio>(Arg.Any<Guid>())
                .Returns(info => new MediaBrowser.Controller.Entities.Audio.Audio { Id = info.ArgAt<Guid>(0) });

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var otherTrackId = db.InsertProviderTrack(correctProviderId, new ProviderTrackInfo());
            var correctTrackId = db.InsertProviderTrack(correctProviderId, correctProviderTrackInfo);
            db.InsertProviderTrack("abc", correctProviderTrackInfo);

            db.InsertProviderTrackMatch((long)correctTrackId!, Guid.NewGuid().ToString(), SpotifyImport.Matchers.ItemMatchLevel.IgnoreCase, SpotifyImport.Matchers.ItemMatchCriteria.AlbumName);
            db.InsertProviderTrackMatch((long)otherTrackId!, Guid.NewGuid().ToString(), SpotifyImport.Matchers.ItemMatchLevel.Fuzzy, SpotifyImport.Matchers.ItemMatchCriteria.TrackName);

            var finder = new CacheFinder(libManagerMock, db);
            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }
    }
}
