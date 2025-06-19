using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.Logging;

namespace Viperinius.Plugin.SpotifyImport
{
    internal class ManualMapStore : IList<ManualMapTrack>
    {
        private static readonly string _schemaResourceName = $"{Plugin.PluginQualifiedName}.manual_track_map.schema.json";

        private readonly ILogger<ManualMapStore>? _logger;
        private List<ManualMapTrack> _tracks;
        private static JsonSchema? _schema;

        private readonly JsonSerializerOptions _serializerOpts;

        public ManualMapStore(ILogger<ManualMapStore>? logger = null)
        {
            _logger = logger;
            _tracks = new List<ManualMapTrack>();
            _serializerOpts = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
        }

        protected static JsonSchema Schema
        {
            get
            {
                using var reader = Assembly.GetExecutingAssembly().GetManifestResourceStream(_schemaResourceName);
                var task = JsonSchema.FromStream(reader!);
                if (task.IsCompleted)
                {
                    _schema ??= task.Result;
                }
                else
                {
                    _schema ??= task.AsTask().Result;
                }

                return _schema;
            }
        }

        public virtual string FilePath => Path.Combine(Plugin.Instance!.PluginDataPath, "manual_track_map.json");

        public int Count => _tracks.Count;

        public bool IsReadOnly => false;

        public ManualMapTrack this[int index] { get => _tracks[index]; set => _tracks[index] = value; }

        private string? GetMapContents()
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            using var reader = new StreamReader(FilePath, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        public bool Load()
        {
            var rawString = GetMapContents();
            if (string.IsNullOrWhiteSpace(rawString))
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger?.LogInformation("Manual track map is empty, skip loading it");
                }

                return false;
            }

            JsonNode? jsonNode;
            try
            {
                jsonNode = JsonNode.Parse(rawString);
            }
            catch (JsonException)
            {
                jsonNode = null;
            }

            if (jsonNode == null)
            {
                _logger?.LogError("Failed to parse manual track map json");
                return false;
            }

            var (isOk, version) = ValidateJsonSchema(jsonNode);
            if (!isOk)
            {
                _logger?.LogError("Manual track map does not follow the expected schema");
                return false;
            }

            var mapSchemaVersion = jsonNode["Version"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(mapSchemaVersion))
            {
                _logger?.LogError("Manual track map does not have a schema version set");
                return false;
            }

            var expectedMinSchemaVersion = Version.Parse(version);
            var actualSchemaVersion = Version.Parse(mapSchemaVersion);
            if (actualSchemaVersion > expectedMinSchemaVersion)
            {
                _logger?.LogError("Manual track map has an invalid schema version set");
                return false;
            }

            var tracks = JsonSerializer.Deserialize<List<ManualMapTrack>>(jsonNode["Items"]);
            if (tracks == null)
            {
                _logger?.LogError("Failed to deserialise the manual track map json");
                return false;
            }

            _tracks = tracks;
            return true;
        }

        public bool Save()
        {
            var schemaVersion = Schema.GetProperties()?["Version"].GetConst()?.GetValue<string>();

            var json = new JsonObject
            {
                ["$schema"] = "https://raw.githubusercontent.com/Viperinius/jellyfin-plugin-spotify-import/refs/heads/master/schemas/manual_track_map.schema.json",
                ["Version"] = schemaVersion,
                ["Items"] = JsonSerializer.SerializeToNode(_tracks)
            };

            File.WriteAllText(FilePath, JsonSerializer.Serialize(json, _serializerOpts));

            return true;
        }

        public static (bool IsOk, string Version) ValidateJsonSchema(JsonNode jsonData)
        {
            var schemaVersion = Schema.GetProperties()?["Version"].GetConst()?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(schemaVersion))
            {
                return (false, string.Empty);
            }

            return (Schema.Evaluate(jsonData).IsValid, schemaVersion);
        }

        public ManualMapTrack? GetByTrackId(Guid jellyfinId)
        {
            return _tracks.FirstOrDefault(t => t?.Jellyfin.Track.Equals(jellyfinId.ToString(), StringComparison.OrdinalIgnoreCase) ?? false, null);
        }

        public ManualMapTrack? GetByProviderTrackInfo(ProviderTrackInfo providerTrackInfo)
        {
            return _tracks.FirstOrDefault(t => t?.Provider.Equals(providerTrackInfo) ?? false, null);
        }

        public int IndexOf(ManualMapTrack item)
        {
            return _tracks.IndexOf(item);
        }

        public void Insert(int index, ManualMapTrack item)
        {
            _tracks.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            _tracks.RemoveAt(index);
        }

        public void Add(ManualMapTrack item)
        {
            _tracks.Add(item);
        }

        public void AddRange(IEnumerable<ManualMapTrack> items)
        {
            _tracks.AddRange(items);
        }

        public void Clear()
        {
            _tracks.Clear();
        }

        public bool Contains(ManualMapTrack item)
        {
            return _tracks.Contains(item);
        }

        public void CopyTo(ManualMapTrack[] array, int arrayIndex)
        {
            _tracks.CopyTo(array, arrayIndex);
        }

        public bool Remove(ManualMapTrack item)
        {
            return _tracks.Remove(item);
        }

        public IEnumerator<ManualMapTrack> GetEnumerator()
        {
            return _tracks.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
