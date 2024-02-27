using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Tasks
{
    /// <summary>
    /// Scheduled task to import playlists from Spotify.
    /// </summary>
    public class DebugDumpMetadataTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugDumpMetadataTask"/> class.
        /// </summary>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
        public DebugDumpMetadataTask(ILibraryManager libraryManager, IUserManager userManager)
        {
            _libraryManager = libraryManager;
            _userManager = userManager;
        }

        /// <inheritdoc/>
        public string Name => "[DebugSpotifyImport] Dump Music Metadata To File";

        /// <inheritdoc/>
        public string Key => "ViperiniusSpotifyDebugDumpMetadataTask";

        /// <inheritdoc/>
        public string Description => "Export metadata of all music / audio items to files";

        /// <inheritdoc/>
        public string Category => "Playlists";

        /// <inheritdoc/>
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var libraries = _libraryManager.RootFolder.GetChildren(_userManager.Users.First(), true).Where(l => l is CollectionFolder cf && cf.CollectionType == "music");

            foreach (var library in libraries)
            {
                var dumpFilePath = MissingTrackStore.GetFilePath($"DEBUG_LIB_{library.Name}");
                var dumps = new List<ProviderTrackInfo>();

                var queryResult = _libraryManager.GetItemsResult(new InternalItemsQuery
                {
                    Parent = library,
                    MediaTypes = new[] { "Audio" },
                    IsFolder = false,
                    Recursive = true
                });

                foreach (var item in queryResult.Items)
                {
                    if (item is not Audio audio)
                    {
                        continue;
                    }

                    dumps.Add(new ProviderTrackInfo
                    {
                        Name = audio.Name,
                        AlbumName = audio.Album,
                        ArtistNames = audio.Artists.ToList(),
                        AlbumArtistNames = audio.AlbumArtists.ToList()
                    });
                }

                await MissingTrackStore.WriteFile(dumpFilePath, dumps).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}
