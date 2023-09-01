using System;
using System.Collections.Generic;
using System.Linq;
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
            _logger.LogInformation("Entrypoint");

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
    }
}
