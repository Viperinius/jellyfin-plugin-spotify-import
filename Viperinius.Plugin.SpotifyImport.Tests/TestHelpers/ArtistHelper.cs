using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;
using NSubstitute;

namespace Viperinius.Plugin.SpotifyImport.Tests.TestHelpers
{
    internal static class ArtistHelper
    {
        public static MusicArtist CreateJfItem(string? name, List<MusicAlbum>? albums)
        {
            var artist = Substitute.ForPartsOf<MusicArtist>();
            artist.Name = name;

            if (albums != null)
            {
                artist.Children.Returns(albums);
            }

            return artist;
        }

        public static string GetErrorString(MusicArtist artist)
        {
            return $"at MusicArtist{{Name='{artist.Name}',Albums=[{artist.Children.Count()}]}}";
        }
    }
}
