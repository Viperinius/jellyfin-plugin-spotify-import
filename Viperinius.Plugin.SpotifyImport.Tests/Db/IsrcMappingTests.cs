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
            var correctMbRecIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var correctMbRelIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var correctMbRelGrpIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var correctLastCheck = DateTime.UtcNow;

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(
                id: -1,
                isrc: correctIsrc,
                mbRecordingIds: correctMbRecIds,
                mbReleaseIds: correctMbRelIds,
                mbReleaseGroupIds: correctMbRelGrpIds,
                lastCheck: correctLastCheck
            ));

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT * FROM IsrcMusicBrainzChecks";
            var rowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    Assert.Equal(1, reader.GetInt32(0));
                    Assert.Equal(correctIsrc, reader.GetString(1));
                    Assert.Equal(correctLastCheck, reader.GetDateTime(2));
                }
            }

            Assert.Equal(1, rowCount);

            rowCount = 0;
            cmd.CommandText = "SELECT * FROM IsrcMusicBrainzRecordingMapping";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    var index = reader.GetInt32(0);
                    Assert.Equal(correctIsrc, reader.GetString(1));
                    var guid = reader.GetGuid(2);
                    Assert.Equal(correctMbRecIds.IndexOf(guid), index - 1);
                }
            }

            Assert.Equal(correctMbRecIds.Count, rowCount);

            rowCount = 0;
            cmd.CommandText = "SELECT * FROM IsrcMusicBrainzReleaseMapping";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    var index = reader.GetInt32(0);
                    Assert.Equal(correctIsrc, reader.GetString(1));
                    var guid = reader.GetGuid(2);
                    Assert.Equal(correctMbRelIds.IndexOf(guid), index - 1);
                }
            }

            Assert.Equal(correctMbRelIds.Count, rowCount);

            rowCount = 0;
            cmd.CommandText = "SELECT * FROM IsrcMusicBrainzReleaseGroupMapping";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    var index = reader.GetInt32(0);
                    Assert.Equal(correctIsrc, reader.GetString(1));
                    var guid = reader.GetGuid(2);
                    Assert.Equal(correctMbRelGrpIds.IndexOf(guid), index - 1);
                }
            }

            Assert.Equal(correctMbRelGrpIds.Count, rowCount);
        }

        [Fact]
        public void CanRetrieve()
        {
            var correctIsrc = "a4iotbaSD";
            var correctMbRecIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var correctMbRelIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var correctMbRelGrpIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var correctLastCheck = DateTime.UtcNow.AddMinutes(-1);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var dbId = db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, "x", DateTime.UtcNow.AddHours(4), [Guid.NewGuid()], [], [Guid.NewGuid()]));
            Assert.NotNull(dbId);

            dbId = db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, correctIsrc, correctLastCheck, correctMbRecIds, correctMbRelIds, correctMbRelGrpIds));
            Assert.NotNull(dbId);

            dbId = db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, "y", DateTime.UtcNow.AddHours(3), [Guid.NewGuid()], [Guid.NewGuid()], []));
            Assert.NotNull(dbId);

            var mappings = db.GetIsrcMusicBrainzMapping();
            Assert.Equal(3, mappings.Count());

            mappings = db.GetIsrcMusicBrainzMapping(isrc: "i94rgaser");
            Assert.Empty(mappings);

            mappings = db.GetIsrcMusicBrainzMapping(isrc: correctIsrc);
            Assert.Single(mappings);

            Assert.Equal(correctIsrc, mappings.ElementAt(0).Isrc);
            Assert.Equal(correctMbRecIds, mappings.ElementAt(0).MusicBrainzRecordingIds);
            Assert.Equal(correctMbRelIds, mappings.ElementAt(0).MusicBrainzReleaseIds);
            Assert.Equal(correctMbRelGrpIds, mappings.ElementAt(0).MusicBrainzReleaseGroupIds);
            Assert.Equal(correctLastCheck, mappings.ElementAt(0).LastCheck);
        }

        [Fact]
        public void CanDelete()
        {
            var correctIsrc = "a4iotbaSD";
            var correctMbRecIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var correctMbRelIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var correctMbRelGrpIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var correctLastCheck = DateTime.UtcNow.AddMinutes(-1);

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var dbId = db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, "x", DateTime.UtcNow.AddHours(4), [Guid.NewGuid()], [], [Guid.NewGuid()]));
            Assert.NotNull(dbId);

            dbId = db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, correctIsrc, correctLastCheck, correctMbRecIds, correctMbRelIds, correctMbRelGrpIds));
            Assert.NotNull(dbId);

            dbId = db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, "y", DateTime.UtcNow.AddHours(3), [Guid.NewGuid()], [Guid.NewGuid()], []));
            Assert.NotNull(dbId);

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
