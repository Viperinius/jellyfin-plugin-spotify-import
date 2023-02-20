using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web.Http;

namespace Viperinius.Plugin.SpotifyImport.Spotify
{
    /// <summary>
    /// Based on the example logger of SpotifyAPI-NET:
    /// https://github.com/JohnnyCrazy/SpotifyAPI-NET/blob/master/SpotifyAPI.Web/Http/SimpleConsoleHTTPLogger.cs
    ///
    /// Uses the ILogger interface instead of Console.
    /// </summary>
    internal class SpotifyLogger : IHTTPLogger
    {
        private readonly ILogger<SpotifyLogger> _logger;

        public SpotifyLogger(ILogger<SpotifyLogger> logger)
        {
            _logger = logger;
        }

        public void OnRequest(IRequest request)
        {
            if (request == null || !(Plugin.Instance?.Configuration.EnableVerboseLogging ?? false))
            {
                return;
            }

            string? parameters = null;
            if (request.Parameters != null)
            {
                parameters = string.Join(
                    ",",
                    request.Parameters.Select(kv => kv.Key + "=" + kv.Value)?.ToArray() ?? Array.Empty<string>());
            }

            _logger.LogInformation("{Method} {Endpoint} [{Params}] {Body}", request.Method, request.Endpoint, parameters, request.Body);
        }

        public void OnResponse(IResponse response)
        {
            if (response == null || !(Plugin.Instance?.Configuration.EnableVerboseLogging ?? false))
            {
                return;
            }

            string? body = response.Body?.ToString()?.Replace("\n", string.Empty, StringComparison.InvariantCulture);
            body = body?[..Math.Min(50, body.Length)];

            _logger.LogInformation("--> {Code} {Type} {Body}", response.StatusCode, response.ContentType, body);
        }
    }
}
