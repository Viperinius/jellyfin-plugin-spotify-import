using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Viperinius.Plugin.SpotifyImport.Tests.Db;
using Viperinius.Plugin.SpotifyImport.Utils;
using Viperinius.Plugin.SpotifyImport.Utils.MusicBrainz;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Utils
{
    internal class MusicBrainzHelperMock : IMusicBrainzHelper, IDisposable
    {
        public List<DbIsrcMusicBrainzMapping> ResultMappings { get; set; } = new List<DbIsrcMusicBrainzMapping>();

        public List<string> QueryByIsrcLastArgs { get; set; } = new List<string>();

        public async IAsyncEnumerable<DbIsrcMusicBrainzMapping> QueryByIsrc(IEnumerable<string> isrcs, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            QueryByIsrcLastArgs = isrcs.ToList();

            foreach (var isrc in isrcs)
            {
                var result = ResultMappings.FirstOrDefault(m => m!.Isrc == isrc, null);
                if (result != null)
                {
                    yield return result;
                }
            }

            await Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    public class IsrcMappingTests
    {
        private (
            DateTime ValidLastCheck,
            string[] ValidIsrcs,
            string[] NonValidIsrcs,
            int UniqueIsrcCount,
            List<ProviderPlaylistInfo> Playlists,
            MusicBrainzHelperMock MbHelper) SetupTestData()
        {
            var validLastCheck = DateTime.UtcNow.AddMinutes(-5);
            var validIsrcs = new string[] { "abc", "somestring", "hij", "MNO", "123" };
            var nonValidIsrcs = new string[] { "XYZ", "ISRC", "def" };
            var playlists = new List<ProviderPlaylistInfo>
            {
                new ProviderPlaylistInfo
                {
                    Tracks = new List<ProviderTrackInfo>
                    {
                        new() { IsrcId = validIsrcs[0] },
                        new() { IsrcId = null },
                        new() { IsrcId = nonValidIsrcs[0] },
                        new() { IsrcId = validIsrcs[1] },
                        new() { IsrcId = nonValidIsrcs[1] },
                    }
                },
                new ProviderPlaylistInfo
                {
                    Tracks = new List<ProviderTrackInfo>
                    {
                        new() { IsrcId = validIsrcs[1] },
                        new() { IsrcId = nonValidIsrcs[2] },
                        new() { IsrcId = validIsrcs[2] },
                        new() { IsrcId = validIsrcs[3] },
                        new() { IsrcId = validIsrcs[4] },
                    }
                }
            };
            var uniqueIsrcCount = playlists.SelectMany(p => p.Tracks.Select(t => t.IsrcId).Where(i => i != null)).Distinct().Count();

            var mbHelper = new MusicBrainzHelperMock();
            // prepare some dummy "api" values
            mbHelper.ResultMappings = new List<DbIsrcMusicBrainzMapping>();
            var rnd = new Random();
            for (int ii = 0; ii < validIsrcs.Length; ii++)
            {
                var recs = new List<Guid>();
                var rels = new List<Guid>();
                var relGrps = new List<Guid>();
                for (int iRec = 0; iRec < rnd.Next(1, 5); iRec++)
                {
                    recs.Add(Guid.NewGuid());
                }
                for (int iRel = 0; iRel < rnd.Next(1, 5); iRel++)
                {
                    rels.Add(Guid.NewGuid());
                }
                for (int iRel = 0; iRel < rnd.Next(0, 5); iRel++)
                {
                    relGrps.Add(Guid.NewGuid());
                }

                mbHelper.ResultMappings.Add(new DbIsrcMusicBrainzMapping(ii + 10, validIsrcs[ii], validLastCheck, recs, rels, relGrps));
            }

            return (validLastCheck, validIsrcs, nonValidIsrcs, uniqueIsrcCount, playlists, mbHelper);
        }

        [Fact]
        public async Task CanUpdateMappingsInit()
        {
            var loggerMock = Substitute.For<ILogger<IsrcMapping>>();
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var testData = SetupTestData();

            foreach (var isrc in testData.ValidIsrcs)
            {
                Assert.Empty(db.GetIsrcMusicBrainzMapping(isrc));
            }

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM IsrcMusicBrainzChecks";
            var rowCount = cmd.ExecuteScalar();
            Assert.Equal(0, (long?)rowCount);

            var isrcMapping = new IsrcMapping(loggerMock, db, testData.MbHelper);
            await isrcMapping.UpdateIsrcMusicBrainzMappings(testData.Playlists);

            Assert.Equal(testData.UniqueIsrcCount, testData.MbHelper.QueryByIsrcLastArgs.Count);
            testData.MbHelper.QueryByIsrcLastArgs.Sort();
            List<string> expectedArgs = [.. testData.ValidIsrcs, .. testData.NonValidIsrcs];
            expectedArgs.Sort();
            Assert.True(testData.MbHelper.QueryByIsrcLastArgs.SequenceEqual(expectedArgs));

            rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount, (long?)rowCount);

            // "invalid" isrcs should only be saved as placeholders using current timestamp
            foreach (var isrc in testData.NonValidIsrcs)
            {
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    Assert.Equal(isrc, item.Isrc);
                    Assert.Empty(item.MusicBrainzRecordingIds);
                    Assert.Empty(item.MusicBrainzReleaseIds);
                    Assert.Empty(item.MusicBrainzReleaseGroupIds);
                    Assert.True(item.LastCheck > testData.ValidLastCheck);
                }
            }

            foreach (var isrc in testData.ValidIsrcs)
            {
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    var result = testData.MbHelper.ResultMappings.Find(m => m.Isrc == item.Isrc
                                                                         && m.MusicBrainzRecordingIds.SequenceEqual(item.MusicBrainzRecordingIds)
                                                                         && m.MusicBrainzReleaseIds.SequenceEqual(item.MusicBrainzReleaseIds)
                                                                         && m.MusicBrainzReleaseGroupIds.SequenceEqual(item.MusicBrainzReleaseGroupIds)
                                                                         && m.LastCheck == item.LastCheck);
                    Assert.NotNull(result);
                }
            }
        }

        [Fact]
        public async Task CanUpdateMappingsNothingToBeDone()
        {
            var loggerMock = Substitute.For<ILogger<IsrcMapping>>();
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var testData = SetupTestData();

            foreach (var mapping in testData.MbHelper.ResultMappings)
            {
                Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(mapping));
            }
            foreach (var isrc in testData.NonValidIsrcs)
            {
                Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, isrc, testData.ValidLastCheck, [], [], [])));
            }

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM IsrcMusicBrainzChecks";
            var rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount, (long?)rowCount);

            var isrcMapping = new IsrcMapping(loggerMock, db, testData.MbHelper);
            await isrcMapping.UpdateIsrcMusicBrainzMappings(testData.Playlists);

            Assert.Empty(testData.MbHelper.QueryByIsrcLastArgs);

            rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount, (long?)rowCount);

            foreach (var isrc in testData.NonValidIsrcs)
            {
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    Assert.Equal(isrc, item.Isrc);
                    Assert.Empty(item.MusicBrainzRecordingIds);
                    Assert.Empty(item.MusicBrainzReleaseIds);
                    Assert.Empty(item.MusicBrainzReleaseGroupIds);
                    Assert.Equal(testData.ValidLastCheck, item.LastCheck);
                }
            }

            foreach (var isrc in testData.ValidIsrcs)
            {
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    var result = testData.MbHelper.ResultMappings.Find(m => m.Isrc == item.Isrc
                                                                         && m.MusicBrainzRecordingIds.SequenceEqual(item.MusicBrainzRecordingIds)
                                                                         && m.MusicBrainzReleaseIds.SequenceEqual(item.MusicBrainzReleaseIds)
                                                                         && m.MusicBrainzReleaseGroupIds.SequenceEqual(item.MusicBrainzReleaseGroupIds)
                                                                         && m.LastCheck == item.LastCheck);
                    Assert.NotNull(result);
                }
            }
        }

        [Fact]
        public async Task CanUpdateMappingsRecheckAfterTime()
        {
            var loggerMock = Substitute.For<ILogger<IsrcMapping>>();
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var testData = SetupTestData();

            var expectedQueriedNonValidIsrcIndex = 0;

            foreach (var mapping in testData.MbHelper.ResultMappings)
            {
                Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(mapping));
            }
            for(int ii = 0; ii < testData.NonValidIsrcs.Length; ii++)
            {
                var isrc = testData.NonValidIsrcs[ii];
                var lastCheck = ii == expectedQueriedNonValidIsrcIndex ? testData.ValidLastCheck.AddDays(-30) : testData.ValidLastCheck;
                Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, isrc, lastCheck, [], [], [])));
            }

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM IsrcMusicBrainzChecks";
            var rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount, (long?)rowCount);

            var isrcMapping = new IsrcMapping(loggerMock, db, testData.MbHelper);
            await isrcMapping.UpdateIsrcMusicBrainzMappings(testData.Playlists);

            rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount, (long?)rowCount);

            Assert.Single(testData.MbHelper.QueryByIsrcLastArgs);
            Assert.Equal(testData.NonValidIsrcs[expectedQueriedNonValidIsrcIndex], testData.MbHelper.QueryByIsrcLastArgs[0]);

            for (int ii = 0; ii < testData.NonValidIsrcs.Length; ii++)
            {
                var isrc = testData.NonValidIsrcs[ii];
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    Assert.Equal(isrc, item.Isrc);
                    Assert.Empty(item.MusicBrainzRecordingIds);
                    Assert.Empty(item.MusicBrainzReleaseIds);
                    Assert.Empty(item.MusicBrainzReleaseGroupIds);
                    if (ii == expectedQueriedNonValidIsrcIndex)
                    {
                        Assert.InRange(item.LastCheck, testData.ValidLastCheck, DateTime.UtcNow);
                    }
                    else
                    {
                        Assert.Equal(testData.ValidLastCheck, item.LastCheck);
                    }
                }
            }

            foreach (var isrc in testData.ValidIsrcs)
            {
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    var result = testData.MbHelper.ResultMappings.Find(m => m.Isrc == item.Isrc
                                                                         && m.MusicBrainzRecordingIds.SequenceEqual(item.MusicBrainzRecordingIds)
                                                                         && m.MusicBrainzReleaseIds.SequenceEqual(item.MusicBrainzReleaseIds)
                                                                         && m.MusicBrainzReleaseGroupIds.SequenceEqual(item.MusicBrainzReleaseGroupIds)
                                                                         && m.LastCheck == item.LastCheck);
                    Assert.NotNull(result);
                }
            }
        }

        [Fact]
        public async Task CanUpdateMappingsDontRecheckDone()
        {
            var loggerMock = Substitute.For<ILogger<IsrcMapping>>();
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var testData = SetupTestData();

            for (int ii = 0; ii < testData.MbHelper.ResultMappings.Count; ii++)
            {
                var mapping = testData.MbHelper.ResultMappings[ii];
                Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(mapping));
            }
            for (int ii = 0; ii < testData.NonValidIsrcs.Length; ii++)
            {
                var isrc = testData.NonValidIsrcs[ii];
                var lastCheck = testData.ValidLastCheck;
                Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, isrc, lastCheck, [], [], [])));
            }

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM IsrcMusicBrainzChecks";
            var rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount, (long?)rowCount);

            var isrcMapping = new IsrcMapping(loggerMock, db, testData.MbHelper);
            await isrcMapping.UpdateIsrcMusicBrainzMappings(testData.Playlists);

            rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount, (long?)rowCount);

            Assert.Empty(testData.MbHelper.QueryByIsrcLastArgs);

            foreach (var isrc in testData.NonValidIsrcs)
            {
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    Assert.Equal(isrc, item.Isrc);
                    Assert.Empty(item.MusicBrainzRecordingIds);
                    Assert.Empty(item.MusicBrainzReleaseIds);
                    Assert.Empty(item.MusicBrainzReleaseGroupIds);
                    Assert.Equal(testData.ValidLastCheck, item.LastCheck);
                }
            }

            foreach (var isrc in testData.ValidIsrcs)
            {
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    var result = testData.MbHelper.ResultMappings.Find(m => m.Isrc == item.Isrc
                                                                         && m.MusicBrainzRecordingIds.SequenceEqual(item.MusicBrainzRecordingIds)
                                                                         && m.MusicBrainzReleaseIds.SequenceEqual(item.MusicBrainzReleaseIds)
                                                                         && m.MusicBrainzReleaseGroupIds.SequenceEqual(item.MusicBrainzReleaseGroupIds)
                                                                         && m.LastCheck == item.LastCheck);
                    Assert.NotNull(result);
                }
            }
        }

        [Fact]
        public async Task CanUpdateMappingsAddNewEntry()
        {
            var loggerMock = Substitute.For<ILogger<IsrcMapping>>();
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var testData = SetupTestData();

            var expectedQueriedValidIsrcIndex = 1;
            var expectedQueriedNonValidIsrcIndex = 0;

            for (int ii = 0; ii < testData.MbHelper.ResultMappings.Count; ii++)
            {
                if (ii == expectedQueriedValidIsrcIndex)
                {
                    continue;
                }

                var mapping = testData.MbHelper.ResultMappings[ii];
                Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(mapping));
            }
            for (int ii = 0; ii < testData.NonValidIsrcs.Length; ii++)
            {
                if (ii == expectedQueriedNonValidIsrcIndex)
                {
                    continue;
                }

                var isrc = testData.NonValidIsrcs[ii];
                Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, isrc, testData.ValidLastCheck, [], [], [])));
            }

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM IsrcMusicBrainzChecks";
            var rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount - 2, (long?)rowCount);

            var isrcMapping = new IsrcMapping(loggerMock, db, testData.MbHelper);
            await isrcMapping.UpdateIsrcMusicBrainzMappings(testData.Playlists);

            rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount, (long?)rowCount);

            Assert.Equal(2, testData.MbHelper.QueryByIsrcLastArgs.Count);
            Assert.Contains(testData.ValidIsrcs[expectedQueriedValidIsrcIndex], testData.MbHelper.QueryByIsrcLastArgs);
            Assert.Contains(testData.NonValidIsrcs[expectedQueriedNonValidIsrcIndex], testData.MbHelper.QueryByIsrcLastArgs);

            for (int ii = 0; ii < testData.NonValidIsrcs.Length; ii++)
            {
                var isrc = testData.NonValidIsrcs[ii];
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    Assert.Equal(isrc, item.Isrc);
                    Assert.Empty(item.MusicBrainzRecordingIds);
                    Assert.Empty(item.MusicBrainzReleaseIds);
                    Assert.Empty(item.MusicBrainzReleaseGroupIds);
                    if (ii == expectedQueriedNonValidIsrcIndex)
                    {
                        Assert.InRange(item.LastCheck, testData.ValidLastCheck.AddSeconds(1), DateTime.UtcNow);
                    }
                    else
                    {
                        Assert.Equal(testData.ValidLastCheck, item.LastCheck);
                    }
                }
            }

            for (int ii = 0; ii < testData.ValidIsrcs.Length; ii++)
            {
                var isrc = testData.ValidIsrcs[ii];
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    var result = testData.MbHelper.ResultMappings.Find(m => m.Isrc == item.Isrc
                                                                         && m.MusicBrainzRecordingIds.SequenceEqual(item.MusicBrainzRecordingIds)
                                                                         && m.MusicBrainzReleaseIds.SequenceEqual(item.MusicBrainzReleaseIds)
                                                                         && m.MusicBrainzReleaseGroupIds.SequenceEqual(item.MusicBrainzReleaseGroupIds)
                                                                         && m.LastCheck == item.LastCheck);
                    Assert.NotNull(result);
                }
            }
        }

        [Fact]
        public async Task CanUpdateMappingsReplacePlaceholderWithValid()
        {
            var loggerMock = Substitute.For<ILogger<IsrcMapping>>();
            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            var testData = SetupTestData();

            var expectedQueriedValidIsrcIndex = 1;

            for (int ii = 0; ii < testData.MbHelper.ResultMappings.Count; ii++)
            {
                if (ii == expectedQueriedValidIsrcIndex)
                {
                    continue;
                }

                var mapping = testData.MbHelper.ResultMappings[ii];
                Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(mapping));
            }
            foreach (var isrc in testData.NonValidIsrcs)
            {
                Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, isrc, testData.ValidLastCheck, [], [], [])));
            }

            Assert.NotNull(db.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, testData.ValidIsrcs[expectedQueriedValidIsrcIndex], testData.ValidLastCheck.AddDays(-30), [], [], [])));

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM IsrcMusicBrainzChecks";
            var rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount, (long?)rowCount);

            var isrcMapping = new IsrcMapping(loggerMock, db, testData.MbHelper);
            await isrcMapping.UpdateIsrcMusicBrainzMappings(testData.Playlists);

            rowCount = cmd.ExecuteScalar();
            Assert.Equal(testData.UniqueIsrcCount, (long?)rowCount);

            Assert.Single(testData.MbHelper.QueryByIsrcLastArgs);
            Assert.Contains(testData.ValidIsrcs[expectedQueriedValidIsrcIndex], testData.MbHelper.QueryByIsrcLastArgs);

            foreach (var isrc in testData.NonValidIsrcs)
            {
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    Assert.Equal(isrc, item.Isrc);
                    Assert.Empty(item.MusicBrainzRecordingIds);
                    Assert.Empty(item.MusicBrainzReleaseIds);
                    Assert.Empty(item.MusicBrainzReleaseGroupIds);
                    Assert.Equal(testData.ValidLastCheck, item.LastCheck);
                }
            }

            for (int ii = 0; ii < testData.ValidIsrcs.Length; ii++)
            {
                var isrc = testData.ValidIsrcs[ii];
                var found = db.GetIsrcMusicBrainzMapping(isrc);
                Assert.NotEmpty(found);
                foreach (var item in found)
                {
                    var result = testData.MbHelper.ResultMappings.Find(m => m.Isrc == item.Isrc
                                                                         && m.MusicBrainzRecordingIds.SequenceEqual(item.MusicBrainzRecordingIds)
                                                                         && m.MusicBrainzReleaseIds.SequenceEqual(item.MusicBrainzReleaseIds)
                                                                         && m.MusicBrainzReleaseGroupIds.SequenceEqual(item.MusicBrainzReleaseGroupIds)
                                                                         && m.LastCheck == item.LastCheck);
                    Assert.NotNull(result);
                }
            }
        }
    }
}
