using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Viperinius.Plugin.SpotifyImport.Api
{
    /// <summary>
    /// The API controller for manual map entries.
    /// </summary>
    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    [Authorize]
    public class ManualMapController : ControllerBase
    {
        private readonly ManualMapStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManualMapController"/> class.
        /// </summary>
        /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
        public ManualMapController(ILoggerFactory loggerFactory)
        {
            _store = new ManualMapStore(loggerFactory.CreateLogger<ManualMapStore>());
        }

        /// <summary>
        /// Gets the map entries.
        /// </summary>
        /// <returns>The entries.</returns>
        [HttpGet($"{nameof(Viperinius)}.{nameof(Viperinius.Plugin)}.{nameof(SpotifyImport)}/ManualTrackMap")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult GetAll()
        {
            if (!_store.Load())
            {
                return NotFound();
            }

            return Ok(_store.ToList());
        }

        /// <summary>
        /// Update the map.
        /// </summary>
        /// <param name="items">New map entries.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Empty.</returns>
        [HttpPost($"{nameof(Viperinius)}.{nameof(Viperinius.Plugin)}.{nameof(SpotifyImport)}/ManualTrackMap")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult SetAll([FromBody, Required] IList<ManualMapTrack> items, CancellationToken cancellationToken)
        {
            if (items == null)
            {
                return BadRequest();
            }

            _store.Clear();
            _store.AddRange(items);

            if (_store.Save())
            {
                return NoContent();
            }

            return StatusCode(500);
        }
    }
}
