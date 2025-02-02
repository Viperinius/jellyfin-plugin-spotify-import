using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Viperinius.Plugin.SpotifyImport.Configuration;
using Viperinius.Plugin.SpotifyImport.Spotify;

namespace Viperinius.Plugin.SpotifyImport.Tasks
{
    /// <summary>
    /// Scheduled task to import playlists from Spotify.
    /// </summary>
    public class SpotifyImportTask : IScheduledTask
    {
        private const string SpotifyOwnedIdPrefix = "37i9dQZ";

        private readonly ILogger<SpotifyImportTask> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IPlaylistManager _playlistManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpotifyImportTask"/> class.
        /// </summary>
        /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
        /// <param name="playlistManager">Instance of the <see cref="IPlaylistManager"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
        public SpotifyImportTask(
            ILoggerFactory loggerFactory,
            IPlaylistManager playlistManager,
            ILibraryManager libraryManager,
            IUserManager userManager)
        {
            _logger = loggerFactory.CreateLogger<SpotifyImportTask>();
            _loggerFactory = loggerFactory;
            _playlistManager = playlistManager;
            _libraryManager = libraryManager;
            _userManager = userManager;
        }

        /// <inheritdoc/>
        public string Name => "Import Spotify playlists";

        /// <inheritdoc/>
        public string Key => "ViperiniusSpotifyImportSpotifyImportTask";

        /// <inheritdoc/>
        public string Description => "Create / Update Jellyfin playlists based on the items of Spotify playlists.";

        /// <inheritdoc/>
        public string Category => "Playlists";

        /// <inheritdoc/>
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (!(Plugin.Instance?.IsInitialised ?? false))
            {
                const string ErrorMsg = "Plugin was not initialised correctly, aborting task!";
                _logger.LogError(ErrorMsg);
                throw new Exceptions.TaskAbortedException(ErrorMsg);
            }

            var followedUsers = Plugin.Instance?.Configuration.Users ?? Array.Empty<TargetUserConfiguration>();
            var playlistIds = new List<string>();
            var playlistIdsAlt = new List<string>();
            foreach (var playlist in Plugin.Instance?.Configuration.Playlists ?? Array.Empty<TargetPlaylistConfiguration>())
            {
                if (playlist.Id.StartsWith(SpotifyOwnedIdPrefix, StringComparison.InvariantCulture))
                {
                    // playlist is owned by Spotify
                    playlistIdsAlt.Add(playlist.Id);
                }
                else
                {
                    playlistIds.Add(playlist.Id);
                }
            }

            if (followedUsers.Length == 0 && playlistIds.Count == 0)
            {
                return;
            }

            var manualMapStore = new ManualMapStore(_loggerFactory.CreateLogger<ManualMapStore>());
            if (!manualMapStore.Load())
            {
                _logger.LogWarning("Failed to load manual track map, but continuing without it");
            }

            using var db = new Utils.DbRepository(Plugin.Instance!.DbPath);

            var spotify = new SpotifyPlaylistProvider(db, _loggerFactory.CreateLogger<SpotifyPlaylistProvider>(), _loggerFactory.CreateLogger<SpotifyLogger>());
            spotify.SetUpProvider();
            var spotifyAlt = new SpotifyAltPlaylistProvider(db, _loggerFactory.CreateLogger<SpotifyAltPlaylistProvider>(), _loggerFactory.CreateLogger<Utils.HttpRequest>());
            spotifyAlt.SetUpProvider();

            // check if any users are given whose playlists need to be included
            var userPlaylistMapping = new Dictionary<string, string>();
            if (followedUsers.Length > 0)
            {
                foreach (var user in followedUsers)
                {
                    // get playlists created / shared with user
                    var userPlaylists = await spotify.GetUserPlaylistIds(user, cancellationToken).ConfigureAwait(false) ?? new List<string>();
                    // get playlists owned by spotify
                    userPlaylists.AddRange(await spotifyAlt.GetUserPlaylistIds(user, cancellationToken).ConfigureAwait(false) ?? new List<string>());

                    userPlaylists.ForEach(id =>
                    {
                        if (id.StartsWith(SpotifyOwnedIdPrefix, StringComparison.InvariantCulture))
                        {
                            // playlist is owned by Spotify
                            playlistIdsAlt.Add(id);
                        }
                        else
                        {
                            playlistIds.Add(id);
                        }

                        if (userPlaylistMapping.ContainsKey(id))
                        {
                            _logger.LogWarning("Found and ignored duplicate playlist id {Id} of user {User}", id, user.Id);
                        }
                        else
                        {
                            userPlaylistMapping.Add(id, user.Id);
                        }
                    });
                }

                playlistIds = playlistIds.Distinct().ToList();
                playlistIdsAlt = playlistIdsAlt.Distinct().ToList();
            }

            await spotify.PopulatePlaylists(playlistIds, cancellationToken).ConfigureAwait(false);
            foreach (var playlist in spotify.Playlists)
            {
                playlistIds.Remove(playlist.Id);
            }

            // try to get any missing and spotify owned playlists using alternative method
            await spotifyAlt.PopulatePlaylists([..playlistIdsAlt, ..playlistIds], cancellationToken).ConfigureAwait(false);

            var playlistSync = new PlaylistSync(
                    _loggerFactory.CreateLogger<PlaylistSync>(),
                    _playlistManager,
                    _libraryManager,
                    _userManager,
                    spotify.Playlists.Concat(spotifyAlt.Playlists),
                    userPlaylistMapping,
                    manualMapStore);
            await playlistSync.Execute(cancellationToken).ConfigureAwait(false);

            // replace old trackset in db with current one
            spotify.UpdatePlaylistTracksInDb(cancellationToken);
            spotifyAlt.UpdatePlaylistTracksInDb(cancellationToken);
        }

        /// <inheritdoc/>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromHours(3).Ticks,
                    MaxRuntimeTicks = TimeSpan.FromHours(3).Ticks,
                }
            };
        }
    }
}
