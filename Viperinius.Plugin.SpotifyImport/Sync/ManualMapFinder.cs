using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;

namespace Viperinius.Plugin.SpotifyImport.Sync
{
    internal class ManualMapFinder : ITrackMatchFinder
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ManualMapStore _manualMapStore;

        public ManualMapFinder(
            ILibraryManager libraryManager,
            ManualMapStore manualMapStore)
        {
            _libraryManager = libraryManager;
            _manualMapStore = manualMapStore;
        }

        public bool IsEnabled => true;

        public Audio? FindTrack(string providerId, ProviderTrackInfo providerTrackInfo)
        {
            if (!IsEnabled)
            {
                return null;
            }

            var manualTrack = _manualMapStore.GetByProviderTrackInfo(providerTrackInfo);
            if (manualTrack?.Provider.Equals(providerTrackInfo) ?? false)
            {
                var jellyfinId = Guid.Parse(manualTrack.Jellyfin.Track);
                return _libraryManager.GetItemById<Audio>(jellyfinId);
            }

            return null;
        }
    }
}
