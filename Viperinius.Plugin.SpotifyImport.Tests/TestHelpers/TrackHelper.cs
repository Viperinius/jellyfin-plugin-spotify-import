using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using NSubstitute;
using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Viperinius.Plugin.SpotifyImport.Tests.TestHelpers
{
    internal static class TrackHelper
    {
        private static readonly Dictionary<Guid, MusicAlbum> _albums;
        private static readonly ILibraryManager _libManagerMock;

        static TrackHelper()
        {
            _albums = new Dictionary<Guid, MusicAlbum>();
            _libManagerMock = Substitute.For<ILibraryManager>();
#pragma warning disable CS8603 // null return
            _libManagerMock.GetItemById(Arg.Any<Guid>()).Returns(args => _albums.TryGetValue((Guid)args[0], out var result) ? result : null);
#pragma warning restore CS8603 // null return
            BaseItem.LibraryManager = _libManagerMock;
            System.Threading.Thread.Sleep(100);
        }

        public static void ClearAlbums()
        {
            _albums.Clear();
        }

        public static Audio CreateJfItem(string? trackName, string? albumName, string? albumArtist, string? artist)
        {
            var audio = new Audio()
            {
                Name = trackName
            };

            if (albumName != null || albumArtist != null)
            {
                var album = new MusicAlbum();
                album.Name = albumName;
                if (albumArtist != null)
                {
                    album.Artists = new List<string> { "abc", albumArtist, "lius987grvalsiuRHH" };
                }

                var albumId = Guid.NewGuid();
                audio.ParentId = albumId;
                _albums.Add(albumId, album);
            }

            if (artist != null)
            {
                audio.Artists = new List<string> { "abc", artist, "dwoeirg87fadaDUG$ASD" };
            }

            return audio;
        }

        public static ProviderTrackInfo CreateProviderItem(string trackName, string albumName, string albumArtist, string artist)
        {
            return new ProviderTrackInfo()
            {
                Name = trackName,
                AlbumName = albumName,
                AlbumArtistName = albumArtist,
                ArtistName = artist
            };
        }

        public static string GetErrorString(Audio audio)
        {
            return $"at Audio{{Name='{audio.Name}',Album='{audio.AlbumEntity?.Name}',AlbumArtist=[{audio.AlbumEntity?.Artists?.Count}],Artists=[{audio.Artists?.Count}]}}";
        }
    }
}
