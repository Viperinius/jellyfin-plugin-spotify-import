using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport
{
    [Serializable]
    internal class ManualMapTrack
    {
        public ManualMapTrack()
        {
            Jellyfin = new JellyfinTrack();
            Provider = new ProviderTrack();
        }

        public JellyfinTrack Jellyfin { get; set; }

        public ProviderTrack Provider { get; set; }

        [Serializable]
        internal class JellyfinTrack
        {
            public JellyfinTrack()
            {
                Track = string.Empty;
            }

            public string Track { get; set; }
        }

        [Serializable]
        internal class ProviderTrack : IEquatable<ProviderTrackInfo>
        {
            public ProviderTrack()
            {
                Name = string.Empty;
                AlbumName = string.Empty;
                AlbumArtistNames = new List<string>();
                ArtistNames = new List<string>();
            }

            public string Name { get; set; }

            public string AlbumName { get; set; }

            public List<string> AlbumArtistNames { get; set; }

            public List<string> ArtistNames { get; set; }

            public bool Equals(ProviderTrackInfo? other)
            {
                if (other == null)
                {
                    return false;
                }

                return Name == other.Name &&
                       AlbumName == other.AlbumName &&
                       Enumerable.SequenceEqual(AlbumArtistNames, other.AlbumArtistNames) &&
                       Enumerable.SequenceEqual(ArtistNames, other.ArtistNames);
            }

            public override bool Equals(object? obj)
            {
                if (obj == null)
                {
                    return false;
                }

                if (obj is ProviderTrackInfo info)
                {
                    return Equals(info);
                }

                if (obj is ProviderTrack other)
                {
                    return Name == other.Name &&
                           AlbumName == other.AlbumName &&
                           AlbumArtistNames == other.AlbumArtistNames &&
                           ArtistNames == other.ArtistNames;
                }

                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Name, AlbumName, AlbumArtistNames, ArtistNames);
            }
        }
    }
}
