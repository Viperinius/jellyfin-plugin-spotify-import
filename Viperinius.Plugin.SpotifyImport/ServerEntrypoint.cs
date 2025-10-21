using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Migrations;

namespace Viperinius.Plugin.SpotifyImport
{
    /// <summary>
    /// Entrypoint called when starting the server.
    /// </summary>
    public class ServerEntrypoint : IHostedService
    {
        private readonly ILogger<ServerEntrypoint> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerEntrypoint"/> class.
        /// </summary>
        /// <param name="logger">Logger.</param>
        public ServerEntrypoint(ILogger<ServerEntrypoint> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (Plugin.Instance != null)
            {
                try
                {
                    if (Plugin.Instance.Configuration.EnableVerboseLogging)
                    {
                        // print this info to ease finding the config dir when setting up debug logging
                        _logger.LogInformation("Jellyfin Configuration Directory: {Path}", Plugin.Instance.GetServerApplicationPaths().ConfigurationDirectoryPath);

                        var tmpManualMapStore = new ManualMapStore();
                        _logger.LogInformation("Path to manual track map: {Path}", tmpManualMapStore.FilePath);

                        _logger.LogInformation("Checking for any needed migrations...");
                    }

                    var thisVersion = Version.Parse(typeof(Plugin).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
                    var configVersion = new Version(0, 0, 0, 0);
                    if (!string.IsNullOrWhiteSpace(Plugin.Instance.Configuration.Version))
                    {
                        configVersion = Version.Parse(Plugin.Instance.Configuration.Version);
                    }

                    if (configVersion < thisVersion)
                    {
                        Plugin.Instance.IsInitialised = RunMigrations(configVersion);

                        // update config version
                        Plugin.Instance.Configuration.Version = thisVersion.ToString();

                        Plugin.Instance.SaveConfiguration();
                    }
                    else
                    {
                        Plugin.Instance.IsInitialised = true;
                    }

                    using var db = new Utils.DbRepository(Plugin.Instance.DbPath);
                    db.InitDb();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to perform tasks on server start");
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private bool RunMigrations(Version configVersion)
        {
            var path = Plugin.Instance!.ConfigurationFilePath;
            var xmlSeraliser = Plugin.Instance.GetInternalXmlSerializer();
            var result = true;

            var migrations = new List<BaseMigration>
            {
                new UserPlaylistsMigration(path, xmlSeraliser, _logger),
                new PlaylistIdMigration(path, xmlSeraliser, _logger),
                new SpotifySecretsUrlMigration(path, xmlSeraliser, _logger),
            }.OrderBy(m => m.LatestWorkingRelease);

            foreach (var migration in migrations)
            {
                result &= migration.Execute(configVersion);
            }

            return result;
        }
    }
}
