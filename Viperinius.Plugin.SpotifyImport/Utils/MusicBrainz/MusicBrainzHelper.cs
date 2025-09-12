using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Library;
using MetaBrainz.MusicBrainz;
using MetaBrainz.MusicBrainz.Interfaces.Entities;
using MetaBrainz.MusicBrainz.Interfaces.Searches;

namespace Viperinius.Plugin.SpotifyImport.Utils.MusicBrainz
{
    internal class MusicBrainzHelper : IMusicBrainzHelper, IDisposable
    {
        private readonly int _mbQueryResultLimit = 100;

        private readonly Query _mbQuery;

        public MusicBrainzHelper()
        {
            // skip setting up rate limit and user agent to reuse the configuration set up by Jellyfin
            // Query.DelayBetweenRequests = 1.0;
            _mbQuery = new Query();
        }

        public static bool IsServerUsingMusicBrainz(ILibraryManager libraryManager)
        {
            var anyItemsWithMbIds = libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                HasAnyProviderId = new Dictionary<string, string>
                {
                    { "MusicBrainzArtist", string.Empty },
                    { "MusicBrainzAlbumArtist", string.Empty },
                    { "MusicBrainzReleaseGroup", string.Empty },
                    { "MusicBrainzAlbum", string.Empty },
                    { "MusicBrainzTrack", string.Empty },
                    { "MusicBrainzRecording", string.Empty },
                },
                IncludeItemTypes = new[] { BaseItemKind.MusicArtist, BaseItemKind.MusicAlbum, BaseItemKind.Audio },
                Limit = 1,
            });
            return anyItemsWithMbIds.Count > 0;
        }

        public async IAsyncEnumerable<DbIsrcMusicBrainzMapping> QueryByIsrc(IEnumerable<string> isrcs, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var checkedAt = DateTime.UtcNow;
            foreach (var isrcChunk in isrcs.Distinct().Chunk(_mbQueryResultLimit))
            {
                var res = await _mbQuery.FindRecordingsAsync("isrc:" + string.Join(" OR isrc:", isrcChunk), limit: isrcChunk.Length, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (res.TotalResults == 0)
                {
                    continue;
                }

                foreach (var result in res.Results)
                {
                    foreach (var parsed in ParseMusicBrainzIsrcResult(result, checkedAt))
                    {
                        yield return parsed;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose all resources.
        /// </summary>
        /// <param name="disposing">Whether to dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _mbQuery.Dispose();
            }
        }

        private IEnumerable<DbIsrcMusicBrainzMapping> ParseMusicBrainzIsrcResult(ISearchResult<IRecording> result, DateTime checkedAt)
        {
            var rec = result.Item;

            if (rec.Releases == null || rec.Isrcs == null)
            {
                yield break;
            }

            var releases = new HashSet<Guid>();
            var relGroups = new HashSet<Guid>();
            foreach (var rel in rec.Releases)
            {
                releases.Add(rel.Id);
                if (rel.ReleaseGroup != null)
                {
                    relGroups.Add(rel.ReleaseGroup.Id);
                }
            }

            var recIsrcs = rec.Isrcs.Distinct();
            var recordings = new List<Guid> { rec.Id };
            foreach (var isrc in recIsrcs)
            {
                yield return new DbIsrcMusicBrainzMapping(-1, isrc.ToUpperInvariant().Replace("-", string.Empty, StringComparison.InvariantCulture), checkedAt, recordings, releases, relGroups);
            }
        }
    }
}
