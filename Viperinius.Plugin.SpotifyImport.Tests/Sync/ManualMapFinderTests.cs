using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Viperinius.Plugin.SpotifyImport.Sync;
using Viperinius.Plugin.SpotifyImport.Tests.Db;
using Viperinius.Plugin.SpotifyImport.Tests.TestHelpers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Sync
{
    internal class ManualMapStoreWrapper : ManualMapStore
    {
        public ManualMapStoreWrapper(ILogger<ManualMapStore> logger) : base(logger)
        {
        }

        public override string FilePath { get; } = Path.GetTempFileName();

        public string TestSchemaVersion
        {
            get
            {
                var (_, version) = ValidateJsonSchema(JsonNode.Parse("{\"whatever\": 0}")!);
                return version;
            }
        }
    }

    public class ManualMapFinderTests
    {
        [Fact]
        public void IsEnabledByDefault()
        {
            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            var loggerMock = Substitute.For<ILogger<ManualMapStore>>();
            var manualMapStore = new ManualMapStoreWrapper(loggerMock);

            var finder = new ManualMapFinder(libManagerMock, manualMapStore);
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

            var loggerMock = Substitute.For<ILogger<ManualMapStore>>();
            var manualMapStore = new ManualMapStoreWrapper(loggerMock);

            var entry1 = new ManualMapTrack();
            entry1.Jellyfin.Track = "B987CFC5-D33A-482F-975B-E824A8A5B745";
            entry1.Provider.Name = "P1";
            entry1.Provider.AlbumName = "PA1";
            entry1.Provider.AlbumArtistNames.Add("PA11");
            entry1.Provider.ArtistNames.Add("PA12");

            var entry2 = new ManualMapTrack();
            entry2.Jellyfin.Track = correctJfId.ToString();
            entry2.Provider.Name = correctProviderTrackInfo.Name;
            entry2.Provider.AlbumName = correctProviderTrackInfo.AlbumName;
            entry2.Provider.AlbumArtistNames.Add(correctProviderTrackInfo.AlbumArtistNames[0]);
            entry2.Provider.ArtistNames.Add(correctProviderTrackInfo.ArtistNames[0]);

            var json = $$"""
                {
                    "$schema": "https://raw.githubusercontent.com/Viperinius/jellyfin-plugin-spotify-import/refs/heads/master/schemas/manual_track_map.schema.json",
                    "Version": "{{manualMapStore.TestSchemaVersion}}",
                    "Items": [
                        {{JsonSerializer.Serialize(entry1)}},
                        {{JsonSerializer.Serialize(entry2)}}
                    ]
                }
                """;

            File.WriteAllText(manualMapStore.FilePath, json);
            Assert.True(manualMapStore.Load());

            var finder = new ManualMapFinder(libManagerMock, manualMapStore);
            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.NotNull(result);
            Assert.Equal(correctJfId, result.Id);
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

            var loggerMock = Substitute.For<ILogger<ManualMapStore>>();
            var manualMapStore = new ManualMapStoreWrapper(loggerMock);

            var entry1 = new ManualMapTrack();
            entry1.Jellyfin.Track = "B987CFC5-D33A-482F-975B-E824A8A5B745";
            entry1.Provider.Name = "P1";
            entry1.Provider.AlbumName = "PA1";
            entry1.Provider.AlbumArtistNames.Add("PA11");
            entry1.Provider.ArtistNames.Add("PA12");

            var json = $$"""
                {
                    "$schema": "https://raw.githubusercontent.com/Viperinius/jellyfin-plugin-spotify-import/refs/heads/master/schemas/manual_track_map.schema.json",
                    "Version": "{{manualMapStore.TestSchemaVersion}}",
                    "Items": [
                        {{JsonSerializer.Serialize(entry1)}}
                    ]
                }
                """;

            File.WriteAllText(manualMapStore.FilePath, json);
            Assert.True(manualMapStore.Load());

            var finder = new ManualMapFinder(libManagerMock, manualMapStore);
            var result = finder.FindTrack(correctProviderId, correctProviderTrackInfo);
            Assert.Null(result);
        }
    }
}
