using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Viperinius.Plugin.SpotifyImport.Api
{
    /// <summary>
    /// The API controller for missing track lists.
    /// </summary>
    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    [Authorize(Policy = "DefaultAuthorization")]
    public class MissingTrackListsController : ControllerBase
    {
        /// <summary>
        /// Gets a missing tracks list file.
        /// </summary>
        /// <param name="name">File name to get.</param>
        /// <returns>The file.</returns>
        [HttpGet($"{nameof(Viperinius)}.{nameof(Viperinius.Plugin)}.{nameof(SpotifyImport)}/MissingTracksFile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult GetListFile([FromQuery, Required] string name)
        {
            var file = MissingTrackStore.GetFileList().FirstOrDefault(f => Path.GetFileName(f) == name, string.Empty);
            if (string.IsNullOrEmpty(file))
            {
                return NotFound();
            }

            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, IODefaults.FileStreamBufferSize, FileOptions.Asynchronous);
            return File(stream, "application/json; charset=utf-8");
        }
    }
}
