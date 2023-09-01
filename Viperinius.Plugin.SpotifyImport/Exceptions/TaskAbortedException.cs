using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viperinius.Plugin.SpotifyImport.Exceptions
{
    /// <summary>
    /// Exception thrown when a task failed.
    /// </summary>
    [Serializable]
    public class TaskAbortedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TaskAbortedException"/> class.
        /// </summary>
        public TaskAbortedException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskAbortedException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        public TaskAbortedException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskAbortedException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="innerException">Inner Exception.</param>
        public TaskAbortedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskAbortedException"/> class.
        /// </summary>
        /// <param name="serializationInfo">Info.</param>
        /// <param name="streamingContext">Context.</param>
        protected TaskAbortedException(
            System.Runtime.Serialization.SerializationInfo serializationInfo,
            System.Runtime.Serialization.StreamingContext streamingContext) : base(serializationInfo, streamingContext)
        {
        }
    }
}
