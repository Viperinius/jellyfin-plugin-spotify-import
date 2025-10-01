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
    [Authorize]
    public class DebugController : ControllerBase
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;

        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

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
        /// <param name="nameOrId">Track name or id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file.</returns>
        [HttpPost($"{nameof(Viperinius)}.{nameof(Viperinius.Plugin)}.{nameof(SpotifyImport)}/Debug/DumpTrackRefs")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> DumpRefsToFile([FromQuery, Required] string nameOrId, CancellationToken cancellationToken)
        {
            IReadOnlyList<BaseItem> queriedItems = new List<BaseItem>();
            if (Guid.TryParse(nameOrId, out var id))
            {
                var item = _libraryManager.GetItemById(id);
                if (item != null)
                {
                    queriedItems =
                    [
                        item
                    ];
                }
            }
            else
            {
                var queryResult = _libraryManager.GetItemsResult(new InternalItemsQuery
                {
                    Name = nameOrId,
                    MediaTypes = new[] { MediaType.Audio },
                    Recursive = true
                });
                queriedItems = queryResult.Items;
            }

            var alreadyIncludedIds = new List<Guid>();
            var dumpFilePath = MissingTrackStore.GetFilePath($"DEBUG_REF_{nameOrId}");

            var trackRefs = new List<TrackRef>();
            foreach (var item in queriedItems)
            {
                if (alreadyIncludedIds.Contains(item.Id))
                {
                    continue;
                }

                if (item is not Audio audio)
                {
                    continue;
                }

                var trackRef = new TrackRef
                {
                    Id = audio.Id.ToString(),
                    Name = audio.Name,
                    MediaType = audio.MediaType.ToString(),
                    ParentId = audio.ParentId.ToString(),
                    IsTopParent = audio.IsTopParent,
                    DisplayParentId = audio.DisplayParentId.ToString(),
                };

                // add album entity if set
                if (audio.AlbumEntity != null)
                {
                    var albumEntityRef = new ItemRef
                    {
                        Id = audio.AlbumEntity.Id.ToString(),
                        Name = audio.AlbumEntity.Name,
                        MediaType = audio.AlbumEntity.MediaType.ToString(),
                        ParentId = audio.AlbumEntity.ParentId.ToString(),
                        IsTopParent = audio.AlbumEntity.IsTopParent,
                        DisplayParentId = audio.AlbumEntity.DisplayParentId.ToString(),
                    };
                    trackRef.Parents.Add("Album", albumEntityRef);
                }

                // get track parents
                int ii = 1;
                var nextParent = item;
                var currentRef = trackRef;
                var nextRef = new TrackRef();
                while (nextParent != null && !nextRef.IsTopParent && ii <= 10)
                {
                    nextParent = _libraryManager.GetItemById(nextParent.ParentId);
                    if (nextParent == null)
                    {
                        continue;
                    }

                    nextRef = new TrackRef
                    {
                        Id = nextParent.Id.ToString(),
                        Name = nextParent.Name,
                        MediaType = nextParent.MediaType.ToString(),
                        ParentId = nextParent.ParentId.ToString(),
                        IsTopParent = nextParent.IsTopParent,
                        DisplayParentId = nextParent.DisplayParentId.ToString(),
                    };
                    currentRef.Parents.Add($"TrackParent{ii}", nextRef);
                    currentRef = nextRef;
                    ii++;
                }

                // reverse search now
                var artistNames = audio.Artists;
                var artistIndex = 0;
                foreach (var artistName in artistNames)
                {
                    var artistResult = _libraryManager.GetArtists(new InternalItemsQuery
                    {
                        SearchTerm = artistName[0..Math.Min(artistName.Length, 5)],
                    }).Items.Select(i => i.Item).ToList();
                    var resultCount1 = artistResult.Count;
                    artistResult =
                    [
                        ..artistResult,
                        .._libraryManager.GetItemsResult(new InternalItemsQuery
                        {
                            SearchTerm = artistName[0..Math.Min(artistName.Length, 5)],
                        }).Items,
                    ];
                    var resultCount2 = artistResult.Count - resultCount1;

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
                            MediaType = artist.MediaType.ToString(),
                            ParentId = artist.ParentId.ToString(),
                            IsTopParent = artist.IsTopParent,
                            DisplayParentId = artist.DisplayParentId.ToString(),
                            Children = artist.Children.Select(c => new ItemRef
                            {
                                Id = c.Id.ToString(),
                                Name = c.Name,
                                MediaType = c.MediaType.ToString(),
                            }).ToList(),
                            RecursiveChildren = artist.RecursiveChildren.Select(c => new ItemRef
                            {
                                Id = c.Id.ToString(),
                                Name = c.Name,
                                MediaType = c.MediaType.ToString(),
                            }).ToList(),
                        };

                        // album by album artists
                        var albums = _libraryManager.GetItemList(new InternalItemsQuery
                        {
                            AlbumArtistIds = new[] { artist.Id },
                            IncludeItemTypes = new[] { BaseItemKind.MusicAlbum }
                        });
                        foreach (var album in albums)
                        {
                            artistRef.AlbumByAlbumArtist.Add(new ItemRef
                            {
                                Id = album.Id.ToString(),
                                Name = album.Name,
                                MediaType = album.MediaType.ToString(),
                                ParentId = album.ParentId.ToString(),
                                IsTopParent = album.IsTopParent,
                                DisplayParentId = album.DisplayParentId.ToString(),
                            });
                        }

                        trackRef.Artists.Add($"Artist{jj}/{resultCount1}/{resultCount2}[{artistIndex}]", artistRef);

                        jj++;
                    }

                    artistIndex++;
                }

                trackRefs.Add(trackRef);
                alreadyIncludedIds.Add(item.Id);
            }

            using var writer = System.IO.File.Create(dumpFilePath);
            using var textWriter = new StreamWriter(writer)
            {
                AutoFlush = true
            };

            var root = new Dictionary<string, List<TrackRef>>
                {
                    { "TrackRefs", trackRefs },
                };

            await JsonSerializer.SerializeAsync(writer, root, _options, cancellationToken).ConfigureAwait(false);

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

        private class TrackRef : ItemRef
        {
            public Dictionary<string, ItemRef> Parents { get; set; } = new Dictionary<string, ItemRef>();

            public Dictionary<string, ArtistRef> Artists { get; set; } = new Dictionary<string, ArtistRef>();
        }

        private class ArtistRef : ItemRef
        {
            public List<ItemRef> Children { get; set; } = new List<ItemRef>();

            public List<ItemRef> RecursiveChildren { get; set; } = new List<ItemRef>();

            public List<ItemRef> AlbumByAlbumArtist { get; set; } = new List<ItemRef>();
        }
    }
}
