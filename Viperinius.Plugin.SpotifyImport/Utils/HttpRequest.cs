using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Viperinius.Plugin.SpotifyImport.Utils
{
    internal class HttpRequest
    {
        private const int MaxRetries = 3;
        private readonly string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0"; // use a hardcoded UA for now
        private readonly ILogger<HttpRequest> _logger;

        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            UseCookies = false,
            CheckCertificateRevocationList = true,
        });

        public HttpRequest(ILogger<HttpRequest> logger)
        {
            _logger = logger;
        }

        public static HttpRequestHeaders CreateHeaders()
        {
            using var message = new HttpRequestMessage();
            return message.Headers;
        }

        public static string BuildUrlQuery(Dictionary<string, string> args)
        {
            using var content = new FormUrlEncodedContent(args);
            return content.ReadAsStringAsync().Result;
        }

        public static string GetResponseContentString(HttpResponseMessage response)
        {
            return response.Content.ReadAsStringAsync().Result;
        }

        public async Task<HttpResponseMessage?> Get(Uri url, HttpRequestHeaders? headers = null, string? cookies = null)
        {
            return await Request(url, HttpMethod.Get, headers: headers, cookies: cookies).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage?> Head(Uri url, HttpRequestHeaders? headers = null, string? cookies = null)
        {
            return await Request(url, HttpMethod.Head, headers: headers, cookies: cookies).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage?> Request(Uri url, HttpMethod method, HttpContent? content = null, HttpRequestHeaders? headers = null, string? cookies = null)
        {
            for (int ii = 0; ii < MaxRetries; ii++)
            {
                try
                {
                    using var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    using var req = new HttpRequestMessage(method, url);
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            req.Headers.Add(header.Key, header.Value);
                        }
                    }

                    if (req.Headers.UserAgent.Count == 0)
                    {
                        req.Headers.UserAgent.ParseAdd(_userAgent);
                    }

                    if (!string.IsNullOrEmpty(cookies))
                    {
                        req.Headers.Add("Cookie", cookies);
                    }

                    if (content != null)
                    {
                        req.Content = content;
                    }

                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation("HTTP {Method} for {Url}", method, url);
                    }

                    var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead, cancellationToken.Token).ConfigureAwait(false);
                    res.EnsureSuccessStatusCode();
                    return res;
                }
                catch (HttpRequestException e)
                {
                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation(e, "Request failed (try {Index}/{Max})", ii + 1, MaxRetries);
                    }
                }
                catch (TaskCanceledException)
                {
                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation("Request timeout (try {Index}/{Max})", ii + 1, MaxRetries);
                    }
                }
            }

            return null;
        }
    }
}
