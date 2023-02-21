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
using Viperinius.Plugin.SpotifyImport.Spotify;

namespace Viperinius.Plugin.SpotifyImport.Tasks
{
    /// <summary>
    /// Scheduled task to import playlists from Spotify.
    /// </summary>
    public class SpotifyImportTask : IScheduledTask
    {
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
            var playlistIds = Plugin.Instance?.Configuration.PlaylistIds.ToList() ?? new List<string>();

            if (!playlistIds.Any())
            {
                return;
            }

            var spotify = new SpotifyPlaylistProvider(_loggerFactory.CreateLogger<SpotifyPlaylistProvider>(), _loggerFactory.CreateLogger<SpotifyLogger>());
            spotify.SetUpProvider();
            await spotify.PopulatePlaylists(playlistIds, cancellationToken).ConfigureAwait(false);

            var playlistSync = new PlaylistSync(
                                                _loggerFactory.CreateLogger<PlaylistSync>(),
                                                _playlistManager,
                                                _libraryManager,
                                                _userManager,
                                                spotify.Playlists);
            await playlistSync.Execute(cancellationToken).ConfigureAwait(false);
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
                    MaxRuntimeTicks = TimeSpan.FromHours(1).Ticks,
                }
            };
        }
    }
}
