#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Db
{
    public class ProviderTrackMatchesTests
    {
        private void PrepareOtherTablesData(DbRepositoryWrapper db)
        {
            using var cmd = db.WrappedConnection.CreateCommand();
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

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$TrackId", "3a");
            cmd.Parameters.AddWithValue("$Name", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumName", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$ArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$Number", 0);
            cmd.Parameters.AddWithValue("$IsrcId", string.Empty);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$TrackId", "4a");
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
            var correctTrackDbId = 1;
            var correctJfId = Guid.NewGuid();
            var correctMatchLevel = ItemMatchLevel.IgnorePunctuationAndCase;
            var correctMatchCriteria = ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists;

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            PrepareOtherTablesData(db);

            db.InsertProviderTrackMatch(correctTrackDbId, correctJfId.ToString(), correctMatchLevel, correctMatchCriteria);

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT * FROM ProviderTrackMatches";
            var rowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    Assert.Equal(1, reader.GetInt32(0));
                    Assert.Equal(correctTrackDbId, reader.GetInt64(1));
                    Assert.Equal(correctJfId, reader.GetGuid(2));
                    Assert.Equal(correctMatchLevel, (ItemMatchLevel)reader.GetInt32(3));
                    Assert.Equal(correctMatchCriteria, (ItemMatchCriteria)reader.GetInt32(4));
                }
            }

            Assert.Equal(1, rowCount);
        }

        [Fact]
        public void CanRetrieveTracks()
        {
            var correctTrackDbId = 1;
            var correctJfId = Guid.NewGuid();
            var correctMatchLevel = ItemMatchLevel.IgnorePunctuationAndCase;
            var correctMatchCriteria = ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumArtists;

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            PrepareOtherTablesData(db);

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderTrackMatchCmd();
            cmd.Parameters.AddWithValue("$TrackId", 2);
            cmd.Parameters.AddWithValue("$JellyfinMatchId", correctJfId);
            cmd.Parameters.AddWithValue("$MatchLevel", ItemMatchLevel.Fuzzy);
            cmd.Parameters.AddWithValue("$MatchCriteria", correctMatchCriteria);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$TrackId", correctTrackDbId);
            cmd.Parameters.AddWithValue("$JellyfinMatchId", correctJfId);
            cmd.Parameters.AddWithValue("$MatchLevel", correctMatchLevel);
            cmd.Parameters.AddWithValue("$MatchCriteria", correctMatchCriteria);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$TrackId", 3);
            cmd.Parameters.AddWithValue("$JellyfinMatchId", "xyz");
            cmd.Parameters.AddWithValue("$MatchLevel", 0);
            cmd.Parameters.AddWithValue("$MatchCriteria", 0);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            var tracks = db.GetProviderTrackMatch(123);
            Assert.Empty(tracks);

            tracks = db.GetProviderTrackMatch(correctTrackDbId);
            foreach (var track in tracks)
            {
                Assert.Equal(correctJfId, track.MatchId);
                Assert.Equal(correctMatchLevel, track.Level);
                Assert.Equal(correctMatchCriteria, track.Criteria);
            }
            Assert.NotEmpty(tracks);
        }
    }
}
