using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Configuration;
using Viperinius.Plugin.SpotifyImport.Matchers;
using Viperinius.Plugin.SpotifyImport.Utils;

namespace Viperinius.Plugin.SpotifyImport.Sync
{
    internal class PlaylistSync
    {
        private const int MaxSearchChars = 5;

        private readonly ILogger<PlaylistSync> _logger;
        private readonly IPlaylistManager _playlistManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IList<ProviderPlaylistInfo> _providerPlaylists;
        private readonly Dictionary<string, string> _userPlaylistIds;
        private readonly ManualMapStore _manualMapStore;
        private readonly DbRepository _dbRepository;

        private readonly CacheFinder _cacheFinder;
        private readonly ManualMapFinder _manualMapFinder;
        private readonly MusicBrainzFinder _musicBrainzFinder;
        private readonly StringMatchFinder _stringMatchFinder;

        public PlaylistSync(
            ILogger<PlaylistSync> logger,
            IPlaylistManager playlistManager,
            ILibraryManager libraryManager,
            IUserManager userManager,
            IList<ProviderPlaylistInfo> playlists,
            Dictionary<string, string> userPlaylistIds,
            ManualMapStore manualMapStore,
            DbRepository dbRepository)
        {
            _logger = logger;
            _playlistManager = playlistManager;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _providerPlaylists = playlists;
            _userPlaylistIds = userPlaylistIds;
            _manualMapStore = manualMapStore;
            _dbRepository = dbRepository;

            _cacheFinder = new CacheFinder(_libraryManager, _dbRepository);
            _manualMapFinder = new ManualMapFinder(_libraryManager, _manualMapStore);
            _musicBrainzFinder = new MusicBrainzFinder(_libraryManager, _dbRepository);
            _stringMatchFinder = new StringMatchFinder(_logger, _libraryManager);
        }

        public async Task Execute(IProgress<double> progress, CancellationToken cancellationToken = default)
        {
            var progressValue = 0d;
            var providerPlaylistCount = _providerPlaylists.Count;
            var providerPlaylistIndexProgress = 0;
            foreach (var providerPlaylist in _providerPlaylists)
            {
                cancellationToken.ThrowIfCancellationRequested();
                providerPlaylistIndexProgress++;
                var nextProgress = 100d * providerPlaylistIndexProgress / providerPlaylistCount;

                // get the targeted playlist configuration
                var targetConfig = Plugin.Instance?.Configuration.Playlists.FirstOrDefault(p => p.Id == providerPlaylist.Id);

                if (targetConfig == null || string.IsNullOrEmpty(targetConfig.Id))
                {
                    // is this a playlist specified by user?
                    var userId = _userPlaylistIds.GetValueOrDefault(providerPlaylist.Id);
                    var targetUser = Plugin.Instance?.Configuration.Users.FirstOrDefault(u => u.Id == userId);

                    if (targetUser == null || string.IsNullOrEmpty(targetUser.Id))
                    {
                        _logger.LogError("Failed to get target playlist configuration for playlist {Id}", providerPlaylist.Id);
                        continue;
                    }

                    targetConfig = new TargetPlaylistConfiguration
                    {
                        Id = targetUser.Id,
                        Name = string.Empty,
                        UserName = targetUser.UserName,
                        IsPrivate = targetUser.IsPrivate,
                        RecreateFromScratch = false,
                    };
                }

                // get the targeted user
                var user = GetUser(targetConfig.UserName);
                if (user == null)
                {
                    _logger.LogError("Failed to get user {Name}", targetConfig.UserName);
                    continue;
                }

                // determine jellyfin playlist name
                var jfPlaylistName = targetConfig.Name;
                if (string.IsNullOrEmpty(jfPlaylistName))
                {
                    jfPlaylistName = providerPlaylist.Name;
                }

                var playlist = await GetOrCreatePlaylistByName(jfPlaylistName, user, targetConfig.IsPrivate, targetConfig.RecreateFromScratch).ConfigureAwait(false);

                if (playlist == null)
                {
                    _logger.LogError("Failed to get Jellyfin playlist with name {Name}", jfPlaylistName);
                    continue;
                }

                var updateReason = ItemUpdateType.None;

                if (providerPlaylist.ImageUrl != null && !playlist.HasImage(ImageType.Primary, 0))
                {
                    playlist.AddImage(new MediaBrowser.Controller.Entities.ItemImageInfo
                    {
                        Path = providerPlaylist.ImageUrl.ToString()
                    });
                    updateReason |= ItemUpdateType.ImageUpdate;
                }

                if (!string.IsNullOrWhiteSpace(providerPlaylist.Description) && providerPlaylist.Description != playlist.Overview)
                {
                    playlist.Overview = providerPlaylist.Description;
                    updateReason |= ItemUpdateType.MetadataEdit;
                }

                if ((!targetConfig.IsPrivate) != playlist.OpenAccess)
                {
                    playlist.OpenAccess = !targetConfig.IsPrivate;
                    updateReason |= ItemUpdateType.MetadataEdit;
                }

                if (updateReason != ItemUpdateType.None)
                {
                    await _libraryManager.UpdateItemAsync(playlist, playlist.GetParent(), updateReason, cancellationToken).ConfigureAwait(false);
                }

                await FindTracksAndAddToPlaylist(playlist, providerPlaylist, user, progress, new Tuple<double, double>(progressValue, nextProgress), cancellationToken).ConfigureAwait(false);

                progressValue = nextProgress;
                progress.Report(progressValue);
            }
        }

        private async Task FindTracksAndAddToPlaylist(Playlist playlist, ProviderPlaylistInfo providerPlaylistInfo, User user, IProgress<double> progress, Tuple<double, double> progressRange, CancellationToken cancellationToken)
        {
            var newTracks = new List<Guid>();
            var missingTracks = new List<ProviderTrackInfo>();

            var providerTrackProgressIndex = 0;
            var progressValue = progressRange.Item1;
            foreach (var providerTrack in providerPlaylistInfo.Tracks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                providerTrackProgressIndex++;
                if (providerTrack == null)
                {
                    continue;
                }

                if (!CheckPlaylistForTrack(playlist, user, providerPlaylistInfo.ProviderName, providerTrack))
                {
                    var track = GetMatchingTrack(providerPlaylistInfo.ProviderName, providerTrack, out var failedCriterium);
                    if (failedCriterium != ItemMatchCriteria.None && (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false))
                    {
                        _logger.LogInformation(
                            "{Criterium} did not match for track {Name} [{Album}][{Artist}]",
                            failedCriterium,
                            providerTrack.Name,
                            providerTrack.AlbumName,
                            string.Join("#", providerTrack.ArtistNames));
                    }

                    if (track != null)
                    {
                        newTracks.Add(track.Id);
                    }
                    else
                    {
                        missingTracks.Add(providerTrack);
                    }
                }

                progressValue = ((double)providerTrackProgressIndex / providerPlaylistInfo.Tracks.Count * (progressRange.Item2 - progressRange.Item1)) + progressRange.Item1;
                progress.Report(progressValue);
            }

            await _playlistManager.AddItemToPlaylistAsync(playlist.Id, newTracks, user.Id).ConfigureAwait(false);
            await UpdatePlaylistCompletenessDesc(playlist, providerPlaylistInfo, missingTracks.Count, providerPlaylistInfo.Tracks.Count, cancellationToken).ConfigureAwait(false);

            if ((Plugin.Instance?.Configuration.GenerateMissingTrackLists ?? false) && missingTracks.Count > 0)
            {
                var missingFilePath = MissingTrackStore.GetFilePath(playlist.Name);

                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("Write missing tracks file with {Count} entries to {Path}", missingTracks.Count, missingFilePath);
                }

                await MissingTrackStore.WriteFile(missingFilePath, missingTracks).ConfigureAwait(false);
            }
        }

        protected Audio? GetMatchingTrack(string providerId, ProviderTrackInfo providerTrackInfo, out ItemMatchCriteria failedMatchCriterium)
        {
            failedMatchCriterium = ItemMatchCriteria.None;
            if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
            {
                _logger.LogInformation(
                    "Now processing provider track {Name} [{Album}][{AlbumArtist}][{Artist}] (Id: {Id})",
                    providerTrackInfo.Name,
                    providerTrackInfo.AlbumName,
                    string.Join("#", providerTrackInfo.AlbumArtistNames),
                    string.Join("#", providerTrackInfo.ArtistNames),
                    providerTrackInfo.Id);
            }

            // 1. check cache
            var match = _cacheFinder.FindTrack(providerId, providerTrackInfo);
            if (match != null)
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("Found cached match for track with id {Id}", match.Id);
                }

                return match;
            }

            // 2. check manual mappings
            match = _manualMapFinder.FindTrack(providerId, providerTrackInfo);
            if (match != null)
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("Found manual mapping for track with id {Id}", match.Id);
                }

                SaveMatchInCache(providerId, providerTrackInfo, match.Id);
                return match;
            }

            // 3. check by isrc
            match = _musicBrainzFinder.FindTrack(providerId, providerTrackInfo);
            if (match != null)
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("Found match for track with id {Id} by ISRC and MusicBrainz id", match.Id);
                }

                SaveMatchInCache(providerId, providerTrackInfo, match.Id);
                return match;
            }

            // 4.1 check using legacy string comparisons
            if (Plugin.Instance?.Configuration.UseLegacyMatching ?? false)
            {
                var legacyMatch = GetMatchingTrackLegacy(providerTrackInfo, out failedMatchCriterium);
                if (legacyMatch != null)
                {
                    SaveMatchInCache(providerId, providerTrackInfo, legacyMatch.Id);
                }

                return legacyMatch;
            }

            // 4.2 check using string comparisons
            match = _stringMatchFinder.FindTrack(providerId, providerTrackInfo);
            failedMatchCriterium = _stringMatchFinder.LastFailedCriteria;
            if (match != null)
            {
                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("Found match for track with id {Id} by string comparison", match.Id);
                }

                SaveMatchInCache(providerId, providerTrackInfo, match.Id);
                return match;
            }

            return match;
        }

        private Audio? GetMatchingTrackLegacy(ProviderTrackInfo providerTrackInfo, out ItemMatchCriteria failedMatchCriterium)
        {
            failedMatchCriterium = ItemMatchCriteria.None;
            if (!(Plugin.Instance?.Configuration.UseLegacyMatching ?? false))
            {
                return null;
            }

            var queryResult = _libraryManager.GetItemsResult(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                SearchTerm = providerTrackInfo.Name[0..Math.Min(providerTrackInfo.Name.Length, MaxSearchChars)],
                MediaTypes = new[] { MediaType.Audio }
            });

            if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
            {
                _logger.LogInformation("> Found {Count} tracks when searching for {Name}", queryResult.Items.Count, providerTrackInfo.Name);
            }

            foreach (var item in queryResult.Items)
            {
                if (item is not Audio audioItem)
                {
                    continue;
                }

                if (ItemMatchesTrackInfo(audioItem, providerTrackInfo, out failedMatchCriterium))
                {
                    if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                    {
                        _logger.LogInformation("> Found matching track {Name} {Id}", audioItem.Name, audioItem.Id);
                    }

                    return audioItem;
                }
            }

            return null;
        }

        private bool CheckPlaylistForTrack(Playlist playlist, User user, string providerId, ProviderTrackInfo providerTrackInfo)
        {
            var match = _cacheFinder.FindTrack(providerId, providerTrackInfo);
            foreach (var item in playlist.GetChildren(user, false, null))
            {
                if (item is not Audio audioItem)
                {
                    continue;
                }

                if (match?.Id == audioItem.Id)
                {
                    return true;
                }

                var manualTrack = _manualMapStore.GetByTrackId(audioItem.Id);
                if (manualTrack?.Provider.Equals(providerTrackInfo) ?? false)
                {
                    return true;
                }

                if (ItemMatchesTrackInfo(audioItem, providerTrackInfo, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ItemMatchesTrackInfo(Audio audioItem, ProviderTrackInfo trackInfo, out ItemMatchCriteria failedCriterium)
        {
            var level = Plugin.Instance?.Configuration.ItemMatchLevel ?? ItemMatchLevel.Default;
            failedCriterium = ItemMatchCriteria.None;

            if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.Artists) ?? false) && !TrackComparison.ArtistOneContained(audioItem, trackInfo, level))
            {
                failedCriterium = ItemMatchCriteria.Artists;
                return false;
            }

            if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumName) ?? false) && !TrackComparison.AlbumNameEqual(audioItem, trackInfo, level).ComparisonResult)
            {
                failedCriterium = ItemMatchCriteria.AlbumName;
                return false;
            }

            if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.AlbumArtists) ?? false) && !TrackComparison.AlbumArtistOneContained(audioItem, trackInfo, level))
            {
                failedCriterium = ItemMatchCriteria.AlbumArtists;
                return false;
            }

            if ((Plugin.Instance?.Configuration.ItemMatchCriteria.HasFlag(ItemMatchCriteria.TrackName) ?? false) && !TrackComparison.TrackNameEqual(audioItem, trackInfo, level).ComparisonResult)
            {
                failedCriterium = ItemMatchCriteria.TrackName;
                return false;
            }

            return true;
        }

        private async Task<Playlist?> GetOrCreatePlaylistByName(string name, User user, bool shouldBePrivate, bool deleteExistingPlaylist)
        {
            var playlists = _playlistManager.GetPlaylists(user.Id);
            var playlist = playlists.FirstOrDefault(p => p.Name == name);

            if (playlist != null && deleteExistingPlaylist)
            {
                _libraryManager.DeleteItem(playlist, new DeleteOptions { DeleteFileLocation = true }, true);

                if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
                {
                    _logger.LogInformation("Deleted existing playlist {Name} ({Id})", playlist.Name, playlist.Id);
                }

                playlist = null;
            }

            if (playlist == null)
            {
                var result = await _playlistManager.CreatePlaylist(new MediaBrowser.Model.Playlists.PlaylistCreationRequest
                {
                    Name = name,
                    MediaType = MediaType.Audio,
                    UserId = user.Id,
                    Public = !shouldBePrivate,
                }).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(result.Id))
                {
                    playlists = _playlistManager.GetPlaylists(user.Id);
                    playlist = playlists.Where(p => p.Id.ToString().Replace("-", string.Empty, StringComparison.InvariantCulture) == result.Id).FirstOrDefault((Playlist?)null);
                }
            }

            if (playlist == null)
            {
                return null;
            }

            // don't just return the previous playlist instance because .GetManageableItems() returns "garbage" LinkedChild ids for that one (TODO: is this a jellyfin bug?)
            return _libraryManager.GetItemById(playlist.Id) as Playlist;
        }

        private async Task UpdatePlaylistCompletenessDesc(Playlist playlist, ProviderPlaylistInfo providerPlaylistInfo, int missingTracks, int totalTracks, CancellationToken cancellationToken)
        {
            if (!(Plugin.Instance?.Configuration.ShowCompletenessInformation ?? false))
            {
                return;
            }

            if (Plugin.Instance?.Configuration.EnableVerboseLogging ?? false)
            {
                _logger.LogInformation("Update completeness for {Name} (Provider Id {Id})", playlist.Name, providerPlaylistInfo.Id);
            }

            playlist.Tagline = $"Synced {totalTracks - missingTracks} out of {totalTracks} tracks " +
                               $"from {providerPlaylistInfo.ProviderName.Replace("Alt", string.Empty, StringComparison.InvariantCulture)} " +
                               $"playlist {providerPlaylistInfo.Id} (at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [UTC])";

            await _libraryManager.UpdateItemAsync(playlist, playlist.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        }

        private User? GetUser(string? username = null)
        {
            if (!string.IsNullOrWhiteSpace(username))
            {
                return _userManager.GetUserByName(username);
            }

            return _userManager.Users.FirstOrDefault(u => u?.HasPermission(PermissionKind.IsAdministrator) ?? false, null);
        }

        protected bool SaveMatchInCache(string providerId, ProviderTrackInfo providerTrackInfo, Guid jellyfinTrackId)
        {
            var trackDbId = _dbRepository.GetProviderTrackDbId(providerId, providerTrackInfo.Id);
            if (trackDbId == null)
            {
                return false;
            }

            var insertedId = _dbRepository.InsertProviderTrackMatch(
                (long)trackDbId,
                jellyfinTrackId.ToString(),
                Plugin.Instance!.Configuration.ItemMatchLevel,
                Plugin.Instance.Configuration.ItemMatchCriteria);
            return insertedId != null;
        }
    }
}
