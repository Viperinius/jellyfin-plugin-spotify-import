using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Migrations;

namespace Viperinius.Plugin.SpotifyImport
{
    /// <summary>
    /// Entrypoint called when starting the server.
    /// </summary>
    public class ServerEntrypoint : IServerEntryPoint
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
        public Task RunAsync()
        {
            if (Plugin.Instance != null)
            {
                try
                {
                    if (Plugin.Instance.Configuration.EnableVerboseLogging)
                    {
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
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to perform tasks on server start");
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Internal Dispose.
        /// </summary>
        /// <param name="dispose">Should dispose.</param>
        protected virtual void Dispose(bool dispose)
        {
        }

        private bool RunMigrations(Version configVersion)
        {
            var path = Plugin.Instance!.ConfigurationFilePath;
            var xmlSeraliser = Plugin.Instance.GetInternalXmlSerializer();
            var result = true;

            var migrations = new List<BaseMigration>
            {
                new UserPlaylistsMigration(path, xmlSeraliser, _logger),
                new PlaylistIdMigration(path, xmlSeraliser, _logger)
            }.OrderBy(m => m.LatestWorkingRelease);

            foreach (var migration in migrations)
            {
                result &= migration.Execute(configVersion);
            }

            return result;
        }
    }
}
