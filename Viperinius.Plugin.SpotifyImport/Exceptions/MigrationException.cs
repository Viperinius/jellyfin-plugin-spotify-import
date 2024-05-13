using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Exceptions
{
    /// <summary>
    /// Exception thrown when a migration failed.
    /// </summary>
    [Serializable]
    public class MigrationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MigrationException"/> class.
        /// </summary>
        public MigrationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MigrationException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        public MigrationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MigrationException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="innerException">Inner Exception.</param>
        public MigrationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
