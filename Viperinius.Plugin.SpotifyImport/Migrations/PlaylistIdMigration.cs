#pragma warning disable CA1034
#pragma warning disable CA1819

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Configuration;
using Viperinius.Plugin.SpotifyImport.Exceptions;

namespace Viperinius.Plugin.SpotifyImport.Migrations
{
    /// <summary>
    /// Reference: 2023-05-14 [Commit: 0f7374b608f0e2a6287250ffa12731020fb4e40d]
    /// Convert any existing legacy playlist IDs to full playlist configurations.
    /// </summary>
    public class PlaylistIdMigration : BaseMigration
    {
        internal PlaylistIdMigration(
            string configPath,
            IXmlSerializer serialiser,
            ILogger logger) : base(configPath, serialiser, logger)
        {
        }

        /// <inheritdoc/>
        public override Version LatestWorkingRelease { get; } = new Version(1, 1, 1, 0);

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

                if (config.PlaylistIds.Length == 0)
                {
                    return true;
                }

                var legacyPlaylistCount = config.PlaylistIds.Length;

                var playlists = Plugin.Instance!.Configuration.Playlists.ToList();
                foreach (var playlistId in config.PlaylistIds.Where(id => !playlists.Where(p => p.Id == id).Any()))
                {
                    playlists.Add(new TargetPlaylistConfiguration
                    {
                        Id = playlistId
                    });
                }

                Plugin.Instance.Configuration.Playlists = playlists.ToArray();
                Logger.LogInformation("Migrated {Count} legacy playlist configurations", legacyPlaylistCount);

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
                PlaylistIds = Array.Empty<string>();
            }

            /// <summary>
            /// Gets or sets the targeted playlist IDs.
            /// Only used for compatibility purposes for old versions.
            /// </summary>
            public string[] PlaylistIds { get; set; }
        }
    }
}
