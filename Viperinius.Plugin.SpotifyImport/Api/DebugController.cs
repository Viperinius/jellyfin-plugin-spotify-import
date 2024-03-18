using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
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
    public class DebugController : ControllerBase
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugController"/> class.
        /// </summary>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
        public DebugController(ILibraryManager libraryManager, IUserManager userManager)
        {
            _libraryManager = libraryManager;
            _userManager = userManager;
        }

        /// <summary>
        /// Dump music metadata to file.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file.</returns>
        [HttpPost($"{nameof(Viperinius)}.{nameof(Viperinius.Plugin)}.{nameof(SpotifyImport)}/Debug/DumpMetadata")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> DumpMetadata(CancellationToken cancellationToken)
        {
            var task = new Tasks.DebugDumpMetadataTask(_libraryManager, _userManager);
            await task.ExecuteAsync(new Progress<double>(), cancellationToken).ConfigureAwait(false);
            return NoContent();
        }

        /// <summary>
        /// Dump all references of a music track to file.
        /// </summary>
        /// <param name="name">Track name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file.</returns>
        [HttpPost($"{nameof(Viperinius)}.{nameof(Viperinius.Plugin)}.{nameof(SpotifyImport)}/Debug/DumpTrackRefs")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> DumpRefsToFile([FromQuery, Required] string name, CancellationToken cancellationToken)
        {
            var queryResult = _libraryManager.GetItemsResult(new InternalItemsQuery
            {
                Name = name,
                MediaTypes = new[] { "Audio" },
                Recursive = true
            });

            var alreadyIncludedIds = new List<Guid>();

            var itemIndex = 0;
            foreach (var item in queryResult.Items)
            {
                if (alreadyIncludedIds.Contains(item.Id))
                {
                    continue;
                }

                var dumpFilePath = MissingTrackStore.GetFilePath($"DEBUG_REF_{item.Id}");

                if (item is not Audio audio)
                {
                    continue;
                }

                var trackRef = new ItemRef
                {
                    Id = audio.Id.ToString(),
                    Name = audio.Name,
                    MediaType = audio.MediaType,
                    ParentId = audio.ParentId.ToString(),
                    IsTopParent = audio.IsTopParent,
                    DisplayParentId = audio.DisplayParentId.ToString(),
                };
                var trackRefs = new Dictionary<string, ItemRef>
                {
                    { $"Track[{itemIndex}]", trackRef },
                };

                // add album entity if set
                if (audio.AlbumEntity != null)
                {
                    var albumEntityRef = new ItemRef
                    {
                        Id = audio.AlbumEntity.Id.ToString(),
                        Name = audio.AlbumEntity.Name,
                        MediaType = audio.AlbumEntity.MediaType,
                        ParentId = audio.AlbumEntity.ParentId.ToString(),
                        IsTopParent = audio.AlbumEntity.IsTopParent,
                        DisplayParentId = audio.AlbumEntity.DisplayParentId.ToString(),
                    };
                    trackRefs.Add($"TrackAlbumEntity[{itemIndex}]", albumEntityRef);
                }

                // get track parents
                int ii = 1;
                var nextParent = item;
                var nextRef = new ItemRef();
                while (!nextRef.IsTopParent && ii <= 10)
                {
                    nextParent = _libraryManager.GetItemById(nextParent.ParentId);
                    nextRef = new ItemRef
                    {
                        Id = nextParent.Id.ToString(),
                        Name = nextParent.Name,
                        MediaType = nextParent.MediaType,
                        ParentId = nextParent.ParentId.ToString(),
                        IsTopParent = nextParent.IsTopParent,
                        DisplayParentId = nextParent.DisplayParentId.ToString(),
                    };
                    trackRefs.Add($"TrackParent{ii}[{itemIndex}]", nextRef);
                    ii++;
                }

                // reverse search now
                var artistNames = audio.Artists;
                var artistRefs = new Dictionary<string, ArtistRef>();
                var artistIndex = 0;
                foreach (var artistName in artistNames)
                {
                    var artistResult = _libraryManager.GetArtists(new InternalItemsQuery
                    {
                        SearchTerm = artistName[0..Math.Min(artistName.Length, 5)],
                    }).Items.Select(i => i.Item);
                    var resultCount1 = artistResult.Count();
                    artistResult = artistResult.Concat(_libraryManager.GetItemsResult(new InternalItemsQuery
                    {
                        SearchTerm = artistName[0..Math.Min(artistName.Length, 5)],
                        MediaTypes = new[] { "MusicArtist" },
                    }).Items);
                    var resultCount2 = artistResult.Count() - resultCount1;

                    var jj = 1;
                    foreach (var artistItem in artistResult)
                    {
                        if (artistItem is not MusicArtist artist)
                        {
                            continue;
                        }

                        var artistRef = new ArtistRef
                        {
                            Id = artist.Id.ToString(),
                            Name = artist.Name,
                            MediaType = artist.MediaType,
                            ParentId = artist.ParentId.ToString(),
                            IsTopParent = artist.IsTopParent,
                            DisplayParentId = artist.DisplayParentId.ToString(),
                            Children = artist.Children.Select(c => new ItemRef
                            {
                                Id = c.Id.ToString(),
                                Name = c.Name,
                                MediaType = c.MediaType,
                            }).ToList(),
                            RecursiveChildren = artist.RecursiveChildren.Select(c => new ItemRef
                            {
                                Id = c.Id.ToString(),
                                Name = c.Name,
                                MediaType = c.MediaType,
                            }).ToList(),
                        };
                        artistRefs.Add($"Artist{jj}[{itemIndex}][{artistIndex}]/{resultCount1}/{resultCount2}", artistRef);

                        // album by album artists
                        var albums = _libraryManager.GetItemList(new InternalItemsQuery
                        {
                            AlbumArtistIds = new[] { artist.Id },
                            IncludeItemTypes = new[] { BaseItemKind.MusicAlbum }
                        });
                        for (int kk = 0; kk < albums.Count; kk++)
                        {
                            var album = albums[kk];
                            trackRefs.Add($"AlbumByAlbumArtist{jj}-{kk}[{itemIndex}][{artistIndex}]/{artistName}", new ItemRef
                            {
                                Id = album.Id.ToString(),
                                Name = album.Name,
                                MediaType = album.MediaType,
                                ParentId = album.ParentId.ToString(),
                                IsTopParent = album.IsTopParent,
                                DisplayParentId = album.DisplayParentId.ToString(),
                            });
                        }

                        jj++;
                    }

                    artistIndex++;
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                using var writer = System.IO.File.Create(dumpFilePath);
                using var textWriter = new StreamWriter(writer)
                {
                    AutoFlush = true
                };
                textWriter.WriteLine("[");
                await JsonSerializer.SerializeAsync(writer, trackRefs, options, cancellationToken).ConfigureAwait(false);
                textWriter.WriteLine(",");
                await JsonSerializer.SerializeAsync(writer, artistRefs, options, cancellationToken).ConfigureAwait(false);
                textWriter.WriteLine("]");
                alreadyIncludedIds.Add(item.Id);
                itemIndex++;
            }

            return NoContent();
        }

        private class ItemRef
        {
            public string Id { get; set; } = "notset";

            public string Name { get; set; } = "notset";

            public string MediaType { get; set; } = "notset";

            public string ParentId { get; set; } = "notset";

            public bool IsTopParent { get; set; }

            public string DisplayParentId { get; set; } = "notset";
        }

        private class ArtistRef : ItemRef
        {
            public List<ItemRef> Children { get; set; } = new List<ItemRef>();

            public List<ItemRef> RecursiveChildren { get; set; } = new List<ItemRef>();
        }
    }
}
