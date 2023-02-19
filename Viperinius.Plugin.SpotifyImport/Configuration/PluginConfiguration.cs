using MediaBrowser.Model.Plugins;

namespace Viperinius.Plugin.SpotifyImport.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        SpotifyClientId = string.Empty;
        SpotifyClientSecret = string.Empty;
    }

    /// <summary>
    /// Gets or sets the Spotify client ID.
    /// </summary>
    public string SpotifyClientId { get; set; }

    /// <summary>
    /// Gets or sets the Spotify client secret.
    /// </summary>
    public string SpotifyClientSecret { get; set; }
}
