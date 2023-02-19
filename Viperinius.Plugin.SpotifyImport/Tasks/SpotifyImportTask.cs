using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Tasks
{
    /// <summary>
    /// Scheduled task to import playlists from Spotify.
    /// </summary>
    public class SpotifyImportTask : IScheduledTask
    {
        /// <inheritdoc/>
        public string Name => "Import Spotify playlists";

        /// <inheritdoc/>
        public string Key => "ViperiniusSpotifyImportSpotifyImportTask";

        /// <inheritdoc/>
        public string Description => "Create / Update Jellyfin playlists based on the items of Spotify playlists.";

        /// <inheritdoc/>
        public string Category => "Playlists";

        /// <inheritdoc/>
        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
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
