using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;

namespace Viperinius.Plugin.SpotifyImport.Sync
{
    internal interface ITrackMatchFinder
    {
        bool IsEnabled { get; }

        Audio? FindTrack(string providerId, ProviderTrackInfo providerTrackInfo);
    }
}
