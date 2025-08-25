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
    public class MusicBrainzFinderTests
    {
        [Fact]
        public void IsNotEnabledByDefault()
        {
            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            using var db = DbRepositoryWrapper.GetInstance();

            var finder = new MusicBrainzFinder(libManagerMock, db);

            Plugin.SetInstance(null);
            Assert.False(finder.IsEnabled);

            TrackHelper.SetValidPluginInstance();
            Assert.False(finder.IsEnabled);
        }

        [Fact]
        public void GetsEnabledByConfig()
        {
            TrackHelper.SetValidPluginInstance();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            using var db = DbRepositoryWrapper.GetInstance();

            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.IsEnabled);
        }

        [Fact]
        public void FindTrackNoLibUsesMB()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

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
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => []);

            using var db = DbRepositoryWrapper.GetInstance();

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.False(finder.AnyLibraryUsesMusicBrainz);
            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }

        [Fact]
        public void FindTrackNoExistingMatch()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

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
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, "D)(RASUDv378fv", DateTime.UtcNow, [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);
            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }

        [Fact]
        public void FindTrackNoExistingNonPlaceholderMatch()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

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
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);
            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }

        [Fact]
        public void FindTrackNoTrackNameMatch()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

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
            var correctMbReleaseId = Guid.NewGuid();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, "D)(RASUDv378fv", DateTime.UtcNow, [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [Guid.NewGuid()], [correctMbReleaseId], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);

            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(info =>
                {
                    var hasAnyProvIds = info.ArgAt<MediaBrowser.Controller.Entities.InternalItemsQuery>(0).HasAnyProviderId;
                    if (hasAnyProvIds?.ContainsKey("MusicBrainzAlbum") ?? false)
                    {
                        return [new MediaBrowser.Controller.Entities.Audio.Audio { Name = $"ad873frasd{correctProviderTrackInfo.Name}AEF$Iasu", ProviderIds = hasAnyProvIds }];
                    }

                    return [];
                });

            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }

        [Fact]
        public void FindTrackOkDirectHit()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

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
            var correctMbRecordingId = Guid.NewGuid();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, "D)(RASUDv378fv", DateTime.UtcNow, [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [correctMbRecordingId], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);

            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(info =>
                {
                    var hasAnyProvIds = info.ArgAt<MediaBrowser.Controller.Entities.InternalItemsQuery>(0).HasAnyProviderId;
                    if (hasAnyProvIds?.ContainsKey("MusicBrainzTrack") ?? false)
                    {
                        return [new MediaBrowser.Controller.Entities.Audio.Audio { ProviderIds = hasAnyProvIds }];
                    }

                    return [];
                });

            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.NotNull(result);
            Assert.True(result.ProviderIds.ContainsKey("MusicBrainzTrack"));
            Assert.Equal(correctMbRecordingId.ToString(), result.ProviderIds["MusicBrainzTrack"]);
        }

        [Fact]
        public void FindTrackOkTrackNameMatch()
        {
            TrackHelper.SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnabledTrackMatchFindersRaw = (int)EnabledTrackMatchFinders.MusicBrainz;

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
            var correctMbReleaseId = Guid.NewGuid();

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(_ => [new MediaBrowser.Controller.Entities.Audio.Audio()]);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, "D)(RASUDv378fv", DateTime.UtcNow, [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [Guid.NewGuid()], [correctMbReleaseId], [Guid.NewGuid()]));
            db.UpsertIsrcMusicBrainzMapping(new SpotifyImport.Utils.DbIsrcMusicBrainzMapping(-1, correctProviderTrackInfo.IsrcId, DateTime.UtcNow, [], [], []));

            var finder = new MusicBrainzFinder(libManagerMock, db);
            Assert.True(finder.AnyLibraryUsesMusicBrainz);

            libManagerMock
                .GetItemList(Arg.Any<MediaBrowser.Controller.Entities.InternalItemsQuery>())
                .Returns(info =>
                {
                    var hasAnyProvIds = info.ArgAt<MediaBrowser.Controller.Entities.InternalItemsQuery>(0).HasAnyProviderId;
                    if (hasAnyProvIds?.ContainsKey("MusicBrainzAlbum") ?? false)
                    {
                        return [new MediaBrowser.Controller.Entities.Audio.Audio { Name = correctProviderTrackInfo.Name, ProviderIds = hasAnyProvIds }];
                    }

                    return [];
                });

            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.NotNull(result);
            Assert.True(result.ProviderIds.ContainsKey("MusicBrainzAlbum"));
            Assert.True(result.ProviderIds.ContainsKey("MusicBrainzReleaseGroup"));
            Assert.Equal(correctMbReleaseId.ToString(), result.ProviderIds["MusicBrainzAlbum"]);
        }
    }
}
