using System.ComponentModel.DataAnnotations;

namespace Lidarr.Api.V1.Albums
{
    /// <summary>Request body for PUT /api/v1/albums/{id}/coverart.</summary>
    public class AlbumCoverArtResource
    {
        /// <summary>Full-resolution cover image URL to pin for this album.</summary>
        [Required]
        public string CoverUrl { get; set; }
    }
}
