#pragma warning disable CA1002 // Do not expose generic lists
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA2227 // Collection properties should be read only

using System;
using System.Collections.Generic;
using System.Linq;

namespace Viperinius.Plugin.SpotifyImport
{
    /// <summary>
    /// Map entry for manually mapping tracks.
    /// </summary>
    [Serializable]
    public class ManualMapTrack
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ManualMapTrack"/> class.
        /// </summary>
        public ManualMapTrack()
        {
            Jellyfin = new JellyfinTrack();
            Provider = new ProviderTrack();
        }

        /// <summary>
        /// Gets or sets the jellyfin part.
        /// </summary>
        public JellyfinTrack Jellyfin { get; set; }

        /// <summary>
        /// Gets or sets the provider part.
        /// </summary>
        public ProviderTrack Provider { get; set; }

        /// <summary>
        /// Represents needed info for the track in Jellyfin.
        /// </summary>
        [Serializable]
        public class JellyfinTrack
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="JellyfinTrack"/> class.
            /// </summary>
            public JellyfinTrack()
            {
                Track = string.Empty;
            }

            /// <summary>
            /// Gets or sets the track id.
            /// </summary>
            public string Track { get; set; }
        }

        /// <summary>
        /// Represents needed info for the track by the provider.
        /// </summary>
        [Serializable]
        public class ProviderTrack : IEquatable<ProviderTrackInfo>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ProviderTrack"/> class.
            /// </summary>
            public ProviderTrack()
            {
                Id = string.Empty;
                Name = string.Empty;
                AlbumName = string.Empty;
                AlbumArtistNames = new List<string>();
                ArtistNames = new List<string>();
            }

            /// <summary>
            /// Gets or sets the track id.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// Gets or sets the track name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the album name.
            /// </summary>
            public string AlbumName { get; set; }

            /// <summary>
            /// Gets or sets the album artists.
            /// </summary>
            public List<string> AlbumArtistNames { get; set; }

            /// <summary>
            /// Gets or sets the artists.
            /// </summary>
            public List<string> ArtistNames { get; set; }

            /// <inheritdoc/>
            public bool Equals(ProviderTrackInfo? other)
            {
                if (other == null)
                {
                    return false;
                }

                return (!string.IsNullOrEmpty(Id) && Id == other.Id) ||
                       (Name == other.Name &&
                       AlbumName == other.AlbumName &&
                       Enumerable.SequenceEqual(AlbumArtistNames, other.AlbumArtistNames) &&
                       Enumerable.SequenceEqual(ArtistNames, other.ArtistNames));
            }

            /// <inheritdoc/>
            public override bool Equals(object? obj)
            {
                if (obj == null)
                {
                    return false;
                }

                if (obj.GetType() == typeof(ProviderTrackInfo))
                {
                    return Equals(obj as ProviderTrackInfo);
                }

                if (obj.GetType() == typeof(ProviderTrack))
                {
                    var other = obj as ProviderTrack;
                    return Id == other!.Id &&
                           Name == other.Name &&
                           AlbumName == other.AlbumName &&
                           AlbumArtistNames == other.AlbumArtistNames &&
                           ArtistNames == other.ArtistNames;
                }

                return false;
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                return HashCode.Combine(Id, Name, AlbumName, AlbumArtistNames, ArtistNames);
            }
        }
    }
}
