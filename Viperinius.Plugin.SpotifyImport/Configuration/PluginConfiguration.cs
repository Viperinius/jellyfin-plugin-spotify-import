#pragma warning disable CA1819

using System;
using System.Linq;
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
        PlaylistIds = Array.Empty<string>();
    }

    /// <summary>
    /// Gets or sets the Spotify client ID.
    /// </summary>
    public string SpotifyClientId { get; set; }

    /// <summary>
    /// Gets or sets the Spotify client secret.
    /// </summary>
    public string SpotifyClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the targeted playlist IDs.
    /// </summary>
    public string[] PlaylistIds { get; set; }

    /// <summary>
    /// Adds a new playlist ID.
    /// </summary>
    /// <param name="id">The targeted ID.</param>
    public void AddPlaylistId(string id)
    {
        if (!PlaylistIds.Contains(id))
        {
            var list = PlaylistIds.ToList();
            list.Add(id);
            PlaylistIds = list.ToArray();
        }
    }

    /// <summary>
    /// Removes a playlist ID.
    /// </summary>
    /// <param name="id">The targeted ID.</param>
    public void RemovePlaylistId(string id)
    {
        var list = PlaylistIds.ToList();
        list.Remove(id);
        PlaylistIds = list.ToArray();
    }
}
