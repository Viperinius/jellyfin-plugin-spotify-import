using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Viperinius.Plugin.SpotifyImport.Matchers;

namespace Viperinius.Plugin.SpotifyImport.Utils
{
    internal class DbRepository : IDisposable
    {
        protected const string ArtistListSeparator = "|\n|";

        private const string TableProviderPlaylistsName = "ProviderPlaylists";
        private const string TableProviderTracksName = "ProviderTracks";
        private const string TableProviderPlaylistTracksName = "ProviderPlaylistTracks";
        private const string TableProviderTrackMatchesName = "ProviderTrackMatches";
        private const string TableIsrcMusicBrainzChecksName = "IsrcMusicBrainzChecks";
        private const string TableIsrcMusicBrainzRecordingName = "IsrcMusicBrainzRecordingMapping";
        private const string TableIsrcMusicBrainzReleaseName = "IsrcMusicBrainzReleaseMapping";
        private const string TableIsrcMusicBrainzRelGroupName = "IsrcMusicBrainzReleaseGroupMapping";

        private const string UpdateProviderPlaylistGenericCmd = $"UPDATE {TableProviderPlaylistsName} SET ProviderId = $ProviderId, PlaylistId = $PlaylistId, LastState = $LastState, LastTimestamp = $LastTimestamp";
        protected const string InsertProviderPlaylistCmd = $"INSERT INTO {TableProviderPlaylistsName} (ProviderId, PlaylistId, LastState, LastTimestamp) VALUES ($ProviderId, $PlaylistId, $LastState, $LastTimestamp)";
        protected const string InsertProviderTrackCmd = $"INSERT INTO {TableProviderTracksName} (ProviderId, TrackId, Name, AlbumName, AlbumArtistNames, ArtistNames, Number, IsrcId) VALUES ($ProviderId, $TrackId, $Name, $AlbumName, $AlbumArtistNames, $ArtistNames, $Number, $IsrcId)";
        private const string UpdateProviderPlaylistTrackGenericCmd = $"UPDATE {TableProviderPlaylistTracksName} SET PlaylistId = $PlaylistId, TrackId = $TrackId, Position = $Position";
        protected const string InsertProviderPlaylistTrackCmd = $"INSERT INTO {TableProviderPlaylistTracksName} (PlaylistId, TrackId, Position) VALUES ($PlaylistId, $TrackId, $Position)";
        protected const string InsertProviderTrackMatchCmd = $"INSERT INTO {TableProviderTrackMatchesName} (TrackId, JellyfinMatchId, MatchLevel, MatchCriteria) VALUES ($TrackId, $JellyfinMatchId, $MatchLevel, $MatchCriteria)";
        protected const string UpsertIsrcMusicBrainzCheckCmd = $"INSERT INTO {TableIsrcMusicBrainzChecksName} (Isrc, LastCheck) VALUES ($Isrc, $LastCheck) ON CONFLICT (Isrc) DO UPDATE SET LastCheck = $LastCheck";

        private static readonly string[] _tableNames = new[]
        {
            TableProviderPlaylistsName,
            TableProviderTracksName,
            TableProviderPlaylistTracksName,
            TableProviderTrackMatchesName,
            TableIsrcMusicBrainzChecksName,
            TableIsrcMusicBrainzRecordingName,
            TableIsrcMusicBrainzReleaseName,
            TableIsrcMusicBrainzRelGroupName,
        };

        private static readonly string[] _createTableQueries = new[]
        {
            @$"CREATE TABLE IF NOT EXISTS {TableProviderPlaylistsName} (
                Id INTEGER PRIMARY KEY,
                ProviderId TEXT,
                PlaylistId TEXT,
                LastState TEXT,
                LastTimestamp TEXT
            )",
            @$"CREATE TABLE IF NOT EXISTS {TableProviderTracksName} (
                Id INTEGER PRIMARY KEY,
                ProviderId TEXT,
                TrackId TEXT,
                Name TEXT,
                AlbumName TEXT,
                AlbumArtistNames TEXT,
                ArtistNames TEXT,
                Number INTEGER,
                IsrcId TEXT
            )",
            @$"CREATE TABLE IF NOT EXISTS {TableProviderPlaylistTracksName} (
                Id INTEGER PRIMARY KEY,
                PlaylistId INTEGER,
                TrackId INTEGER,
                Position INTEGER,
                FOREIGN KEY (PlaylistId) REFERENCES {TableProviderPlaylistsName}(Id) ON DELETE CASCADE,
                FOREIGN KEY (TrackId) REFERENCES {TableProviderTracksName}(Id) ON DELETE CASCADE
            )",
            @$"CREATE TABLE IF NOT EXISTS {TableProviderTrackMatchesName} (
                Id INTEGER PRIMARY KEY,
                TrackId INTEGER,
                JellyfinMatchId TEXT,
                MatchLevel INTEGER,
                MatchCriteria INTEGER,
                FOREIGN KEY (TrackId) REFERENCES {TableProviderTracksName}(Id) ON DELETE CASCADE
            )",
            $@"CREATE TABLE IF NOT EXISTS {TableIsrcMusicBrainzChecksName} (
                Id INTEGER PRIMARY KEY,
                Isrc TEXT UNIQUE NOT NULL,
                LastCheck TEXT NOT NULL
            )",
            $@"CREATE TABLE IF NOT EXISTS {TableIsrcMusicBrainzRecordingName} (
                Id INTEGER PRIMARY KEY,
                Isrc TEXT NOT NULL,
                MusicBrainzRecordingId TEXT NOT NULL,
                FOREIGN KEY (Isrc) REFERENCES {TableIsrcMusicBrainzChecksName}(Isrc) ON DELETE CASCADE
            )",
            $@"CREATE TABLE IF NOT EXISTS {TableIsrcMusicBrainzReleaseName} (
                Id INTEGER PRIMARY KEY,
                Isrc TEXT NOT NULL,
                MusicBrainzReleaseId TEXT NOT NULL,
                MusicBrainzTrackId TEXT NOT NULL,
                FOREIGN KEY (Isrc) REFERENCES {TableIsrcMusicBrainzChecksName}(Isrc) ON DELETE CASCADE
            )",
            $@"CREATE TABLE IF NOT EXISTS {TableIsrcMusicBrainzRelGroupName} (
                Id INTEGER PRIMARY KEY,
                Isrc TEXT NOT NULL,
                MusicBrainzReleaseGroupId TEXT NOT NULL,
                FOREIGN KEY (Isrc) REFERENCES {TableIsrcMusicBrainzChecksName}(Isrc) ON DELETE CASCADE
            )",
        };

        private readonly SqliteConnectionStringBuilder _connectionStringBuilder;
        private SqliteConnection? _connection;

        public DbRepository(string path)
        {
            Path = path;
            _connectionStringBuilder = new SqliteConnectionStringBuilder()
            {
                DataSource = Path,
                ForeignKeys = true,
            };
        }

        protected SqliteConnection Connection
        {
            get
            {
                if (_connection != null && _connection.State == System.Data.ConnectionState.Open)
                {
                    return _connection;
                }

                Open();
                return _connection!;
            }
        }

        public string Path { get; }

        public void InitDb()
        {
            using var tx = Connection.BeginTransaction();

            // create tables

            using var cmd = Connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            cmd.CommandText = string.Join(';', _createTableQueries);
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            cmd.ExecuteNonQuery();

            // update table columns if needed

            /*AddTableColumn(TableProviderTracksName, "Name", "TEXT");*/

            tx.Commit();
        }

        public void Open()
        {
            _connection = new SqliteConnection(_connectionStringBuilder.ToString());
            _connection.Open();
        }

        public string? GetLastProviderPlaylistState(string providerId, string providerPlaylistId)
        {
            using var selectCmd = Connection.CreateCommand();
            selectCmd.CommandText = $"SELECT LastState, LastTimestamp FROM {TableProviderPlaylistsName} WHERE ProviderId = $ProviderId AND PlaylistId = $PlaylistId";
            selectCmd.Parameters.AddWithValue("$ProviderId", providerId);
            selectCmd.Parameters.AddWithValue("$PlaylistId", providerPlaylistId);
            using var reader = selectCmd.ExecuteReader();

            if (reader.HasRows)
            {
                reader.Read();
                var state = reader.GetString(0);
                return state;
            }

            return null;
        }

        public long? GetProviderPlaylistDbId(string providerId, string providerPlaylistId)
        {
            using var selectCmd = Connection.CreateCommand();
            selectCmd.CommandText = $"SELECT Id FROM {TableProviderPlaylistsName} WHERE ProviderId = $ProviderId AND PlaylistId = $PlaylistId";
            selectCmd.Parameters.AddWithValue("$ProviderId", providerId);
            selectCmd.Parameters.AddWithValue("$PlaylistId", providerPlaylistId);
            return (long?)selectCmd.ExecuteScalar();
        }

        public long? UpsertProviderPlaylist(string providerId, ProviderPlaylistInfo playlist)
        {
            // check for existing item in db first
            using var selectCmd = Connection.CreateCommand();
            selectCmd.CommandText = $"SELECT Id FROM {TableProviderPlaylistsName} WHERE ProviderId = $ProviderId AND PlaylistId = $PlaylistId";
            selectCmd.Parameters.AddWithValue("$ProviderId", providerId);
            selectCmd.Parameters.AddWithValue("$PlaylistId", playlist.Id);
            using var reader = selectCmd.ExecuteReader();

            using var putCmd = Connection.CreateCommand();

            if (reader.HasRows)
            {
                reader.Read();
                var id = reader.GetInt32(0);
                // update entry with playlist data
                putCmd.CommandText = UpdateProviderPlaylistGenericCmd + " WHERE Id = $Id RETURNING Id";
                putCmd.Parameters.AddWithValue("$Id", id);
            }
            else
            {
                // add new entry
                putCmd.CommandText = InsertProviderPlaylistCmd + " RETURNING Id";
            }

            putCmd.Parameters.AddWithValue("$ProviderId", providerId);
            putCmd.Parameters.AddWithValue("$PlaylistId", playlist.Id);
            putCmd.Parameters.AddWithValue("$LastState", playlist.State);
            putCmd.Parameters.AddWithValue("$LastTimestamp", DateTime.UtcNow);
            return (long?)putCmd.ExecuteScalar();
        }

        public long? GetProviderTrackDbId(string providerId, string providerTrackId)
        {
            using var selectCmd = Connection.CreateCommand();
            selectCmd.CommandText = $"SELECT Id FROM {TableProviderTracksName} WHERE ProviderId = $ProviderId AND TrackId = $TrackId";
            selectCmd.Parameters.AddWithValue("$ProviderId", providerId);
            selectCmd.Parameters.AddWithValue("$TrackId", providerTrackId);
            return (long?)selectCmd.ExecuteScalar();
        }

        public ProviderTrackInfo? GetProviderTrack(string providerId, long trackDbId)
        {
            using var selectCmd = Connection.CreateCommand();
            selectCmd.CommandText = $"SELECT TrackId, Name, AlbumName, AlbumArtistNames, ArtistNames, Number, IsrcId FROM {TableProviderTracksName} WHERE ProviderId = $ProviderId AND Id = $Id";
            selectCmd.Parameters.AddWithValue("$ProviderId", providerId);
            selectCmd.Parameters.AddWithValue("$Id", trackDbId);
            using var reader = selectCmd.ExecuteReader();
            if (reader.HasRows)
            {
                reader.Read();
                var info = new ProviderTrackInfo
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    AlbumName = reader.GetString(2),
                    AlbumArtistNames = reader.GetString(3).Split(ArtistListSeparator).ToList(),
                    ArtistNames = reader.GetString(4).Split(ArtistListSeparator).ToList(),
                    TrackNumber = (uint)reader.GetInt32(5),
                    IsrcId = reader.IsDBNull(6) ? null : reader.GetString(6),
                };
                info.AlbumArtistNames.RemoveAll(string.IsNullOrWhiteSpace);
                info.ArtistNames.RemoveAll(string.IsNullOrWhiteSpace);
                return info;
            }

            return null;
        }

        public long? InsertProviderTrack(string providerId, ProviderTrackInfo track)
        {
            // check for existing item in db first
            var existingId = GetProviderTrackDbId(providerId, track.Id);
            if (existingId != null)
            {
                return existingId;
            }

            using var insertCmd = Connection.CreateCommand();
            insertCmd.CommandText = InsertProviderTrackCmd + " RETURNING Id";
            insertCmd.Parameters.AddWithValue("$ProviderId", providerId);
            insertCmd.Parameters.AddWithValue("$TrackId", track.Id);
            insertCmd.Parameters.AddWithValue("$Name", track.Name);
            insertCmd.Parameters.AddWithValue("$AlbumName", track.AlbumName);
            insertCmd.Parameters.AddWithValue("$AlbumArtistNames", string.Join(ArtistListSeparator, track.AlbumArtistNames));
            insertCmd.Parameters.AddWithValue("$ArtistNames", string.Join(ArtistListSeparator, track.ArtistNames));
            insertCmd.Parameters.AddWithValue("$Number", track.TrackNumber);
            if (track.IsrcId != null)
            {
                insertCmd.Parameters.AddWithValue("$IsrcId", track.IsrcId);
            }
            else
            {
                insertCmd.Parameters.AddWithValue("$IsrcId", DBNull.Value);
            }

            return (long?)insertCmd.ExecuteScalar();
        }

        public IEnumerable<(long Id, int Position)> GetProviderPlaylistTracks(long playlistDbId)
        {
            using var selectCmd = Connection.CreateCommand();
            selectCmd.CommandText = $"SELECT TrackId, Position FROM {TableProviderPlaylistTracksName} WHERE PlaylistId = $PlaylistId";
            selectCmd.Parameters.AddWithValue("$PlaylistId", playlistDbId);
            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var pos = reader.GetInt32(1);
                yield return (id, pos);
            }
        }

        public long? UpsertProviderPlaylistTrack(long playlistDbId, long trackDbId, int position)
        {
            // check for existing item in db first
            using var selectCmd = Connection.CreateCommand();
            selectCmd.CommandText = $"SELECT Id FROM {TableProviderPlaylistTracksName} WHERE PlaylistId = $PlaylistId AND TrackId = $TrackId";
            selectCmd.Parameters.AddWithValue("$PlaylistId", playlistDbId);
            selectCmd.Parameters.AddWithValue("$TrackId", trackDbId);
            using var reader = selectCmd.ExecuteReader();

            using var putCmd = Connection.CreateCommand();

            if (reader.HasRows)
            {
                reader.Read();
                var id = reader.GetInt32(0);
                // update entry with playlist data
                putCmd.CommandText = UpdateProviderPlaylistTrackGenericCmd + " WHERE Id = $Id RETURNING Id";
                putCmd.Parameters.AddWithValue("$Id", id);
            }
            else
            {
                // add new entry
                putCmd.CommandText = InsertProviderPlaylistTrackCmd + " RETURNING Id";
            }

            putCmd.Parameters.AddWithValue("$PlaylistId", playlistDbId);
            putCmd.Parameters.AddWithValue("$TrackId", trackDbId);
            putCmd.Parameters.AddWithValue("$Position", position);
            return (long?)putCmd.ExecuteScalar();
        }

        public bool DeleteProviderPlaylistTracks(long playlistDbId)
        {
            using var deleteCmd = Connection.CreateCommand();
            deleteCmd.CommandText = $"DELETE FROM {TableProviderPlaylistTracksName} WHERE PlaylistId = $PlaylistId";
            deleteCmd.Parameters.AddWithValue("$PlaylistId", playlistDbId);
            return deleteCmd.ExecuteNonQuery() > 0;
        }

        public IEnumerable<DbProviderTrackMatch> GetProviderTrackMatch(long trackDbId)
        {
            using var selectCmd = Connection.CreateCommand();
            selectCmd.CommandText = $"SELECT Id, JellyfinMatchId, MatchLevel, MatchCriteria FROM {TableProviderTrackMatchesName} WHERE TrackId = $TrackId";
            selectCmd.Parameters.AddWithValue("$TrackId", trackDbId);
            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var matchIdRaw = reader.GetString(1);
                if (!Guid.TryParse(matchIdRaw, out var matchId))
                {
                    continue;
                }

                var level = (ItemMatchLevel)reader.GetInt32(2);
                var criteria = (ItemMatchCriteria)reader.GetInt32(3);

                yield return new DbProviderTrackMatch(id, matchId, level, criteria);
            }
        }

        public long? InsertProviderTrackMatch(long trackDbId, string jellyfinTrackId, ItemMatchLevel level, ItemMatchCriteria criteria)
        {
            using var insertCmd = Connection.CreateCommand();
            insertCmd.CommandText = InsertProviderTrackMatchCmd + " RETURNING Id";
            insertCmd.Parameters.AddWithValue("$TrackId", trackDbId);
            insertCmd.Parameters.AddWithValue("$JellyfinMatchId", jellyfinTrackId);
            insertCmd.Parameters.AddWithValue("$MatchLevel", (int)level);
            insertCmd.Parameters.AddWithValue("$MatchCriteria", (int)criteria);

            return (long?)insertCmd.ExecuteScalar();
        }

        public IEnumerable<DbIsrcMusicBrainzMapping> GetIsrcMusicBrainzMapping(
            string? isrc = null,
            bool? hasAnyMbIdsSet = null,
            DateTime? minLastCheck = null,
            DateTime? maxLastCheck = null,
            bool? logicalAnd = null,
            bool returnMbIdLists = true)
        {
            var logicalOp = (logicalAnd == null || (bool)logicalAnd) ? " AND " : " OR ";
            using var selectCmd = Connection.CreateCommand();
            static List<string> CreateBasicWhere(SqliteCommand cmd, string? isrcFilter = null)
            {
                var cmdWhere = new List<string>();
                if (!string.IsNullOrEmpty(isrcFilter))
                {
                    cmdWhere.Add("Isrc = $Isrc");
                    cmd.Parameters.AddWithValue("$Isrc", isrcFilter);
                }

                return cmdWhere;
            }

            // lookup isrc
            selectCmd.CommandText = $"SELECT Id, Isrc, LastCheck FROM {TableIsrcMusicBrainzChecksName} c";
            var where = CreateBasicWhere(selectCmd, isrc);
            if (minLastCheck != null)
            {
                where.Add("LastCheck >= $MinLastCheck");
                selectCmd.Parameters.AddWithValue("$MinLastCheck", minLastCheck.Value.ToString("s"));
            }

            if (maxLastCheck != null)
            {
                where.Add("LastCheck <= $MaxLastCheck");
                selectCmd.Parameters.AddWithValue("$MaxLastCheck", maxLastCheck.Value.ToString("s"));
            }

            if (hasAnyMbIdsSet != null)
            {
                var nestedWhere = new List<string>
                {
                    $"{(!hasAnyMbIdsSet.Value ? "NOT " : string.Empty)}EXISTS(SELECT 1 FROM {TableIsrcMusicBrainzRecordingName} WHERE Isrc = c.Isrc)",
                    $"{(!hasAnyMbIdsSet.Value ? "NOT " : string.Empty)}EXISTS(SELECT 1 FROM {TableIsrcMusicBrainzReleaseName} WHERE Isrc = c.Isrc)",
                    $"{(!hasAnyMbIdsSet.Value ? "NOT " : string.Empty)}EXISTS(SELECT 1 FROM {TableIsrcMusicBrainzRelGroupName} WHERE Isrc = c.Isrc)",
                };
                where.Add("(" + string.Join(hasAnyMbIdsSet.Value ? " OR " : " AND ", nestedWhere) + ")");
            }

            if (where.Count > 0)
            {
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities --> where, logicalOp only contain pre-defined strings
                selectCmd.CommandText += $" WHERE {string.Join(logicalOp, where)}";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            }

            using var reader = selectCmd.ExecuteReader();
            using var innerCmd = Connection.CreateCommand();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var readIsrc = reader.GetString(1);
                var lastCheck = reader.GetDateTime(2);

                // lookup associated MB ids
                var recordings = new List<Guid>();
                var releases = new List<Guid>();
                var tracks = new List<Guid>();
                var releaseGroups = new List<Guid>();

                if (returnMbIdLists)
                {
                    innerCmd.Parameters.Clear();
                    innerCmd.CommandText = $"SELECT MusicBrainzRecordingId FROM {TableIsrcMusicBrainzRecordingName}";
                    var innerWhere = CreateBasicWhere(innerCmd, readIsrc);
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities --> innerWhere, logicalOp only contain pre-defined strings
                    innerCmd.CommandText += $" WHERE {string.Join(logicalOp, innerWhere)}";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                    {
                        using var innerReader = innerCmd.ExecuteReader();
                        while (innerReader.Read())
                        {
                            var mbRecordingIdRaw = innerReader.GetString(0);
                            if (Guid.TryParse(mbRecordingIdRaw, out var tmpGuid))
                            {
                                recordings.Add(tmpGuid);
                            }
                        }
                    }

                    innerCmd.Parameters.Clear();
                    innerCmd.CommandText = $"SELECT MusicBrainzReleaseId, MusicBrainzTrackId FROM {TableIsrcMusicBrainzReleaseName}";
                    innerWhere = CreateBasicWhere(innerCmd, readIsrc);
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities --> innerWhere, logicalOp only contain pre-defined strings
                    innerCmd.CommandText += $" WHERE {string.Join(logicalOp, innerWhere)}";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                    {
                        using var innerReader = innerCmd.ExecuteReader();
                        while (innerReader.Read())
                        {
                            var mbReleaseIdRaw = innerReader.GetString(0);
                            if (Guid.TryParse(mbReleaseIdRaw, out var tmpGuid))
                            {
                                releases.Add(tmpGuid);
                            }

                            var mbTrackIdRaw = innerReader.GetString(1);
                            if (Guid.TryParse(mbTrackIdRaw, out tmpGuid))
                            {
                                tracks.Add(tmpGuid);
                            }
                        }
                    }

                    innerCmd.Parameters.Clear();
                    innerCmd.CommandText = $"SELECT MusicBrainzReleaseGroupId FROM {TableIsrcMusicBrainzRelGroupName}";
                    innerWhere = CreateBasicWhere(innerCmd, readIsrc);
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities --> innerWhere, logicalOp only contain pre-defined strings
                    innerCmd.CommandText += $" WHERE {string.Join(logicalOp, innerWhere)}";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                    {
                        using var innerReader = innerCmd.ExecuteReader();
                        while (innerReader.Read())
                        {
                            var mbReleaseGroupIdRaw = innerReader.GetString(0);
                            if (Guid.TryParse(mbReleaseGroupIdRaw, out var tmpGuid))
                            {
                                releaseGroups.Add(tmpGuid);
                            }
                        }
                    }
                }

                yield return new DbIsrcMusicBrainzMapping(id, readIsrc, lastCheck, recordings, releases, tracks, releaseGroups);
            }
        }

        public long? UpsertIsrcMusicBrainzMapping(DbIsrcMusicBrainzMapping mapping)
        {
            using var upsertCmd = Connection.CreateCommand();
            upsertCmd.CommandText = UpsertIsrcMusicBrainzCheckCmd + " RETURNING Id";
            upsertCmd.Parameters.AddWithValue("$Isrc", mapping.Isrc);
            upsertCmd.Parameters.AddWithValue("$LastCheck", mapping.LastCheck);

            for (var ii = 0; ii < mapping.MusicBrainzRecordingIds.Count; ii++)
            {
                var recording = mapping.MusicBrainzRecordingIds[ii];
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities --> only variable is ii
                upsertCmd.CommandText += $"; INSERT INTO {TableIsrcMusicBrainzRecordingName} (Isrc, MusicBrainzRecordingId) VALUES ($Isrc, $MusicBrainzRecordingId{ii})";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                upsertCmd.Parameters.AddWithValue($"$MusicBrainzRecordingId{ii}", recording);
            }

            // this assumes number of releases should always be = number of track ids, otherwise the "excess" values will be ignored
            for (var ii = 0; ii < Math.Min(mapping.MusicBrainzReleaseIds.Count, mapping.MusicBrainzTrackIds.Count); ii++)
            {
                var release = mapping.MusicBrainzReleaseIds[ii];
                var track = mapping.MusicBrainzTrackIds[ii];
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities --> only variable is ii
                upsertCmd.CommandText += $"; INSERT INTO {TableIsrcMusicBrainzReleaseName} (Isrc, MusicBrainzReleaseId, MusicBrainzTrackId) VALUES ($Isrc, $MusicBrainzReleaseId{ii}, $MusicBrainzTrackId{ii})";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                upsertCmd.Parameters.AddWithValue($"$MusicBrainzReleaseId{ii}", release);
                upsertCmd.Parameters.AddWithValue($"$MusicBrainzTrackId{ii}", track);
            }

            for (var ii = 0; ii < mapping.MusicBrainzReleaseGroupIds.Count; ii++)
            {
                var relGroup = mapping.MusicBrainzReleaseGroupIds[ii];
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities --> only variable is ii
                upsertCmd.CommandText += $"; INSERT INTO {TableIsrcMusicBrainzRelGroupName} (Isrc, MusicBrainzReleaseGroupId) VALUES ($Isrc, $MusicBrainzReleaseGroupId{ii})";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                upsertCmd.Parameters.AddWithValue($"$MusicBrainzReleaseGroupId{ii}", relGroup);
            }

            return (long?)upsertCmd.ExecuteScalar();
        }

        public bool DeleteIsrcMusicBrainzMapping(IList<long> dbIds)
        {
            if (dbIds.Count == 0)
            {
                return true;
            }

            using var deleteCmd = Connection.CreateCommand();
            var idParams = string.Join(',', dbIds.Select((_, ii) => $"$Id{ii}"));
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities --> idParams only contains pre-defined strings
            deleteCmd.CommandText = $"DELETE FROM {TableIsrcMusicBrainzChecksName} WHERE Id IN ({idParams})";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            for (var ii = 0; ii < dbIds.Count; ii++)
            {
                deleteCmd.Parameters.AddWithValue($"$Id{ii}", dbIds[ii]);
            }

            return deleteCmd.ExecuteNonQuery() > 0;
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        private bool HasTableColumn(string table, string column)
        {
            if (!_tableNames.Contains(table))
            {
                return false;
            }

            using var cmd = Connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $Name";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            cmd.Parameters.AddWithValue("$Name", column);
            var count = (long?)cmd.ExecuteScalar();
            return count > 0;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "for future reference")]
        private bool AddTableColumn(string table, string column, string type)
        {
            if (!_tableNames.Contains(table))
            {
                return false;
            }

            if (HasTableColumn(table, column))
            {
                return true;
            }

            using var cmd = Connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            cmd.Parameters.AddWithValue("$Name", column);
            cmd.ExecuteNonQuery();
            return true;
        }
    }
}
