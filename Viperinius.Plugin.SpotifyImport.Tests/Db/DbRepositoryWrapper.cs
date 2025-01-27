using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Viperinius.Plugin.SpotifyImport.Utils;

namespace Viperinius.Plugin.SpotifyImport.Tests.Db
{
    internal class DbRepositoryWrapper : DbRepository
    {
        public DbRepositoryWrapper(string path) : base(path)
        {
        }

        public SqliteConnection WrappedConnection => Connection;

        public static DbRepositoryWrapper GetInstance()
        {
            return new DbRepositoryWrapper(":memory:");
        }

        public string GetInsertProviderPlaylistCmd()
        {
            return InsertProviderPlaylistCmd;
        }

        public string GetInsertProviderTrackCmd()
        {
            return InsertProviderTrackCmd;
        }

        public string GetInsertProviderPlaylistTrackCmd()
        {
            return InsertProviderPlaylistTrackCmd;
        }
    }
}
