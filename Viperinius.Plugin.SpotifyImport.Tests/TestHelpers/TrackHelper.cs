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

        public static void SetValidPluginInstance()
        {
            if (Plugin.Instance == null)
            {
                var mockAppPaths = Substitute.For<MediaBrowser.Common.Configuration.IApplicationPaths>();
                mockAppPaths.PluginsPath.Returns(string.Empty);
                mockAppPaths.PluginConfigurationsPath.Returns(string.Empty);
                var mockXmlSerializer = Substitute.For<MediaBrowser.Model.Serialization.IXmlSerializer>();
                mockXmlSerializer.DeserializeFromFile(Arg.Any<Type>(), Arg.Any<string>())
                                 .Returns(_ => new Configuration.PluginConfiguration());

                _ = new Plugin(mockAppPaths, mockXmlSerializer);
            }
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

        public static (Audio, MusicAlbum, MusicArtist) CreateAllJfItems(string? trackName, string? albumName, string? albumArtist, string? artist)
        {
            var audioItem = CreateJfItem(trackName, albumName, albumArtist, artist);
            var albumItem = AlbumHelper.CreateJfItem(albumName, albumArtist, new List<Audio> { audioItem });
            var artistItem = ArtistHelper.CreateJfItem(artist, new List<MusicAlbum> { albumItem });
            return (audioItem, albumItem, artistItem);
        }

        public static ProviderTrackInfo CreateProviderItem(string trackName, string albumName, List<string> albumArtists, List<string> artists)
        {
            return new ProviderTrackInfo()
            {
                Name = trackName,
                AlbumName = albumName,
                AlbumArtistNames = albumArtists,
                ArtistNames = artists
            };
        }

        public static string GetErrorString(Audio audio)
        {
            return $"at Audio{{Name='{audio.Name}',Album='{audio.AlbumEntity?.Name}',AlbumArtist=[{audio.AlbumEntity?.Artists?.Count}],Artists=[{audio.Artists?.Count}]}}";
        }
    }
}
