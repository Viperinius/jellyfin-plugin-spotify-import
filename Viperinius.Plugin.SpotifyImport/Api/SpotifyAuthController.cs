#pragma warning disable CA1003

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using Viperinius.Plugin.SpotifyImport.Spotify;

namespace Viperinius.Plugin.SpotifyImport.Api
{
    /// <summary>
    /// The API controller for authenticating with Spotify.
    /// </summary>
    [ApiController]
    public class SpotifyAuthController : ControllerBase
    {
        private readonly ILogger<SpotifyAuthController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpotifyAuthController"/> class.
        /// </summary>
        /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
        public SpotifyAuthController(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SpotifyAuthController>();
        }

        /// <summary>
        /// Event that is called when the auth callback has been received.
        /// </summary>
        public static event Func<AuthCodeResponse, Task>? CallbackReceived;

        /// <summary>
        /// Creates an auth request URL and prepares the callback event.
        /// </summary>
        /// <param name="baseUrl">The base url to use in the redirect uri.</param>
        /// <returns>The login request uri.</returns>
        [HttpPost($"{nameof(Viperinius)}.{nameof(Viperinius.Plugin)}.{nameof(SpotifyImport)}/SpotifyAuth")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Produces(MediaTypeNames.Application.Json)]
        public ActionResult StartAuth([FromQuery, Required] string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                return BadRequest();
            }

            var (verifier, challenge) = PKCEUtil.GenerateCodes();
            var (state, _) = PKCEUtil.GenerateCodes(64);

            Uri? callbackEndpoint = null;
            try
            {
                callbackEndpoint = new Uri($"{baseUrl}/{Plugin.PluginQualifiedName}/SpotifyAuthCallback");
            }
            catch (Exception)
            {
                return BadRequest("Malformed Base URL");
            }

            if (string.IsNullOrEmpty(Plugin.Instance?.Configuration.SpotifyClientId))
            {
                return BadRequest("Missing Spotify Client ID");
            }

            async Task AuthCallback(AuthCodeResponse response)
            {
                CallbackReceived -= AuthCallback;

                if (!string.IsNullOrEmpty(response.Error))
                {
                    _logger.LogError("Failed to authenticate with Spotify, received error '{Error}'!", response.Error.Replace(Environment.NewLine, " ", StringComparison.InvariantCulture));
                    return;
                }

                if (response.State != state)
                {
                    _logger.LogError("Failed to authenticate with Spotify, state mismatch!");
                    return;
                }

                if (string.IsNullOrWhiteSpace(response.Code))
                {
                    _logger.LogError("Failed to authenticate with Spotify, empty code!");
                    return;
                }

                var token = await new OAuthClient().RequestToken(new PKCETokenRequest(
                    Plugin.Instance!.Configuration.SpotifyClientId,
                    response.Code,
                    callbackEndpoint,
                    verifier)).ConfigureAwait(false);

                Plugin.Instance!.Configuration.SpotifyAuthToken = token;
                Plugin.Instance!.SaveConfiguration();
            }

            CallbackReceived += AuthCallback;
            var request = new LoginRequest(callbackEndpoint, Plugin.Instance!.Configuration.SpotifyClientId, LoginRequest.ResponseType.Code)
            {
                CodeChallenge = challenge,
                CodeChallengeMethod = "S256",
                Scope = new List<string> { Scopes.PlaylistReadCollaborative, Scopes.PlaylistReadPrivate },
                State = state
            };

            var json = JsonDocument.Parse($@"{{
                ""login_req_uri"": ""{request.ToUri()}""
            }}");

            return Ok(json);
        }

        /// <summary>
        /// Handles the redirect from Spotify after requesting the auth code.
        /// </summary>
        /// <param name="state">The auth state.</param>
        /// <param name="code">The auth code, on success.</param>
        /// <param name="error">Any error if applicable.</param>
        /// <returns>Empty.</returns>
        [HttpGet($"{nameof(Viperinius)}.{nameof(Viperinius.Plugin)}.{nameof(SpotifyImport)}/SpotifyAuthCallback")]
        // [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult AuthResponseCallback(
            [FromQuery] string state,
            [FromQuery] string? code = null,
            [FromQuery] string? error = null)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return BadRequest();
            }

            CallbackReceived?.Invoke(new AuthCodeResponse(code ?? string.Empty, state, error));

            // return Ok("Received Spotify Auth. You may close this tab now.");
            return Redirect("/");
        }
    }
}
