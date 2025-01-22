using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Playlists;
using Microsoft.Data.Sqlite;

namespace Viperinius.Plugin.SpotifyImport.Utils
{
    internal class DbRepository : IDisposable
    {
        private const string ArtistListSeparator = "|\n|";

        private const string TableProviderPlaylistsName = "ProviderPlaylists";
        private const string TableProviderTracksName = "ProviderTracks";
        private const string TableProviderPlaylistTracksName = "ProviderPlaylistTracks";
        /*private const string TableProviderTrackMatchesName = "ProviderTrackMatches";*/

        private const string UpdateProviderPlaylistGenericCmd = $"UPDATE {TableProviderPlaylistsName} SET ProviderId = $ProviderId, PlaylistId = $PlaylistId, LastState = $LastState, LastTimestamp = $LastTimestamp";
        private const string InsertProviderPlaylistCmd = $"INSERT INTO {TableProviderPlaylistsName} (ProviderId, PlaylistId, LastState, LastTimestamp) VALUES ($ProviderId, $PlaylistId, $LastState, $LastTimestamp)";
        private const string InsertProviderTrackCmd = $"INSERT INTO {TableProviderTracksName} (ProviderId, TrackId, Name, AlbumName, AlbumArtistNames, ArtistNames, Number, IsrcId) VALUES ($ProviderId, $TrackId, $Name, $AlbumName, $AlbumArtistNames, $ArtistNames, $Number, $IsrcId)";
        private const string UpdateProviderPlaylistTrackGenericCmd = $"UPDATE {TableProviderPlaylistTracksName} SET PlaylistId = $PlaylistId, TrackId = $TrackId, Position = $Position";
        private const string InsertProviderPlaylistTrackCmd = $"INSERT INTO {TableProviderPlaylistTracksName} (PlaylistId, TrackId, Position) VALUES ($PlaylistId, $TrackId, $Position)";

        private static readonly string[] _tableNames = new[]
        {
            TableProviderPlaylistsName,
            TableProviderTracksName,
            TableProviderPlaylistTracksName,
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
                FOREIGN KEY (PlaylistId) REFERENCES {TableProviderPlaylistsName}(Id),
                FOREIGN KEY (TrackId) REFERENCES {TableProviderTracksName}(Id)
            )",
            /*@$"CREATE TABLE IF NOT EXISTS {TableProviderTrackMatchesName} (
                Id INTEGER PRIMARY KEY,
                TrackId TEXT,
                JellyfinMatchId TEXT,
                MatchLevel INTEGER,
                MatchCriteria INTEGER,
                FOREIGN KEY (TrackId) REFERENCES {TableProviderTracksName}(Id)
            )",*/
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

        private SqliteConnection Connection
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
