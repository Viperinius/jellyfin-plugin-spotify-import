using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Viperinius.Plugin.SpotifyImport.Configuration;

namespace Viperinius.Plugin.SpotifyImport;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Spotify Import";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("F03D0ADB-289F-4986-BD6F-2468025249B3");

    /// <summary>
    /// Gets or sets a value indicating whether the plugin instance was fully initialised during server start.
    /// </summary>
    public bool IsInitialised { get; set; }

    /// <summary>
    /// Gets the base path for the plugin API.
    /// </summary>
    public static string PluginQualifiedName => $"{nameof(Viperinius)}.{nameof(Viperinius.Plugin)}.{nameof(SpotifyImport)}";

    /// <summary>
    /// Gets the Spotify base URL.
    /// </summary>
    public static string SpotifyBaseUrl => "https://open.spotify.com";

    /// <summary>
    /// Gets the path used for storing plugin data.
    /// </summary>
    public string PluginDataPath
    {
        get
        {
            if (!Path.Exists(DataFolderPath))
            {
                Directory.CreateDirectory(DataFolderPath);
            }

            return DataFolderPath;
        }
    }

    /// <summary>
    /// Gets the path to the db file.
    /// </summary>
    public string DbPath => Path.Combine(PluginDataPath, "plugin.db");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = "playlistconfigjs",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.playlistConfig.js", GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = "spotifyimportmap",
                DisplayName = "Spotify Import - Map",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.manualMapConfig.html", GetType().Namespace),
                EnableInMainMenu = true,
            },
            new PluginPageInfo
            {
                Name = "spotifyimportmapjs",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.manualMapConfig.js", GetType().Namespace),
            },
        };
    }

    /// <summary>
    /// Get the xml serialiser.
    /// </summary>
    /// <returns>Xml Serialiser.</returns>
    public IXmlSerializer GetInternalXmlSerializer()
    {
        return XmlSerializer;
    }

    /// <summary>
    /// Get the jellyfin app paths.
    /// </summary>
    /// <returns>Application paths.</returns>
    public IApplicationPaths GetServerApplicationPaths()
    {
        return ApplicationPaths;
    }

    /// <summary>
    /// Sets the plugin instance.
    /// </summary>
    /// <param name="instance">New Instance.</param>
    public static void SetInstance(Plugin? instance)
    {
        Instance = instance;
    }
}
