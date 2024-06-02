#pragma warning disable CA1034
#pragma warning disable CA1819

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Exceptions;

namespace Viperinius.Plugin.SpotifyImport.Migrations
{
    /// <summary>
    /// Reference: 2023-08-30 [Commit: 0018bbc8ba0a463e56100e3236ee94e8147be5f2]
    /// Convert any existing target playlist configurations containing a user ID to target user configurations.
    /// </summary>
    public class UserPlaylistsMigration : BaseMigration
    {
        internal UserPlaylistsMigration(
            string configPath,
            IXmlSerializer serialiser,
            ILogger logger) : base(configPath, serialiser, logger)
        {
        }

        /// <summary>
        /// Type the configuration describes.
        /// </summary>
        public enum TargetConfigurationType
        {
            /// <summary>
            /// Configuration contains a playlist id.
            /// </summary>
            Playlist,

            /// <summary>
            /// Configuration contains a user id.
            /// </summary>
            User,
        }

        /// <inheritdoc/>
        public override Version LatestWorkingRelease { get; } = new Version(1, 3, 0, 0);

        /// <inheritdoc/>
        public override bool Execute(Version currentConfigVersion)
        {
            if (currentConfigVersion > LatestWorkingRelease)
            {
                return true;
            }

            try
            {
                var config = ParseConfigFile<PluginConfiguration>();

                if (config.Playlists.Length == 0)
                {
                    return true;
                }

                var users = Plugin.Instance!.Configuration.Users.ToList();
                var playlists = Plugin.Instance.Configuration.Playlists.ToList();
                var migratedCount = 0;
                foreach (var playlist in config.Playlists.Where(p => p.Type == TargetConfigurationType.User))
                {
                    var user = new Configuration.TargetUserConfiguration
                    {
                        Id = playlist.Id,
                        UserName = playlist.UserName,
                        OnlyOwnPlaylists = true // this was always done in this version, so keep the behaviour
                    };

                    if (!users.Any(u => u.Id == user.Id))
                    {
                        users.Add(user);
                    }

                    playlists.RemoveAll(p => p.Id == playlist.Id);
                    migratedCount++;
                }

                Plugin.Instance.Configuration.Users = users.ToArray();
                Plugin.Instance.Configuration.Playlists = playlists.ToArray();
                Logger.LogInformation("Migrated {Count} target playlist configurations with user IDs", migratedCount);

                return true;
            }
            catch (MigrationException e)
            {
                Logger.LogError(e, "Failed");
                return false;
            }
        }

        /// <summary>
        /// Parts of the configuration needed to migrate.
        /// </summary>
        public class PluginConfiguration : BasePluginConfiguration
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
            /// </summary>
            public PluginConfiguration()
            {
                Playlists = Array.Empty<TargetPlaylistConfiguration>();
            }

            /// <summary>
            /// Gets or sets the targeted playlists.
            /// </summary>
            public TargetPlaylistConfiguration[] Playlists { get; set; }
        }

        /// <summary>
        /// Holds the information about a configured playlist.
        /// </summary>
        public class TargetPlaylistConfiguration
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TargetPlaylistConfiguration"/> class.
            /// </summary>
            public TargetPlaylistConfiguration()
            {
                Id = string.Empty;
                Name = string.Empty;
                UserName = string.Empty;
            }

            /// <summary>
            /// Gets or sets the playlist ID.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// Gets or sets the targeted Jellyfin playlist name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the targeted Jellyfin user name.
            /// </summary>
            public string UserName { get; set; }

            /// <summary>
            /// Gets or sets the type of the configured id.
            /// </summary>
            public TargetConfigurationType Type { get; set; }
        }
    }
}
