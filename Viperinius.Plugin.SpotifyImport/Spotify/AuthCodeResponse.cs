using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Spotify
{
    /// <summary>
    /// The response containing the Spotify auth code.
    /// </summary>
    public class AuthCodeResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthCodeResponse"/> class.
        /// </summary>
        /// <param name="code">The auth code.</param>
        /// <param name="state">The auth state.</param>
        /// <param name="error">An auth error.</param>
        public AuthCodeResponse(string code, string state, string? error = null)
        {
            Code = code;
            State = state;
            Error = error;
        }

        /// <summary>
        /// Gets or sets the auth code.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets the auth state.
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// Gets or sets the auth error.
        /// </summary>
        public string? Error { get; set; }
    }
}
