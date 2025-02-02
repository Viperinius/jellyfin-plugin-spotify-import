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
    public class ProviderPlaylistsTests
    {
        [Theory]
        [ClassData(typeof(CanRetrieveLastStateData))]
        public void CanRetrieveLastState(bool queryCorrectProvider, bool queryCorrectPlaylist, bool queryNonExistent, bool shouldHaveValue, bool shouldHaveCorrectState)
        {
            var correctProviderId = "ABCDEF";
            var otherProviderId = "xyz";
            var correctPlaylistId = "948basd";
            var otherPlaylistId = "a09hf4ASPd";
            var nonExistentPlaylistId = "adoi4goais";
            var correctState = "hello";
            var otherState = "bye";

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderPlaylistCmd();
            cmd.Parameters.AddWithValue("$ProviderId", correctProviderId);
            cmd.Parameters.AddWithValue("$PlaylistId", correctPlaylistId);
            cmd.Parameters.AddWithValue("$LastState", correctState);
            cmd.Parameters.AddWithValue("$LastTimestamp", DateTime.MinValue);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", otherProviderId);
            cmd.Parameters.AddWithValue("$PlaylistId", otherPlaylistId);
            cmd.Parameters.AddWithValue("$LastState", otherState);
            cmd.Parameters.AddWithValue("$LastTimestamp", DateTime.MinValue);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", otherProviderId);
            cmd.Parameters.AddWithValue("$PlaylistId", correctPlaylistId);
            cmd.Parameters.AddWithValue("$LastState", otherState);
            cmd.Parameters.AddWithValue("$LastTimestamp", DateTime.MinValue);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", correctProviderId);
            cmd.Parameters.AddWithValue("$PlaylistId", otherPlaylistId);
            cmd.Parameters.AddWithValue("$LastState", otherState);
            cmd.Parameters.AddWithValue("$LastTimestamp", DateTime.MinValue);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            var state = db.GetLastProviderPlaylistState(
                queryCorrectProvider ? correctProviderId : otherProviderId,
                queryNonExistent ? nonExistentPlaylistId : (queryCorrectPlaylist ? correctPlaylistId : otherPlaylistId));
            if (shouldHaveValue)
            {
                Assert.NotNull(state);
                Assert.Equal(shouldHaveCorrectState ? correctState : otherState, state);
            }
            else
            {
                Assert.Null(state);
            }
        }

        class CanRetrieveLastStateData : IEnumerable<object[]>
        {
            public IEnumerable<object[]> GetDefault()
            {
                yield return new object[] { true, true, false, true, true };
                yield return new object[] { true, false, false, true, false };
                yield return new object[] { false, true, false, true, false };
                yield return new object[] { false, false, false, true, false };
                yield return new object[] { false, false, true, false, false };
            }

            public virtual IEnumerator<object[]> GetEnumerator() => GetDefault().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void CanRetrieveDbId()
        {
            var correctProviderId = "ABCDEF";
            var correctPlaylistId = "948basd";

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderPlaylistCmd();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$PlaylistId", "2b");
            cmd.Parameters.AddWithValue("$LastState", string.Empty);
            cmd.Parameters.AddWithValue("$LastTimestamp", DateTime.MinValue);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", correctProviderId);
            cmd.Parameters.AddWithValue("$PlaylistId", correctPlaylistId);
            cmd.Parameters.AddWithValue("$LastState", string.Empty);
            cmd.Parameters.AddWithValue("$LastTimestamp", DateTime.MinValue);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", "a");
            cmd.Parameters.AddWithValue("$PlaylistId", "b");
            cmd.Parameters.AddWithValue("$LastState", string.Empty);
            cmd.Parameters.AddWithValue("$LastTimestamp", DateTime.MinValue);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            var id = db.GetProviderPlaylistDbId(correctProviderId, correctPlaylistId);
            Assert.NotNull(id);
            Assert.Equal(2, id);

            id = db.GetProviderPlaylistDbId("not", "existing");
            Assert.Null(id);
        }

        [Fact]
        public void CanInsert()
        {
            var correctProviderId = "ABCDEF";
            var correctPlaylistId = "948basd";
            var correctState = "diufsbief";

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.UpsertProviderPlaylist(correctProviderId, new ProviderPlaylistInfo
            {
                Id = correctPlaylistId,
                State = correctState,
            });

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT * FROM ProviderPlaylists";
            var rowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    Assert.Equal(1, reader.GetInt32(0));
                    Assert.Equal(correctProviderId, reader.GetString(1));
                    Assert.Equal(correctPlaylistId, reader.GetString(2));
                    Assert.Equal(correctState, reader.GetString(3));
                    Assert.InRange(reader.GetDateTime(4), DateTime.UtcNow - TimeSpan.FromSeconds(5), DateTime.UtcNow + TimeSpan.FromSeconds(5));
                }
            }

            Assert.Equal(1, rowCount);
        }

        [Fact]
        public void CanUpdate()
        {
            var correctProviderId = "ABCDEF";
            var correctPlaylistId = "948basd";
            var correctOldState = "F($Asdi7uv";
            var correctState = "diufsbief";

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderPlaylistCmd();
            cmd.Parameters.AddWithValue("$ProviderId", correctProviderId);
            cmd.Parameters.AddWithValue("$PlaylistId", correctPlaylistId);
            cmd.Parameters.AddWithValue("$LastState", correctOldState);
            cmd.Parameters.AddWithValue("$LastTimestamp", DateTime.MinValue);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            db.UpsertProviderPlaylist(correctProviderId, new ProviderPlaylistInfo
            {
                Id = correctPlaylistId,
                State = correctState,
            });

            cmd.CommandText = "SELECT * FROM ProviderPlaylists";
            cmd.Parameters.Clear();
            var rowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    Assert.Equal(1, reader.GetInt32(0));
                    Assert.Equal(correctProviderId, reader.GetString(1));
                    Assert.Equal(correctPlaylistId, reader.GetString(2));
                    Assert.Equal(correctState, reader.GetString(3));
                    Assert.InRange(reader.GetDateTime(4), DateTime.UtcNow - TimeSpan.FromSeconds(5), DateTime.UtcNow + TimeSpan.FromSeconds(5));
                }
            }

            Assert.Equal(1, rowCount);
        }
    }
}
