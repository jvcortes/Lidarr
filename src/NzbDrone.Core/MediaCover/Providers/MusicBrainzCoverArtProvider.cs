using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.MediaCover.Providers
{
    /// <summary>
    /// Fetches cover art from the Cover Art Archive (coverartarchive.org),
    /// using the MusicBrainz Release Group ID already stored in Album.ForeignAlbumId.
    /// No authentication required. Always enabled.
    ///
    /// Fast path:  GET /release-group/{foreignAlbumId}  (4 s timeout)
    /// Fallback:   GET /release/{foreignReleaseId}       per-pressing (4 s each)
    ///
    /// Successful responses are cached in memory for 24 hours.
    /// </summary>
    public class MusicBrainzCoverArtProvider : ICoverArtProvider
    {
        private const string BaseUrl = "https://coverartarchive.org";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);

        private readonly IHttpClient _httpClient;
        private readonly ICached<List<CoverArtCandidate>> _cache;
        private readonly Logger _logger;

        public string Name => "MusicBrainz";
        public bool IsEnabled => true;

        public MusicBrainzCoverArtProvider(IHttpClient httpClient, ICacheManager cacheManager, Logger logger)
        {
            _httpClient = httpClient;
            _cache = cacheManager.GetCache<List<CoverArtCandidate>>(GetType());
            _logger = logger;
        }

        public List<CoverArtCandidate> GetCandidates(
            string artist,
            string album,
            string foreignAlbumId,
            IEnumerable<string> foreignReleaseIds)
        {
            if (foreignAlbumId.IsNullOrWhiteSpace())
            {
                return new List<CoverArtCandidate>();
            }

            return _cache.Get(foreignAlbumId, () => FetchCandidates(foreignAlbumId, foreignReleaseIds), CacheLifetime);
        }

        private List<CoverArtCandidate> FetchCandidates(
            string foreignAlbumId,
            IEnumerable<string> foreignReleaseIds)
        {
            // Fast path: release-group aggregation endpoint
            var images = FetchFromReleaseGroup(foreignAlbumId);
            if (images.Count > 0)
            {
                return images;
            }

            // Fallback: per-release queries using IDs already in the DB
            _logger.Debug("MusicBrainz release-group CAA query returned no results for {0}; falling back to per-release queries", foreignAlbumId);

            var results = new List<CoverArtCandidate>();
            foreach (var releaseId in foreignReleaseIds ?? Enumerable.Empty<string>())
            {
                var releaseImages = FetchFromRelease(releaseId);
                results.AddRange(releaseImages);
            }

            return results;
        }

        private List<CoverArtCandidate> FetchFromReleaseGroup(string foreignAlbumId)
        {
            try
            {
                var request = BuildRequest($"{BaseUrl}/release-group/{foreignAlbumId}");
                var response = _httpClient.Get<CaaResponse>(request);

                if (response.HasHttpError || response.Resource?.Images == null)
                {
                    return new List<CoverArtCandidate>();
                }

                return MapImages(response.Resource.Images);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "CAA release-group query failed for {0}", foreignAlbumId);
                return new List<CoverArtCandidate>();
            }
        }

        private List<CoverArtCandidate> FetchFromRelease(string foreignReleaseId)
        {
            try
            {
                var request = BuildRequest($"{BaseUrl}/release/{foreignReleaseId}");
                var response = _httpClient.Get<CaaResponse>(request);

                if (response.HasHttpError || response.Resource?.Images == null)
                {
                    return new List<CoverArtCandidate>();
                }

                return MapImages(response.Resource.Images);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "CAA per-release query failed for {0}", foreignReleaseId);
                return new List<CoverArtCandidate>();
            }
        }

        private HttpRequest BuildRequest(string url)
        {
            var request = new HttpRequest(url);
            request.AllowAutoRedirect = true;
            request.SuppressHttpError = true;
            request.RequestTimeout = RequestTimeout;
            return request;
        }

        private List<CoverArtCandidate> MapImages(List<CaaImage> images)
        {
            // Front images first, then approved, then the rest.
            return images
                .OrderByDescending(i => i.Front)
                .ThenByDescending(i => i.Approved)
                .Select(i => new CoverArtCandidate
                {
                    Source = Name,
                    ImageUrl = i.Image,
                    ThumbnailUrl = i.Thumbnails?.Large ?? i.Thumbnails?.P500 ?? i.Image,
                    Types = i.Types ?? new List<string>(),
                    ReleaseTitle = null,
                    ReleaseUrl = null
                })
                .Where(c => c.ImageUrl.IsNotNullOrWhiteSpace())
                .ToList();
        }

        // ── JSON response models ─────────────────────────────────────────────

        private class CaaResponse
        {
            [JsonProperty("images")]
            public List<CaaImage> Images { get; set; }
        }

        private class CaaImage
        {
            [JsonProperty("image")]
            public string Image { get; set; }

            [JsonProperty("thumbnails")]
            public CaaThumbnails Thumbnails { get; set; }

            [JsonProperty("types")]
            public List<string> Types { get; set; }

            [JsonProperty("front")]
            public bool Front { get; set; }

            [JsonProperty("approved")]
            public bool Approved { get; set; }

            [JsonProperty("comment")]
            public string Comment { get; set; }
        }

        private class CaaThumbnails
        {
            [JsonProperty("250")]
            public string P250 { get; set; }

            [JsonProperty("500")]
            public string P500 { get; set; }

            [JsonProperty("1200")]
            public string P1200 { get; set; }

            [JsonProperty("small")]
            public string Small { get; set; }

            [JsonProperty("large")]
            public string Large { get; set; }
        }
    }
}
