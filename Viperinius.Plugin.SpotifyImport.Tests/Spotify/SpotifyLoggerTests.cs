using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Spotify
{
    public class SpotifyLoggerTests
    {
        private void SetValidPluginInstance()
        {
            if (Plugin.Instance == null)
            {
                var mockAppPaths = Substitute.For<MediaBrowser.Common.Configuration.IApplicationPaths>();
                mockAppPaths.PluginsPath.Returns(string.Empty);
                mockAppPaths.PluginConfigurationsPath.Returns(string.Empty);
                var mockXmlSerializer = Substitute.For<MediaBrowser.Model.Serialization.IXmlSerializer>();
                mockXmlSerializer.DeserializeFromFile(Arg.Any<Type>(), Arg.Any<string>())
                                 .Returns(_ => new Configuration.PluginConfiguration());

                _ = new Plugin(mockAppPaths, mockXmlSerializer);
            }
            System.Threading.Thread.Sleep(100);
        }

        private void SetNullPluginInstance()
        {
            if (Plugin.Instance != null)
            {
                Plugin.SetInstance(null);
            }
            System.Threading.Thread.Sleep(100);
        }

        [Fact]
        public void CreateInstance()
        {
            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            _ = new SpotifyImport.Spotify.SpotifyLogger(mock);
        }

        [Fact]
        public void Log_OnRequest_InvalidRequest()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = true;

            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock);

#pragma warning disable CS8625
            spotifyLogger.OnRequest(null);
#pragma warning restore CS8625

            Assert.Empty(mock.ReceivedCalls());
        }

        [Fact]
        public void Log_OnRequest_NoPluginInstance()
        {
            SetNullPluginInstance();

            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock);

            var request = new SpotifyAPI.Web.Http.Request(
                new Uri("http://example.com"),
                new Uri("/no", UriKind.Relative),
                System.Net.Http.HttpMethod.Get);

            spotifyLogger.OnRequest(request);

            Assert.Empty(mock.ReceivedCalls());
        }

        [Fact]
        public void Log_OnRequest_DisabledLogging()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = false;

            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock);

            var request = new SpotifyAPI.Web.Http.Request(
                new Uri("http://example.com"),
                new Uri("/no", UriKind.Relative),
                System.Net.Http.HttpMethod.Get);

            spotifyLogger.OnRequest(request);

            Assert.Empty(mock.ReceivedCalls());
        }

        [Fact]
        public void Log_OnRequest()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = true;

            var expectedReqUri = "http://example.com";
            var expectedReqEndpoint = "/abc";
            var expectedMsg = $"GET {expectedReqEndpoint} [] (null)";

            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock);

            var request = new SpotifyAPI.Web.Http.Request(
                new Uri(expectedReqUri),
                new Uri(expectedReqEndpoint, UriKind.Relative),
                System.Net.Http.HttpMethod.Get);

            spotifyLogger.OnRequest(request);

            var call = mock.ReceivedCalls().First();
            var callArgs = call.GetArguments();

            Assert.Equal(callArgs[0], LogLevel.Information);

            var state = callArgs[2];
            var exception = callArgs[3];
            var formatter = callArgs[4];
            var formattingMethod = formatter!.GetType().GetMethod("Invoke");
            var actualMsg = (string)formattingMethod!.Invoke(formatter, new[] { state, exception })!;

            Assert.Equal(expectedMsg, actualMsg);
        }
        
        [Fact]
        public void Log_OnRequest_WithParamsAndBody()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = true;

            var expectedReqUri = "http://example.com";
            var expectedReqEndpoint = "/abc";
            var reqParams = new Dictionary<string, string>
            {
                { "1", "a" },
                { "2", "b" },
                { "345", "XYZ" }
            };
            var expectedReqParams = "1=a,2=b,345=XYZ";
            var expectedReqBody = "hello";
            var expectedMsg = $"POST {expectedReqEndpoint} [{expectedReqParams}] {expectedReqBody}";

            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock);

            var request = new SpotifyAPI.Web.Http.Request(
                new Uri(expectedReqUri),
                new Uri(expectedReqEndpoint, UriKind.Relative),
                System.Net.Http.HttpMethod.Post,
                new Dictionary<string, string>(),
                reqParams);
            request.Body = expectedReqBody;

            spotifyLogger.OnRequest(request);

            var call = mock.ReceivedCalls().First();
            var callArgs = call.GetArguments();

            Assert.Equal(callArgs[0], LogLevel.Information);

            var state = callArgs[2];
            var exception = callArgs[3];
            var formatter = callArgs[4];
            var formattingMethod = formatter!.GetType().GetMethod("Invoke");
            var actualMsg = (string)formattingMethod!.Invoke(formatter, new[] { state, exception })!;

            Assert.Equal(expectedMsg, actualMsg);
        }

        [Fact]
        public void Log_OnResponse_InvalidResponse()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = true;

            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock);

#pragma warning disable CS8625
            spotifyLogger.OnResponse(null);
#pragma warning restore CS8625

            Assert.Empty(mock.ReceivedCalls());
        }

        [Fact]
        public void Log_OnResponse_NoPluginInstance()
        {
            SetNullPluginInstance();

            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock);

            var response = new SpotifyAPI.Web.Http.Response(new Dictionary<string, string>());

            spotifyLogger.OnResponse(response);

            Assert.Empty(mock.ReceivedCalls());
        }

        [Fact]
        public void Log_OnResponse_DisabledLogging()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = false;

            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock);

            var response = new SpotifyAPI.Web.Http.Response(new Dictionary<string, string>());

            spotifyLogger.OnResponse(response);

            Assert.Empty(mock.ReceivedCalls());
        }

        [Fact]
        public void Log_OnResponse()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = true;

            var expectedCode = System.Net.HttpStatusCode.UnavailableForLegalReasons;
            var expectedContentType = "application/json";
            var expectedMsg = $"--> {expectedCode} {expectedContentType} (null)";

            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock);

            var response = new SpotifyAPI.Web.Http.Response(new Dictionary<string, string>());
            response.StatusCode = expectedCode;
            response.ContentType = expectedContentType;

            spotifyLogger.OnResponse(response);

            var call = mock.ReceivedCalls().First();
            var callArgs = call.GetArguments();

            Assert.Equal(callArgs[0], LogLevel.Information);

            var state = callArgs[2];
            var exception = callArgs[3];
            var formatter = callArgs[4];
            var formattingMethod = formatter!.GetType().GetMethod("Invoke");
            var actualMsg = (string)formattingMethod!.Invoke(formatter, new[] { state, exception })!;

            Assert.Equal(expectedMsg, actualMsg);
        }

        [Fact]
        public void Log_OnResponse_WithBody()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = true;

            var expectedCode = System.Net.HttpStatusCode.UnavailableForLegalReasons;
            var expectedContentType = "application/json";
            var rawBody = "oh hi, have some \n\n\n\t.";
            var expectedBody = "oh hi, have some \t.";
            var expectedMsg = $"--> {expectedCode} {expectedContentType} {expectedBody}";

            var mock = Substitute.For<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock);

            var response = new SpotifyAPI.Web.Http.Response(new Dictionary<string, string>());
            response.StatusCode = expectedCode;
            response.ContentType = expectedContentType;
            response.Body = rawBody;

            spotifyLogger.OnResponse(response);

            var call = mock.ReceivedCalls().First();
            var callArgs = call.GetArguments();

            Assert.Equal(callArgs[0], LogLevel.Information);

            var state = callArgs[2];
            var exception = callArgs[3];
            var formatter = callArgs[4];
            var formattingMethod = formatter!.GetType().GetMethod("Invoke");
            var actualMsg = (string)formattingMethod!.Invoke(formatter, new[] { state, exception })!;

            Assert.Equal(expectedMsg, actualMsg);

            rawBody += "thisisalongerbodycontentthatshouldbecutoffatsomepoint";
            expectedBody += "thisisalongerbodycontentthatshouldbecutoffatsomepoint";
            expectedBody = expectedBody[..50];
            expectedMsg = $"--> {expectedCode} {expectedContentType} {expectedBody}";

            response.Body = rawBody;
            mock.ClearReceivedCalls();

            spotifyLogger.OnResponse(response);

            call = mock.ReceivedCalls().First();
            callArgs = call.GetArguments();

            Assert.Equal(callArgs[0], LogLevel.Information);

            state = callArgs[2];
            exception = callArgs[3];
            formatter = callArgs[4];
            formattingMethod = formatter!.GetType().GetMethod("Invoke");
            actualMsg = (string)formattingMethod!.Invoke(formatter, new[] { state, exception })!;

            Assert.Equal(expectedMsg, actualMsg);
        }
    }
}
