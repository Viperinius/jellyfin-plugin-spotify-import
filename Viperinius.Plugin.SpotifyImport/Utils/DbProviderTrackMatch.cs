using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Viperinius.Plugin.SpotifyImport.Matchers;

namespace Viperinius.Plugin.SpotifyImport.Utils
{
    internal class DbProviderTrackMatch
    {
        public DbProviderTrackMatch(long id, Guid matchId, ItemMatchLevel level, ItemMatchCriteria criteria)
        {
            Id = id;
            MatchId = matchId;
            Level = level;
            Criteria = criteria;
        }

        public long Id { get; set; }

        public Guid MatchId { get; set; }

        public ItemMatchLevel Level { get; set; }

        public ItemMatchCriteria Criteria { get; set; }
    }
}
