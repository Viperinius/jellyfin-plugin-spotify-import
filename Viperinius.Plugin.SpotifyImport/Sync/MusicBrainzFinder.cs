using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Viperinius.Plugin.SpotifyImport.Utils;

namespace Viperinius.Plugin.SpotifyImport.Sync
{
    internal class MusicBrainzFinder : ITrackMatchFinder
    {
        private readonly ILibraryManager _libraryManager;
        private readonly DbRepository _dbRepository;

        private bool? _anyLibraryUsesMusicBrainz;

        public MusicBrainzFinder(
            ILibraryManager libraryManager,
            DbRepository dbRepository)
        {
            _libraryManager = libraryManager;
            _dbRepository = dbRepository;
        }

        public bool AnyLibraryUsesMusicBrainz
        {
            get
            {
                _anyLibraryUsesMusicBrainz ??= Utils.MusicBrainz.MusicBrainzHelper.IsServerUsingMusicBrainz(_libraryManager);
                return (bool)_anyLibraryUsesMusicBrainz;
            }
        }

        public bool IsEnabled => Plugin.Instance?.Configuration.EnabledTrackMatchFinders.HasFlag(EnabledTrackMatchFinders.MusicBrainz) ?? false;

        public Audio? FindTrack(string providerId, ProviderTrackInfo providerTrackInfo)
        {
            if (!IsEnabled || !AnyLibraryUsesMusicBrainz || string.IsNullOrWhiteSpace(providerTrackInfo.IsrcId))
            {
                return null;
            }

            var foundIsrcMappings = _dbRepository.GetIsrcMusicBrainzMapping(isrc: providerTrackInfo.IsrcId, hasAnyMbIdsSet: true);
            var mbRecordings = new HashSet<string>();
            var mbReleases = new HashSet<string>();
            var mbTracks = new HashSet<string>();
            var mbReleaseGroups = new HashSet<string>();
            foreach (var mapping in foundIsrcMappings)
            {
                mbRecordings.UnionWith(mapping.MusicBrainzRecordingIds.Select(r => r.ToString()));
                mbReleases.UnionWith(mapping.MusicBrainzReleaseIds.Select(r => r.ToString()));
                mbTracks.UnionWith(mapping.MusicBrainzTrackIds.Select(t => t.ToString()));
                mbReleaseGroups.UnionWith(mapping.MusicBrainzReleaseGroupIds.Select(r => r.ToString()));
            }

            if (mbRecordings.Count > 0 || mbTracks.Count > 0)
            {
                // library manager does not seem to support querying multiple ProviderIds with same key, so every different MB id has to be done in a separate query...
                // to speed this up in some way, try to fill query with one of each "direct hit" ProviderId types if available
                for (var ii = 0; ii < Math.Max(mbRecordings.Count, mbTracks.Count); ii++)
                {
                    var idDict = new Dictionary<string, string>();
                    if (ii < mbRecordings.Count)
                    {
                        idDict.Add("MusicBrainzRecording", mbRecordings.ElementAt(ii));
                    }

                    if (ii < mbTracks.Count)
                    {
                        idDict.Add("MusicBrainzTrack", mbTracks.ElementAt(ii));
                    }

                    var directHits = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        HasAnyProviderId = idDict,
                        IncludeItemTypes = new[] { BaseItemKind.Audio },
                        Limit = 1,
                    });

                    if (directHits.Count > 0 && directHits[0] is Audio directHit)
                    {
                        return directHit;
                    }
                }
            }

            // library manager does not seem to support querying multiple ProviderIds with same key, so every different MB id has to be done in a separate query...
            // to speed this up in some way, try to fill query with one of each ProviderId types if available
            var matchCandidates = new List<(int, ItemMatchLevel, Audio)>();
            for (var ii = 0; ii < Math.Max(mbReleases.Count, mbReleaseGroups.Count); ii++)
            {
                var idDict = new Dictionary<string, string>();
                if (ii < mbReleases.Count)
                {
                    idDict.Add("MusicBrainzAlbum", mbReleases.ElementAt(ii));
                }

                if (ii < mbReleaseGroups.Count)
                {
                    idDict.Add("MusicBrainzReleaseGroup", mbReleaseGroups.ElementAt(ii));
                }

                var tracksWithMbIds = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                {
                    HasAnyProviderId = idDict,
                    IncludeItemTypes = new[] { BaseItemKind.Audio },
                });
                foreach (var track in tracksWithMbIds)
                {
                    var matchInfo = MatchTrack((track as Audio)!, providerTrackInfo);
                    if (matchInfo != null)
                    {
                        matchCandidates.Add(((int, ItemMatchLevel, Audio))matchInfo);
                    }
                }
            }

            if (matchCandidates.Count > 0)
            {
                // sort by prio first, then match level
                matchCandidates.Sort((a, b) =>
                {
                    var result = a.Item1.CompareTo(b.Item1);
                    if (result == 0)
                    {
                        result = a.Item2.CompareTo(b.Item2);
                    }

                    return result;
                });
                return matchCandidates.First().Item3;
            }

            return null;
        }

        private (int Prio, ItemMatchLevel Level, Audio Item)? MatchTrack(Audio track, ProviderTrackInfo providerTrackInfo)
        {
            var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
            var checkResult = TrackComparison.TrackNameEqual(track, providerTrackInfo, level);
            if (!checkResult.ComparisonResult || checkResult.MatchedLevel == null || checkResult.MatchedPrio == null)
            {
                return null;
            }

            var prio = (int)checkResult.MatchedPrio;
            level = (ItemMatchLevel)checkResult.MatchedLevel;
            return (prio, level, track);
        }
    }
}
