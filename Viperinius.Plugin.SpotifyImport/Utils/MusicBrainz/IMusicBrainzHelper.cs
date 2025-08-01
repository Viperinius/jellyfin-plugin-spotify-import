using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;

namespace Viperinius.Plugin.SpotifyImport.Utils.MusicBrainz
{
    internal interface IMusicBrainzHelper : IDisposable
    {
        static bool IsServerUsingMusicBrainz(ILibraryManager libraryManager) => false;

        IAsyncEnumerable<DbIsrcMusicBrainzMapping> QueryByIsrc(IEnumerable<string> isrcs, CancellationToken cancellationToken);
    }
}
