using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Viperinius.Plugin.SpotifyImport.Sync;
using Viperinius.Plugin.SpotifyImport.Tests.TestHelpers;
using Xunit;

namespace Viperinius.Plugin.SpotifyImport.Tests.Sync
{
    internal class StringMatchFinderWrapper : StringMatchFinder
    {
        public StringMatchFinderWrapper(ILogger logger, MediaBrowser.Controller.Library.ILibraryManager libraryManager) : base(logger, libraryManager)
        {
        }

        public Dictionary<string, IReadOnlyCollection<BaseItem>> GetJfArtistQueryCache() => JfArtistQueryCache;
    }

    public class StringMatchFinderTests
    {
        [Fact]
        public void IsEnabledByDefault()
        {
            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            var loggerMock = Substitute.For<ILogger<ManualMapStore>>();

            var finder = new StringMatchFinder(loggerMock,libManagerMock);
            Assert.True(finder.IsEnabled);
        }

        [Fact]
        public void ArtistCacheIsUsed()
        {
            var dummyJfArtist = ArtistHelper.CreateJfItem("abc", [AlbumHelper.CreateJfItem("album", null, null)]);

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetArtists(Arg.Any<InternalItemsQuery>())
                .Returns(_ => new MediaBrowser.Model.Querying.QueryResult<(BaseItem, ItemCounts)>([(dummyJfArtist, new ItemCounts())]));
            var loggerMock = Substitute.For<ILogger<ManualMapStore>>();

            var finder = new StringMatchFinderWrapper(loggerMock, libManagerMock);
            var cache = finder.GetJfArtistQueryCache();
            Assert.Empty(cache);

            Assert.Null(finder.FindTrack(string.Empty, new ProviderTrackInfo { ArtistNames = ["one"] }));
            libManagerMock.Received(1).GetArtists(Arg.Any<InternalItemsQuery>());
            libManagerMock.Received(1).GetArtists(Arg.Is<InternalItemsQuery>(q => q.SearchTerm == "one"));

            cache = finder.GetJfArtistQueryCache();
            Assert.Single(cache);
            Assert.True(cache.ContainsKey("one"));
            Assert.Equal(dummyJfArtist, cache["one"].First());

            libManagerMock.ClearReceivedCalls();
            Assert.Null(finder.FindTrack(string.Empty, new ProviderTrackInfo { ArtistNames = ["one", "two"] }));
            libManagerMock.Received(1).GetArtists(Arg.Any<InternalItemsQuery>());
            libManagerMock.Received(1).GetArtists(Arg.Is<InternalItemsQuery>(q => q.SearchTerm == "two"));

            cache = finder.GetJfArtistQueryCache();
            Assert.Equal(2, cache.Count);
            Assert.True(cache.ContainsKey("one"));
            Assert.Equal(dummyJfArtist, cache["one"].First());
            Assert.True(cache.ContainsKey("two"));
            Assert.Equal(dummyJfArtist, cache["two"].First());
        }

        [Theory]
        [InlineData("Daryl Hall & John Oates", "Daryl Hall & John Oates", "daryl")]
        [InlineData("✝✝✝ (Crosses)", "Crosses", "cross")]
        [InlineData("$uicideboy$", "$uicideboy$", "uicid")]
        [InlineData("AC/DC", "AC/DC", "ac dc")]
        [InlineData("Mötley Crüe", "Mötley Crüe", "motle")]
        [InlineData("Muse", "Muse", "muse")]
        [InlineData("!!!", "!!!", "!!!")]
        [InlineData("Def Leppard", "Def Leppard", "def l")]
        public void FindTrackCheckGetArtist(string providerArtistName, string jfArtistName, string expectedSearchTerm)
        {
            var dummyJfArtist = ArtistHelper.CreateJfItem(jfArtistName, [AlbumHelper.CreateJfItem("album", null, null)]);

            var libManagerMock = Substitute.For<MediaBrowser.Controller.Library.ILibraryManager>();
            libManagerMock
                .GetArtists(Arg.Any<InternalItemsQuery>())
                .Returns(_ => new MediaBrowser.Model.Querying.QueryResult<(BaseItem, ItemCounts)>([(dummyJfArtist, new ItemCounts())]));
            var loggerMock = Substitute.For<ILogger<ManualMapStore>>();

            var finder = new StringMatchFinderWrapper(loggerMock, libManagerMock);
            Assert.Null(finder.FindTrack(string.Empty, new ProviderTrackInfo { ArtistNames = [providerArtistName] }));
            Assert.True(finder.LastFailedCriteria.HasFlag(SpotifyImport.Matchers.ItemMatchCriteria.AlbumName));
            Assert.True(finder.GetJfArtistQueryCache().ContainsKey(expectedSearchTerm));
        }
    }
}
