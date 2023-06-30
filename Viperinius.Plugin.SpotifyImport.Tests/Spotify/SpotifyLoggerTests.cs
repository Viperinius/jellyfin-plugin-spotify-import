using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Spotify
{
    public class SpotifyLoggerTests
    {
        private void SetValidPluginInstance()
        {
            if (Plugin.Instance == null)
            {
                var mockAppPaths = new Mock<MediaBrowser.Common.Configuration.IApplicationPaths>();
                mockAppPaths.SetupGet(m => m.PluginsPath).Returns(() => string.Empty);
                mockAppPaths.SetupGet(m => m.PluginConfigurationsPath).Returns(() => string.Empty);
                var mockXmlSerializer = new Mock<MediaBrowser.Model.Serialization.IXmlSerializer>();
                mockXmlSerializer.Setup(m => m.DeserializeFromFile(It.IsAny<Type>(), It.IsAny<string>()))
                                 .Returns(() => new Configuration.PluginConfiguration());

                _ = new Plugin(mockAppPaths.Object, mockXmlSerializer.Object);
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
            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            _ = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);
        }

        [Fact]
        public void Log_OnRequest_InvalidRequest()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = true;

            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);

#pragma warning disable CS8625
            spotifyLogger.OnRequest(null);
#pragma warning restore CS8625

            mock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Log_OnRequest_NoPluginInstance()
        {
            SetNullPluginInstance();

            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);

            var request = new SpotifyAPI.Web.Http.Request(
                new Uri("http://example.com"),
                new Uri("/no", UriKind.Relative),
                System.Net.Http.HttpMethod.Get);

            spotifyLogger.OnRequest(request);

            mock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Log_OnRequest_DisabledLogging()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = false;

            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);

            var request = new SpotifyAPI.Web.Http.Request(
                new Uri("http://example.com"),
                new Uri("/no", UriKind.Relative),
                System.Net.Http.HttpMethod.Get);

            spotifyLogger.OnRequest(request);

            mock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Log_OnRequest()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = true;

            var expectedReqUri = "http://example.com";
            var expectedReqEndpoint = "/abc";
            var expectedMsg = $"GET {expectedReqEndpoint} [] (null)";

            var actualMsg = string.Empty;

            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            mock.Setup(m => m.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback(new InvocationAction(invocation =>
                {
                    var state = invocation.Arguments[2];
                    var exception = invocation.Arguments[3];
                    var formatter = invocation.Arguments[4];
                    var formattingMethod = formatter.GetType().GetMethod("Invoke");
                    actualMsg = (string)formattingMethod!.Invoke(formatter, new[] { state, exception })!;
                }));

            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);

            var request = new SpotifyAPI.Web.Http.Request(
                new Uri(expectedReqUri),
                new Uri(expectedReqEndpoint, UriKind.Relative),
                System.Net.Http.HttpMethod.Get);

            spotifyLogger.OnRequest(request);

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

            var actualMsg = string.Empty;

            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            mock.Setup(m => m.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback(new InvocationAction(invocation =>
                {
                    var state = invocation.Arguments[2];
                    var exception = invocation.Arguments[3];
                    var formatter = invocation.Arguments[4];
                    var formattingMethod = formatter.GetType().GetMethod("Invoke");
                    actualMsg = (string)formattingMethod!.Invoke(formatter, new[] { state, exception })!;
                }));

            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);

            var request = new SpotifyAPI.Web.Http.Request(
                new Uri(expectedReqUri),
                new Uri(expectedReqEndpoint, UriKind.Relative),
                System.Net.Http.HttpMethod.Post,
                new Dictionary<string, string>(),
                reqParams);
            request.Body = expectedReqBody;

            spotifyLogger.OnRequest(request);

            Assert.Equal(expectedMsg, actualMsg);
        }

        [Fact]
        public void Log_OnResponse_InvalidResponse()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = true;

            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);

#pragma warning disable CS8625
            spotifyLogger.OnResponse(null);
#pragma warning restore CS8625

            mock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Log_OnResponse_NoPluginInstance()
        {
            SetNullPluginInstance();

            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);

            var response = new SpotifyAPI.Web.Http.Response(new Dictionary<string, string>());

            spotifyLogger.OnResponse(response);

            mock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Log_OnResponse_DisabledLogging()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = false;

            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);

            var response = new SpotifyAPI.Web.Http.Response(new Dictionary<string, string>());

            spotifyLogger.OnResponse(response);

            mock.VerifyNoOtherCalls();
        }

        [Fact]
        public void Log_OnResponse()
        {
            SetValidPluginInstance();
            Plugin.Instance!.Configuration.EnableVerboseLogging = true;

            var expectedCode = System.Net.HttpStatusCode.UnavailableForLegalReasons;
            var expectedContentType = "application/json";
            var expectedMsg = $"--> {expectedCode} {expectedContentType} (null)";

            var actualMsg = string.Empty;

            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            mock.Setup(m => m.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback(new InvocationAction(invocation =>
                {
                    var state = invocation.Arguments[2];
                    var exception = invocation.Arguments[3];
                    var formatter = invocation.Arguments[4];
                    var formattingMethod = formatter.GetType().GetMethod("Invoke");
                    actualMsg = (string)formattingMethod!.Invoke(formatter, new[] { state, exception })!;
                }));

            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);

            var response = new SpotifyAPI.Web.Http.Response(new Dictionary<string, string>());
            response.StatusCode = expectedCode;
            response.ContentType = expectedContentType;

            spotifyLogger.OnResponse(response);

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

            var actualMsg = string.Empty;

            var mock = new Mock<ILogger<SpotifyImport.Spotify.SpotifyLogger>>();
            mock.Setup(m => m.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback(new InvocationAction(invocation =>
                {
                    var state = invocation.Arguments[2];
                    var exception = invocation.Arguments[3];
                    var formatter = invocation.Arguments[4];
                    var formattingMethod = formatter.GetType().GetMethod("Invoke");
                    actualMsg = (string)formattingMethod!.Invoke(formatter, new[] { state, exception })!;
                }));

            var spotifyLogger = new SpotifyImport.Spotify.SpotifyLogger(mock.Object);

            var response = new SpotifyAPI.Web.Http.Response(new Dictionary<string, string>());
            response.StatusCode = expectedCode;
            response.ContentType = expectedContentType;
            response.Body = rawBody;

            spotifyLogger.OnResponse(response);

            Assert.Equal(expectedMsg, actualMsg);

            rawBody += "thisisalongerbodycontentthatshouldbecutoffatsomepoint";
            expectedBody += "thisisalongerbodycontentthatshouldbecutoffatsomepoint";
            expectedBody = expectedBody[..50];
            expectedMsg = $"--> {expectedCode} {expectedContentType} {expectedBody}";

            response.Body = rawBody;

            spotifyLogger.OnResponse(response);

            Assert.Equal(expectedMsg, actualMsg);
        }
    }
}
