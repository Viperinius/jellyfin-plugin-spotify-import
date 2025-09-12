using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Utils
{
    internal class DbIsrcMusicBrainzMapping
    {
        public DbIsrcMusicBrainzMapping(long id, string isrc, DateTime lastCheck, IEnumerable<Guid> mbRecordingIds, IEnumerable<Guid> mbReleaseIds, IEnumerable<Guid> mbTrackIds, IEnumerable<Guid> mbReleaseGroupIds)
        {
            Id = id;
            Isrc = isrc;
            MusicBrainzRecordingIds = mbRecordingIds.ToList();
            MusicBrainzReleaseIds = mbReleaseIds.ToList();
            MusicBrainzTrackIds = mbTrackIds.ToList();
            MusicBrainzReleaseGroupIds = mbReleaseGroupIds.ToList();
            LastCheck = lastCheck;
        }

        public long Id { get; set; }

        public string Isrc { get; set; }

        public List<Guid> MusicBrainzRecordingIds { get; set; }

        public List<Guid> MusicBrainzReleaseIds { get; set; }

        public List<Guid> MusicBrainzTrackIds { get; set; }

        public List<Guid> MusicBrainzReleaseGroupIds { get; set; }

        public DateTime LastCheck { get; set; }
    }
}
