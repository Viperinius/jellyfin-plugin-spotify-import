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
    /// Migration base class.
    /// </summary>
    public abstract class BaseMigration
    {
        private readonly string _configPath;
        private readonly IXmlSerializer _xmlSerialiser;
        private readonly ILogger _logger;

        internal BaseMigration(string configPath, IXmlSerializer serialiser, ILogger logger)
        {
            _configPath = configPath;
            _xmlSerialiser = serialiser;
            _logger = logger;
        }

        /// <summary>
        /// Gets the last release version that supports the config to be migrated.
        /// </summary>
        public abstract Version LatestWorkingRelease { get; }

        /// <summary>
        /// Gets the path to the config file.
        /// </summary>
        protected string ConfigPath => _configPath;

        /// <summary>
        /// Gets the xml serialiser.
        /// </summary>
        protected IXmlSerializer XmlSerialiser => _xmlSerialiser;

        /// <summary>
        /// Gets the logger.
        /// </summary>
        protected ILogger Logger => _logger;

        /// <summary>
        /// Run the migration.
        /// </summary>
        /// <param name="currentConfigVersion">The version of the config file.</param>
        /// <returns>True if successful / nothing to be done, false otherwise.</returns>
        public abstract bool Execute(Version currentConfigVersion);

        /// <summary>
        /// Parse the config file into a configuration fit for <see cref="LatestWorkingRelease"/>.
        /// </summary>
        /// <typeparam name="TConfiguration">The old configuration representation.</typeparam>
        /// <returns>The parsed configuration.</returns>
        protected TConfiguration ParseConfigFile<TConfiguration>()
            where TConfiguration : BasePluginConfiguration
        {
            try
            {
                return (TConfiguration)XmlSerialiser.DeserializeFromFile(typeof(TConfiguration), ConfigPath);
            }
            catch (Exception e)
            {
                throw new MigrationException("Failed to parse config file", e);
            }
        }
    }
}
