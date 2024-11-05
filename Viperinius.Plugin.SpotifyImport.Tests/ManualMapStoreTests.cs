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
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests
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

    public class ManualMapStoreTests
    {
        [Fact]
        public void NoMapFileExists()
        {
            var mock = Substitute.For<ILogger<ManualMapStore>>();
            var store = new ManualMapStoreWrapper(mock);

            File.Delete(store.FilePath);

            Assert.Empty(store);
            Assert.False(store.Load());
            Assert.Empty(store);
        }

        [Fact]
        public void MapFileIsEmpty()
        {
            var mock = Substitute.For<ILogger<ManualMapStore>>();
            var store = new ManualMapStoreWrapper(mock);

            // empty temp file gets created automatically

            Assert.Empty(store);
            Assert.False(store.Load());
            Assert.Empty(store);
        }

        [Fact]
        public void MapFileHasNoValidJson()
        {
            var mock = Substitute.For<ILogger<ManualMapStore>>();
            var store = new ManualMapStoreWrapper(mock);

            File.WriteAllText(store.FilePath, "[}");

            Assert.Empty(store);
            Assert.False(store.Load());
            Assert.Empty(store);
        }

        [Fact]
        public void MapFileDoesNotFollowSchema()
        {
            var mock = Substitute.For<ILogger<ManualMapStore>>();
            var store = new ManualMapStoreWrapper(mock);

            File.WriteAllText(store.FilePath, "{\"justsomeotherjson\": true}");

            Assert.Empty(store);
            Assert.False(store.Load());
            Assert.Empty(store);
        }

        [Fact]
        public void MapFileHasUnsupportedSchemaVersion()
        {
            var mock = Substitute.For<ILogger<ManualMapStore>>();
            var store = new ManualMapStoreWrapper(mock);

            var schemaVersion = Version.Parse(store.TestSchemaVersion);
            var invalidVersion = new Version(schemaVersion.Major, schemaVersion.Minor, schemaVersion.Build + 1);

            var entry = new ManualMapTrack();
            var json = $$"""
                {
                    "$schema": "https://raw.githubusercontent.com/Viperinius/jellyfin-plugin-spotify-import/refs/heads/master/schemas/manual_track_map.schema.json",
                    "Version": "{{invalidVersion}}",
                    "Items": [{{JsonSerializer.Serialize(entry)}}]
                }
                """;

            File.WriteAllText(store.FilePath, json);

            Assert.Empty(store);
            Assert.False(store.Load());
            Assert.Empty(store);
        }

        [Fact]
        public void LoadMapFile()
        {
            var mock = Substitute.For<ILogger<ManualMapStore>>();
            var store = new ManualMapStoreWrapper(mock);

            var entry1 = new ManualMapTrack();
            entry1.Jellyfin.Track = "B987CFC5-D33A-482F-975B-E824A8A5B745";
            entry1.Provider.Name = "P1";
            entry1.Provider.AlbumName = "PA1";
            entry1.Provider.AlbumArtistNames.Add("PA11");
            entry1.Provider.ArtistNames.Add("PA12");

            var entry2 = new ManualMapTrack();
            entry2.Jellyfin.Track = "6E10D6CE-3344-4138-A611-C790F0E1C914";
            entry2.Provider.Name = "P2";
            entry2.Provider.AlbumName = "PA2";
            entry2.Provider.AlbumArtistNames.Add("PA21");
            entry2.Provider.ArtistNames.Add("PA22");

            var json = $$"""
                {
                    "$schema": "https://raw.githubusercontent.com/Viperinius/jellyfin-plugin-spotify-import/refs/heads/master/schemas/manual_track_map.schema.json",
                    "Version": "{{store.TestSchemaVersion}}",
                    "Items": [
                        {{JsonSerializer.Serialize(entry1)}},
                        {{JsonSerializer.Serialize(entry2)}}
                    ]
                }
                """;

            File.WriteAllText(store.FilePath, json);

            Assert.Empty(store);
            Assert.True(store.Load());
            Assert.Equal(2, store.Count);

            Assert.Equivalent(entry1, store.GetByTrackId(Guid.Parse(entry1.Jellyfin.Track)));
            Assert.Equivalent(entry2, store.GetByTrackId(Guid.Parse(entry2.Jellyfin.Track)));
        }
    }
}
