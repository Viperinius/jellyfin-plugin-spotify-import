#pragma warning disable CA1819

using System;
using System.Linq;
using System.Xml.Serialization;
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
        PlaylistIds = Array.Empty<string>();
        Playlists = Array.Empty<TargetPlaylistConfiguration>();
        MissingTrackListsDateFormat = "yyyy-MM-dd_HH-mm";
    }

    /// <summary>
    /// Gets or sets a value indicating whether to enable verbose logging (ex: spotify requests).
    /// </summary>
    public bool EnableVerboseLogging { get; set; }

    /// <summary>
    /// Gets or sets the Spotify client ID.
    /// </summary>
    public string SpotifyClientId { get; set; }

    /// <summary>
    /// Gets or sets the targeted playlist IDs.
    /// Only used for compatibility purposes for old versions.
    /// </summary>
    public string[] PlaylistIds { get; set; }

    /// <summary>
    /// Gets or sets the targeted playlists.
    /// </summary>
    public TargetPlaylistConfiguration[] Playlists { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable the creation of files containing missing tracks on the server.
    /// </summary>
    public bool GenerateMissingTrackLists { get; set; }

    /// <summary>
    /// Gets or sets the date time format for the filenames of missing tracks.
    /// </summary>
    public string MissingTrackListsDateFormat { get; set; }

    /// <summary>
    /// Gets the list of existing files with missing tracks.
    /// </summary>
    [XmlIgnore]
    public string[] MissingTrackListPaths => MissingTrackStore.GetFileList().ToArray();

    /// <summary>
    /// Gets or sets the Spotify auth token.
    /// </summary>
    [XmlElement(IsNullable = true)]
    public SpotifyAPI.Web.PKCETokenResponse? SpotifyAuthToken { get; set; }
}
