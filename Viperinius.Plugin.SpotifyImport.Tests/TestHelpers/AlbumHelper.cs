using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;
using NSubstitute;

namespace Viperinius.Plugin.SpotifyImport.Tests.TestHelpers
{
    internal class AlbumHelper
    {
        public static MusicAlbum CreateJfItem(string? name, string? artistName, List<Audio>? tracks)
        {
            var album = Substitute.For<MusicAlbum>();
            album.Name = name;

            if (artistName != null)
            {
                album.Artists = new List<string> { artistName };
            }

            if (tracks != null)
            {
                album.Children.Returns(tracks);
                album.Tracks.Returns(tracks);
            }

            return album;
        }

        public static string GetErrorString(MusicAlbum album)
        {
            return $"at MusicAlbum{{Name='{album.Name}',Tracks=[{album.Tracks.Count()}]}}";
        }
    }
}
