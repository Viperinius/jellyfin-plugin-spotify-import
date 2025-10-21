#pragma warning disable CA1034

using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Exceptions;

namespace Viperinius.Plugin.SpotifyImport.Migrations
{
    /// <summary>
    /// Reference: 2025-10-20 [Commit: 091335991a239d208945d36000a06ea5da69091a]
    /// Move any previously configured default secrets url to the new one.
    /// </summary>
    public class SpotifySecretsUrlMigration : BaseMigration
    {
        private readonly string _currentUrl = "https://raw.githubusercontent.com/xyloflake/spot-secrets-go/refs/heads/main/secrets/secretBytes.json";
        internal static readonly List<string> OldUrls =
        [
            "https://raw.githubusercontent.com/Thereallo1026/spotify-secrets/refs/heads/main/secrets/secretBytes.json"
        ];

        internal SpotifySecretsUrlMigration(
            string configPath,
            IXmlSerializer serialiser,
            ILogger logger) : base(configPath, serialiser, logger)
        {
        }

        /// <inheritdoc/>
        public override Version LatestWorkingRelease { get; } = new Version(1, 15, 0, 0);

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

                if (!OldUrls.Contains(config.SpotifyTotpSecretsUrl.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    // seems to be a manually set url or already the new one, leave it
                    return true;
                }

                Plugin.Instance!.Configuration.SpotifyTotpSecretsUrl = _currentUrl;
                Logger.LogInformation("Migrated Spotify TOTP secrets URL");

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
                SpotifyTotpSecretsUrl = OldUrls[0];
            }

            /// <summary>
            /// Gets or sets the URL to retrieve Spotify TOTP secrets from.
            /// </summary>
            public string SpotifyTotpSecretsUrl { get; set; }
        }
    }
}
