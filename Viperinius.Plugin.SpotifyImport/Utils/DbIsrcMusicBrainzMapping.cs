using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Utils
{
    internal class DbIsrcMusicBrainzMapping
    {
        public DbIsrcMusicBrainzMapping(long id, string isrc, DateTime lastCheck, Guid? mbRecordingId = null, Guid? mbReleaseId = null, Guid? mbReleaseGroupId = null)
        {
            Id = id;
            Isrc = isrc;
            MusicBrainzRecordingId = mbRecordingId;
            MusicBrainzReleaseId = mbReleaseId;
            MusicBrainzReleaseGroupId = mbReleaseGroupId;
            LastCheck = lastCheck;
        }

        public long Id { get; set; }

        public string Isrc { get; set; }

        public Guid? MusicBrainzRecordingId { get; set; }

        public Guid? MusicBrainzReleaseId { get; set; }

        public Guid? MusicBrainzReleaseGroupId { get; set; }

        public DateTime LastCheck { get; set; }
    }
}
