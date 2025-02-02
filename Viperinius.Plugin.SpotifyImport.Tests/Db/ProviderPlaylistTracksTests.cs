#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Db
{
    public class ProviderPlaylistTracksTests
    {
        private void PrepareOtherTablesData(DbRepositoryWrapper db)
        {
            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderPlaylistCmd();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$PlaylistId", "2b");
            cmd.Parameters.AddWithValue("$LastState", string.Empty);
            cmd.Parameters.AddWithValue("$LastTimestamp", DateTime.MinValue);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.CommandText = db.GetInsertProviderPlaylistCmd();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$PlaylistId", "3c");
            cmd.Parameters.AddWithValue("$LastState", string.Empty);
            cmd.Parameters.AddWithValue("$LastTimestamp", DateTime.MinValue);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.CommandText = db.GetInsertProviderTrackCmd();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$TrackId", "2a");
            cmd.Parameters.AddWithValue("$Name", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumName", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$ArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$Number", 0);
            cmd.Parameters.AddWithValue("$IsrcId", string.Empty);
            Assert.Equal(1, cmd.ExecuteNonQuery());
        }

        [Fact]
        public void CanInsert()
        {
            var correctPlaylistDbId = 1;
            var correctTrackDbId = 1;
            var correctPosition = 66;

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            PrepareOtherTablesData(db);

            db.UpsertProviderPlaylistTrack(correctPlaylistDbId, correctTrackDbId, correctPosition);

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT * FROM ProviderPlaylistTracks";
            var rowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    Assert.Equal(1, reader.GetInt32(0));
                    Assert.Equal(correctPlaylistDbId, reader.GetInt64(1));
                    Assert.Equal(correctTrackDbId, reader.GetInt64(2));
                    Assert.Equal(correctPosition, reader.GetInt32(3));
                }
            }

            Assert.Equal(1, rowCount);
        }

        [Fact]
        public void CanUpdate()
        {
            var correctPlaylistDbId = 1;
            var correctTrackDbId = 1;
            var correctOldPosition = 15;
            var correctPosition = 66;

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            PrepareOtherTablesData(db);

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderPlaylistTrackCmd();
            cmd.Parameters.AddWithValue("$PlaylistId", correctPlaylistDbId);
            cmd.Parameters.AddWithValue("$TrackId", correctTrackDbId);
            cmd.Parameters.AddWithValue("$Position", correctOldPosition);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            db.UpsertProviderPlaylistTrack(correctPlaylistDbId, correctTrackDbId, correctPosition);

            cmd.CommandText = "SELECT * FROM ProviderPlaylistTracks";
            cmd.Parameters.Clear();
            var rowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    Assert.Equal(1, reader.GetInt32(0));
                    Assert.Equal(correctPlaylistDbId, reader.GetInt64(1));
                    Assert.Equal(correctTrackDbId, reader.GetInt64(2));
                    Assert.Equal(correctPosition, reader.GetInt32(3));
                }
            }

            Assert.Equal(1, rowCount);
        }

        [Fact]
        public void CanDelete()
        {
            var correctPlaylistDbId = 1;
            var otherPlaylistDbId = 2;
            var correctTrackDbId = 1;

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            PrepareOtherTablesData(db);

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderPlaylistTrackCmd();
            cmd.Parameters.AddWithValue("$PlaylistId", correctPlaylistDbId);
            cmd.Parameters.AddWithValue("$TrackId", correctTrackDbId);
            cmd.Parameters.AddWithValue("$Position", 1);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$PlaylistId", otherPlaylistDbId);
            cmd.Parameters.AddWithValue("$TrackId", correctTrackDbId);
            cmd.Parameters.AddWithValue("$Position", 1);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$PlaylistId", correctPlaylistDbId);
            cmd.Parameters.AddWithValue("$TrackId", correctTrackDbId);
            cmd.Parameters.AddWithValue("$Position", 2);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            db.DeleteProviderPlaylistTracks(correctPlaylistDbId);

            cmd.CommandText = "SELECT * FROM ProviderPlaylistTracks";
            cmd.Parameters.Clear();
            var rowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    Assert.Equal(2, reader.GetInt32(0));
                    Assert.Equal(otherPlaylistDbId, reader.GetInt64(1));
                    Assert.Equal(correctTrackDbId, reader.GetInt64(2));
                }
            }

            Assert.Equal(1, rowCount);
        }

        [Fact]
        public void CanRetrieveTracks()
        {
            var correctPlaylistDbId = 1;
            var otherPlaylistDbId = 2;
            var correctTrackDbId = 1;

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            PrepareOtherTablesData(db);

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderPlaylistTrackCmd();
            cmd.Parameters.AddWithValue("$PlaylistId", correctPlaylistDbId);
            cmd.Parameters.AddWithValue("$TrackId", correctTrackDbId);
            cmd.Parameters.AddWithValue("$Position", 0);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$PlaylistId", otherPlaylistDbId);
            cmd.Parameters.AddWithValue("$TrackId", correctTrackDbId);
            cmd.Parameters.AddWithValue("$Position", 0);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$PlaylistId", correctPlaylistDbId);
            cmd.Parameters.AddWithValue("$TrackId", correctTrackDbId);
            cmd.Parameters.AddWithValue("$Position", 1);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            var tracks = db.GetProviderPlaylistTracks(123);
            Assert.Empty(tracks);

            tracks = db.GetProviderPlaylistTracks(correctPlaylistDbId);
            int ii = 0;
            foreach (var (id, pos) in tracks)
            {
                Assert.Equal(correctTrackDbId, id);
                Assert.Equal(ii, pos);
                ii++;
            }
            Assert.NotEmpty(tracks);
        }
    }
}
