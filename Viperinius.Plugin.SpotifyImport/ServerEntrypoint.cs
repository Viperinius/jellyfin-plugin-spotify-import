using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.Logging;

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
                        RunMigrations(configVersion);

                        // update config version
                        Plugin.Instance.Configuration.Version = thisVersion.ToString();

                        Plugin.Instance.SaveConfiguration();
                    }

                    Plugin.Instance.IsInitialised = true;
                }
                catch (Exception)
                {
                    throw;
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

        private void RunMigrations(Version configVersion)
        {
            MigratePlaylistIds(configVersion);
        }

        /// <summary>
        /// Reference: 2023-05-14 [Commit: 0f7374b608f0e2a6287250ffa12731020fb4e40d]
        /// Convert any existing legacy playlist IDs to full playlist configurations.
        /// </summary>
        private void MigratePlaylistIds(Version configVersion)
        {
            if (configVersion >= new Version(1, 1, 1, 0))
            {
                return;
            }

            if (!(Plugin.Instance?.Configuration.PlaylistIds.Any() ?? false))
            {
                return;
            }

            var legacyPlaylistCount = Plugin.Instance.Configuration.PlaylistIds.Length;

            var playlists = Plugin.Instance.Configuration.Playlists.ToList();
            foreach (var playlistId in Plugin.Instance.Configuration.PlaylistIds)
            {
                if (!playlists.Where(p => p.Id == playlistId).Any())
                {
                    playlists.Add(new Configuration.TargetPlaylistConfiguration()
                    {
                        Id = playlistId
                    });
                }
            }

            Plugin.Instance.Configuration.Playlists = playlists.ToArray();
            Plugin.Instance.Configuration.PlaylistIds = Array.Empty<string>();

            _logger.LogInformation("Migrated {Count} legacy playlist configurations", legacyPlaylistCount);
        }
    }
}
