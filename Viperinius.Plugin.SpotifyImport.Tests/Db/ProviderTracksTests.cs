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
    public class ProviderTracksTests
    {
        [Fact]
        public void CanRetrieveDbId()
        {
            var correctProviderId = "ABCDEF";
            var correctTrackId = "948basd";

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderTrackCmd();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$TrackId", "2b");
            cmd.Parameters.AddWithValue("$Name", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumName", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$ArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$Number", 0);
            cmd.Parameters.AddWithValue("$IsrcId", string.Empty);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", correctProviderId);
            cmd.Parameters.AddWithValue("$TrackId", correctTrackId);
            cmd.Parameters.AddWithValue("$Name", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumName", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$ArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$Number", 0);
            cmd.Parameters.AddWithValue("$IsrcId", string.Empty);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", "a");
            cmd.Parameters.AddWithValue("$TrackId", "b");
            cmd.Parameters.AddWithValue("$Name", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumName", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$ArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$Number", 0);
            cmd.Parameters.AddWithValue("$IsrcId", string.Empty);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            var id = db.GetProviderTrackDbId(correctProviderId, correctTrackId);
            Assert.NotNull(id);
            Assert.Equal(2, id);

            id = db.GetProviderTrackDbId("not", "existing");
            Assert.Null(id);
        }

        [Fact]
        public void CanInsert()
        {
            var correctProviderId = "ABCDEF";
            var correctTrackId = "948basd";
            var correctName = "diufsbief";
            var correctAlbumName = "%IAIdbfieuvr";
            var correctAlbumArtists = new List<string> { "x", "y" };
            var correctArtists = new List<string> { "m", "n" };
            var correctNumber = 42;
            var correctIsrc = "a4iotbaSD";

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            db.InsertProviderTrack(correctProviderId, new ProviderTrackInfo
            {
                Id = correctTrackId,
                Name = correctName,
                AlbumName = correctAlbumName,
                AlbumArtistNames = correctAlbumArtists,
                ArtistNames = correctArtists,
                TrackNumber = (uint)correctNumber,
                IsrcId = correctIsrc,
            });

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = "SELECT * FROM ProviderTracks";
            var rowCount = 0;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount++;
                    Assert.Equal(1, reader.GetInt32(0));
                    Assert.Equal(correctProviderId, reader.GetString(1));
                    Assert.Equal(correctTrackId, reader.GetString(2));
                    Assert.Equal(correctName, reader.GetString(3));
                    Assert.Equal(correctAlbumName, reader.GetString(4));
                    Assert.Equal(string.Join(DbRepositoryWrapper.InternalArtistListSeparator, correctAlbumArtists), reader.GetString(5));
                    Assert.Equal(string.Join(DbRepositoryWrapper.InternalArtistListSeparator, correctArtists), reader.GetString(6));
                    Assert.Equal(correctNumber, reader.GetInt32(7));
                    Assert.Equal(correctIsrc, reader.GetString(8));
                }
            }

            Assert.Equal(1, rowCount);
        }

        [Fact]
        public void CanRetrieveTrack()
        {
            var correctProviderId = "ABCDEF";
            var correctTrackId = "948basd";
            var correctName = "diufsbief";
            var correctAlbumName = "%IAIdbfieuvr";
            var correctAlbumArtists = new List<string> { "x", "y" };
            var correctArtists = new List<string> { "m", "n" };
            var correctNumber = 42;
            var correctIsrc = "a4iotbaSD";

            using var db = DbRepositoryWrapper.GetInstance();
            db.InitDb();

            using var cmd = db.WrappedConnection.CreateCommand();
            cmd.CommandText = db.GetInsertProviderTrackCmd();
            cmd.Parameters.AddWithValue("$ProviderId", "1a");
            cmd.Parameters.AddWithValue("$TrackId", "2b");
            cmd.Parameters.AddWithValue("$Name", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumName", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$ArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$Number", 0);
            cmd.Parameters.AddWithValue("$IsrcId", string.Empty);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", correctProviderId);
            cmd.Parameters.AddWithValue("$TrackId", correctTrackId);
            cmd.Parameters.AddWithValue("$Name", correctName);
            cmd.Parameters.AddWithValue("$AlbumName", correctAlbumName);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Join(DbRepositoryWrapper.InternalArtistListSeparator, correctAlbumArtists));
            cmd.Parameters.AddWithValue("$ArtistNames", string.Join(DbRepositoryWrapper.InternalArtistListSeparator, correctArtists));
            cmd.Parameters.AddWithValue("$Number", correctNumber);
            cmd.Parameters.AddWithValue("$IsrcId", correctIsrc);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$ProviderId", "a");
            cmd.Parameters.AddWithValue("$TrackId", "b");
            cmd.Parameters.AddWithValue("$Name", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumName", string.Empty);
            cmd.Parameters.AddWithValue("$AlbumArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$ArtistNames", string.Empty);
            cmd.Parameters.AddWithValue("$Number", 0);
            cmd.Parameters.AddWithValue("$IsrcId", string.Empty);
            Assert.Equal(1, cmd.ExecuteNonQuery());

            var track = db.GetProviderTrack(correctProviderId, 123);
            Assert.Null(track);

            track = db.GetProviderTrack("DITASDBerifgaoweidfv", 123);
            Assert.Null(track);

            track = db.GetProviderTrack(correctProviderId, 2);
            Assert.NotNull(track);

            Assert.Equal(correctTrackId, track.Id);
            Assert.Equal(correctName, track.Name);
            Assert.Equal(correctAlbumName, track.AlbumName);
            Assert.Equal(correctAlbumArtists, track.AlbumArtistNames);
            Assert.Equal(correctArtists, track.ArtistNames);
            Assert.Equal((uint)correctNumber, track.TrackNumber);
            Assert.Equal(correctIsrc, track.IsrcId);
        }
    }
}
