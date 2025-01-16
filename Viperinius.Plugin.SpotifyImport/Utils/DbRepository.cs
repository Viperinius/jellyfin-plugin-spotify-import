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
        private const string TableProviderPlaylistsName = "ProviderPlaylists";
        private const string TableProviderTracksName = "ProviderTracks";
        /*private const string TableProviderPlaylistTracksName = "ProviderPlaylistTracks";
        private const string TableProviderTrackMatchesName = "ProviderTrackMatches";*/

        private const string UpdateProviderPlaylistGenericCmd = $"UPDATE {TableProviderPlaylistsName} SET ProviderId = $ProviderId, PlaylistId = $PlaylistId, LastState = $LastState, LastTimestamp = $LastTimestamp";
        private const string InsertProviderPlaylistCmd = $"INSERT INTO {TableProviderPlaylistsName} (ProviderId, PlaylistId, LastState, LastTimestamp) VALUES ($ProviderId, $PlaylistId, $LastState, $LastTimestamp)";
        private const string InsertProviderTrackCmd = $"INSERT INTO {TableProviderTracksName} (ProviderId, TrackId, IsrcId) VALUES ($ProviderId, $TrackId, $IsrcId)";

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
                IsrcId TEXT
            )",
            /*@$"CREATE TABLE IF NOT EXISTS {TableProviderPlaylistTracksName} (
                Id INTEGER PRIMARY KEY,
                PlaylistId TEXT,
                TrackId TEXT,
                Position INTEGER,
                FOREIGN KEY (PlaylistId) REFERENCES {TableProviderPlaylistsName}(Id),
                FOREIGN KEY (TrackId) REFERENCES {TableProviderTracksName}(Id)
            )",
            @$"CREATE TABLE IF NOT EXISTS {TableProviderTrackMatchesName} (
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

            tx.Commit();
        }

        public void Open()
        {
            _connection = new SqliteConnection(_connectionStringBuilder.ToString());
            _connection.Open();
        }

        public string? GetLastProviderPlaylistState(string providerId, string playlistId)
        {
            using var selectCmd = Connection.CreateCommand();
            selectCmd.CommandText = $"SELECT LastState, LastTimestamp FROM {TableProviderPlaylistsName} WHERE ProviderId = $ProviderId AND PlaylistId = $PlaylistId";
            selectCmd.Parameters.AddWithValue("$ProviderId", providerId);
            selectCmd.Parameters.AddWithValue("$PlaylistId", playlistId);
            using var reader = selectCmd.ExecuteReader();

            if (reader.HasRows)
            {
                reader.Read();
                var state = reader.GetString(0);
                return state;
            }

            return null;
        }

        public bool UpsertProviderPlaylist(string providerId, ProviderPlaylistInfo playlist)
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
                putCmd.CommandText = UpdateProviderPlaylistGenericCmd + " WHERE Id = $Id";
                putCmd.Parameters.AddWithValue("$Id", id);
            }
            else
            {
                // add new entry
                putCmd.CommandText = InsertProviderPlaylistCmd;
            }

            putCmd.Parameters.AddWithValue("$ProviderId", providerId);
            putCmd.Parameters.AddWithValue("$PlaylistId", playlist.Id);
            putCmd.Parameters.AddWithValue("$LastState", playlist.State);
            putCmd.Parameters.AddWithValue("$LastTimestamp", DateTime.UtcNow);
            return putCmd.ExecuteNonQuery() == 1;
        }

        public bool InsertProviderTrack(string providerId, ProviderTrackInfo track)
        {
            // check for existing item in db first
            using var selectCmd = Connection.CreateCommand();
            selectCmd.CommandText = $"SELECT Id FROM {TableProviderTracksName} WHERE ProviderId = $ProviderId AND TrackId = $TrackId";
            selectCmd.Parameters.AddWithValue("$ProviderId", providerId);
            selectCmd.Parameters.AddWithValue("$TrackId", track.Id);
            using var reader = selectCmd.ExecuteReader();
            if (reader.HasRows)
            {
                return true;
            }

            using var insertCmd = Connection.CreateCommand();
            insertCmd.CommandText = InsertProviderTrackCmd;
            insertCmd.Parameters.AddWithValue("$ProviderId", providerId);
            insertCmd.Parameters.AddWithValue("$TrackId", track.Id);
            if (track.IsrcId != null)
            {
                insertCmd.Parameters.AddWithValue("$IsrcId", track.IsrcId);
            }
            else
            {
                insertCmd.Parameters.AddWithValue("$IsrcId", DBNull.Value);
            }

            return insertCmd.ExecuteNonQuery() == 1;
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
