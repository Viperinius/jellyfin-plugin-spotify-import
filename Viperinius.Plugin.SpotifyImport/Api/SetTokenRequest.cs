namespace Viperinius.Plugin.SpotifyImport.Api
{
    /// <summary>
    /// Request model for setting auth token from JSON.
    /// </summary>
    public class SetTokenRequest
    {
        /// <summary>
        /// Gets or sets the token JSON.
        /// </summary>
        public string TokenJson { get; set; } = string.Empty;
    }
}
