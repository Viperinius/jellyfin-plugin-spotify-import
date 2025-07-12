#pragma warning disable CA1819

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;
using Viperinius.Plugin.SpotifyImport.Matchers;

namespace Viperinius.Plugin.SpotifyImport.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string _spotifyOAuthTokenJson;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Version = string.Empty;
        SpotifyClientId = string.Empty;
        SpotifyCookie = string.Empty;
        _spotifyOAuthTokenJson = string.Empty;
        Playlists = Array.Empty<TargetPlaylistConfiguration>();
        Users = Array.Empty<TargetUserConfiguration>();
        ItemMatchCriteriaRaw = (int)(ItemMatchCriteria.TrackName | ItemMatchCriteria.AlbumName | ItemMatchCriteria.AlbumArtists | ItemMatchCriteria.Artists);
        ItemMatchLevel = ItemMatchLevel.Default;
        MaxFuzzyCharDifference = 2;
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
    /// Gets or sets a value indicating whether to show completeness information.
    /// </summary>
    public bool ShowCompletenessInformation { get; set; }

    /// <summary>
    /// Gets or sets the Spotify client ID.
    /// </summary>
    public string SpotifyClientId { get; set; }

    /// <summary>
    /// Gets or sets the Spotify session cookies.
    /// </summary>
    public string SpotifyCookie { get; set; }

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
    /// Gets or sets the maximum amount of character differences to be acceptable as fuzzy match.
    /// </summary>
    public int MaxFuzzyCharDifference { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable the legacy way of comparing tracks.
    /// </summary>
    public bool UseLegacyMatching { get; set; }

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

    /// <summary>
    /// Gets or sets the Spotify OAuth token in JSON format.
    /// </summary>
    public string SpotifyOAuthTokenJson
    {
        get => _spotifyOAuthTokenJson;
        set
        {
            _spotifyOAuthTokenJson = value;

            // If JSON is provided, try to parse it and update SpotifyAuthToken
            if (!string.IsNullOrWhiteSpace(value))
            {
                try
                {
                    var parsedToken = JsonSerializer.Deserialize<SpotifyAPI.Web.PKCETokenResponse>(value, _jsonSerializerOptions);
                    if (parsedToken != null &&
                        !string.IsNullOrEmpty(parsedToken.AccessToken) &&
                        !string.IsNullOrEmpty(parsedToken.TokenType) &&
                        parsedToken.ExpiresIn > 0 &&
                        !string.IsNullOrEmpty(parsedToken.RefreshToken) &&
                        !string.IsNullOrEmpty(parsedToken.Scope))
                    {
                        // If CreatedAt was not provided or is default, set it to a date in the past
                        // to ensure the token doesn't appear "fresh" when it might be old
                        if (parsedToken.CreatedAt == default || parsedToken.CreatedAt == DateTime.MinValue)
                        {
                            // Set to 1 hour ago to account for typical token expiration times
                            parsedToken.CreatedAt = DateTime.UtcNow.AddHours(-1);
                        }

                        SpotifyAuthToken = parsedToken;
                    }
                }
                catch (JsonException)
                {
                    // If parsing fails, keep the JSON but don't update SpotifyAuthToken
                    // The validation will be handled in the frontend
                }
            }
        }
    }
}
