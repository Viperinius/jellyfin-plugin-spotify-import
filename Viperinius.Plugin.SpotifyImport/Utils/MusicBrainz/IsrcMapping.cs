using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Viperinius.Plugin.SpotifyImport.Utils.MusicBrainz
{
    internal class IsrcMapping : IDisposable
    {
        private readonly int _mbRetryDays = 14;

        private readonly ILogger _logger;
        private readonly DbRepository _dbRepository;
        private readonly IMusicBrainzHelper _musicBrainzHelper;

        public IsrcMapping(ILogger logger, DbRepository dbRepository, IMusicBrainzHelper mbHelper)
        {
            _logger = logger;
            _dbRepository = dbRepository;
            _musicBrainzHelper = mbHelper;
        }

        public async Task UpdateIsrcMusicBrainzMappings(IList<ProviderPlaylistInfo> playlists, CancellationToken? cancellationToken = null)
        {
            _logger.LogInformation("Starting to update mappings between ISRC and MusicBrainz IDs for {Amount} playlists", playlists.Count);

            var tmpIsrcs = playlists.SelectMany(p => p.Tracks.Select(t => t.IsrcId).OfType<string>());
            var isrcs = new HashSet<string>(tmpIsrcs);

            var checkedAt = DateTime.UtcNow;
            var retryCheckDate = checkedAt.AddDays(-_mbRetryDays);

            // query db for items that have mb ids set or lastcheck was NOT over x days ago
            var existingDoneEntries = _dbRepository.GetIsrcMusicBrainzMapping(hasAnyMbIdsSet: true, minLastCheck: retryCheckDate, logicalAnd: false).ToList();
            isrcs.ExceptWith(existingDoneEntries.Select(m => m.Isrc));

            // query db for items without mb ids and lastcheck over x days ago
            var existingUnfinishedEntries = _dbRepository.GetIsrcMusicBrainzMapping(hasAnyMbIdsSet: false, maxLastCheck: retryCheckDate).ToList();
            var newDoneIsrcs = new HashSet<string>();

            await foreach (var release in _musicBrainzHelper.QueryByIsrc(isrcs, cancellationToken ?? CancellationToken.None).ConfigureAwait(false))
            {
                if (!isrcs.Contains(release.Isrc) || existingDoneEntries.Contains(release))
                {
                    continue;
                }

                _dbRepository.DeleteIsrcMusicBrainzMapping(existingUnfinishedEntries.Where(e => e.Isrc == release.Isrc).Select(m => m.Id).ToList());

                if (_dbRepository.UpsertIsrcMusicBrainzMapping(release) == null)
                {
                    _logger.LogWarning("Failed to save ISRC<->MusicBrainz mapping to DB for ISRC {Isrc}", release.Isrc);
                }

                newDoneIsrcs.Add(release.Isrc);
            }

            foreach (var notFoundIsrc in isrcs.Except(newDoneIsrcs))
            {
                // delete any previous placeholders for this isrc
                _dbRepository.DeleteIsrcMusicBrainzMapping(existingUnfinishedEntries.Where(e => e.Isrc == notFoundIsrc).Select(m => m.Id).ToList());

                // insert placeholder
                if (_dbRepository.UpsertIsrcMusicBrainzMapping(new DbIsrcMusicBrainzMapping(-1, notFoundIsrc, checkedAt)) == null)
                {
                    _logger.LogWarning("Failed to save placeholder ISRC<->MusicBrainz mapping to DB for ISRC {Isrc}", notFoundIsrc);
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
                _musicBrainzHelper.Dispose();
            }
        }
    }
}
