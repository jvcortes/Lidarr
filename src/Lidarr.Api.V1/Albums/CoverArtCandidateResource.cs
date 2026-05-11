using System.Collections.Generic;
using Lidarr.Http.REST;

namespace Lidarr.Api.V1.Albums
{
    /// <summary>
    /// A single cover art candidate returned by GET /api/v1/albums/{id}/coverartcandidates.
    /// ThumbnailUrl is a proxied local URL (/MediaCover/proxy?url=…) so the browser
    /// can display it without CORS restrictions.
    /// </summary>
    public class CoverArtCandidateResource : RestResource
    {
        public string Source { get; set; }
        public string ImageUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public List<string> Types { get; set; }
        public string ReleaseTitle { get; set; }
        public string ReleaseUrl { get; set; }
    }
}
