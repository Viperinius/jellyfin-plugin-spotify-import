#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Viperinius.Plugin.SpotifyImport.Utils;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Db
{
    public class IsrcMappingTests
    {
        [Fact]
        public void CanInsert()
        {
            var correctIsrc = "a4iotbaSD";
            var correctMbRelId = Guid.NewGuid();
            var correctMbRelGrpId = Guid.NewGuid();
            var correctLastCheck = DateTime.UtcNow;

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(
                id: -1,
                isrc: correctIsrc,
                mbReleaseId: correctMbRelId,
                mbReleaseGroupId: correctMbRelGrpId,
                lastCheck: correctLastCheck
            ));

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT * FROM IsrcMusicBrainzMapping";
            var rowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    Assert.Equal(1, reader.GetInt32(0));
                    Assert.Equal(correctIsrc, reader.GetString(1));
                    Assert.Equal(correctMbRelId, reader.GetGuid(2));
                    Assert.Equal(correctMbRelGrpId, reader.GetGuid(3));
                    Assert.Equal(correctLastCheck, reader.GetDateTime(4));
                }
            }

            Assert.Equal(1, rowCount);
        }

        [Fact]
        public void CanRetrieve()
        {
            var correctIsrc = "a4iotbaSD";
            var correctMbRelId = Guid.NewGuid();
            var correctMbRelGrpId = Guid.NewGuid();
            var correctLastCheck = DateTime.UtcNow.AddMinutes(-1);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertIsrcMusicBrainzCmd();
            cmd.Parameters.AddWithValue("$Isrc", "x");
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseId", DBNull.Value);
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseGroupId", Guid.NewGuid());
            cmd.Parameters.AddWithValue("$LastCheck", DateTime.UtcNow.AddHours(4));
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$Isrc", correctIsrc);
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseId", correctMbRelId);
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseGroupId", correctMbRelGrpId);
            cmd.Parameters.AddWithValue("$LastCheck", correctLastCheck);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$Isrc", "y");
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseId", Guid.NewGuid());
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseGroupId", DBNull.Value);
            cmd.Parameters.AddWithValue("$LastCheck", DateTime.UtcNow.AddHours(3));
            Assert.Equal(1, cmd.ExecuteNonQuery());

            var mappings = db.GetIsrcMusicBrainzMapping();
            Assert.Equal(3, mappings.Count());

            mappings = db.GetIsrcMusicBrainzMapping(isrc: "i94rgaser");
            Assert.Empty(mappings);

            mappings = db.GetIsrcMusicBrainzMapping(isrc: correctIsrc);
            Assert.Single(mappings);

            Assert.Equal(correctIsrc, mappings.ElementAt(0).Isrc);
            Assert.Equal(correctMbRelId, mappings.ElementAt(0).MusicBrainzReleaseId);
            Assert.Equal(correctMbRelGrpId, mappings.ElementAt(0).MusicBrainzReleaseGroupId);
            Assert.Equal(correctLastCheck, mappings.ElementAt(0).LastCheck);
        }

        [Fact]
        public void CanDelete()
        {
            var correctIsrc = "a4iotbaSD";
            var correctMbRelId = Guid.NewGuid();
            var correctMbRelGrpId = Guid.NewGuid();
            var correctLastCheck = DateTime.UtcNow.AddMinutes(-1);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertIsrcMusicBrainzCmd();
            cmd.Parameters.AddWithValue("$Isrc", "x");
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseId", DBNull.Value);
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseGroupId", Guid.NewGuid());
            cmd.Parameters.AddWithValue("$LastCheck", DateTime.UtcNow.AddHours(4));
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$Isrc", correctIsrc);
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseId", correctMbRelId);
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseGroupId", correctMbRelGrpId);
            cmd.Parameters.AddWithValue("$LastCheck", correctLastCheck);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$Isrc", "y");
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseId", Guid.NewGuid());
            cmd.Parameters.AddWithValue("$MusicBrainzReleaseGroupId", DBNull.Value);
            cmd.Parameters.AddWithValue("$LastCheck", DateTime.UtcNow.AddHours(3));
            Assert.Equal(1, cmd.ExecuteNonQuery());

            Assert.True(db.DeleteIsrcMusicBrainzMapping(new List<long>()));
            var mappings = db.GetIsrcMusicBrainzMapping();
            Assert.Equal(3, mappings.Count());

            Assert.False(db.DeleteIsrcMusicBrainzMapping(new List<long> { 123 }));
            mappings = db.GetIsrcMusicBrainzMapping();
            Assert.Equal(3, mappings.Count());

            Assert.True(db.DeleteIsrcMusicBrainzMapping(new List<long> { 2 }));
            mappings = db.GetIsrcMusicBrainzMapping();
            Assert.Equal(2, mappings.Count());

            Assert.DoesNotContain(mappings, m => m.Isrc == correctIsrc);
        }
    }
}
