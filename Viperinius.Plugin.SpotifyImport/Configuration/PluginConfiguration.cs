#pragma warning disable CA1819

using System;
using System.Linq;
using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;
using Viperinius.Plugin.SpotifyImport.Matchers;

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
        Version = string.Empty;
        SpotifyClientId = string.Empty;
        Playlists = Array.Empty<TargetPlaylistConfiguration>();
        Users = Array.Empty<TargetUserConfiguration>();
        ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
        ItemMatchLevel = ItemMatchLevel.Default;
        MissingTrackListsDateFormat = "yyyy-MM-dd_HH-mm";
    }

    /// <summary>
    /// Gets or sets the config version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable verbose logging (ex: spotify requests).
    /// </summary>
    public bool EnableVerboseLogging { get; set; }

    /// <summary>
    /// Gets or sets the Spotify client ID.
    /// </summary>
    public string SpotifyClientId { get; set; }

    /// <summary>
    /// Gets or sets the targeted playlists.
    /// </summary>
    public TargetPlaylistConfiguration[] Playlists { get; set; }

    /// <summary>
    /// Gets or sets the target users.
    /// </summary>
    public TargetUserConfiguration[] Users { get; set; }

    /// <summary>
    /// Gets the track comparison criteria.
    /// </summary>
    internal ItemMatchCriteria ItemMatchCriteria => (ItemMatchCriteria)ItemMatchCriteriaRaw;

    /// <summary>
    /// Gets or sets the track comparison criteria.
    /// </summary>
    public int ItemMatchCriteriaRaw { get; set; }

    /// <summary>
    /// Gets or sets the track comparison level.
    /// </summary>
    public ItemMatchLevel ItemMatchLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable the creation of files containing missing tracks on the server.
    /// </summary>
    public bool GenerateMissingTrackLists { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to keep created missing track files (instead of storing in tmp dir).
    /// </summary>
    public bool KeepMissingTrackLists { get; set; }

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
