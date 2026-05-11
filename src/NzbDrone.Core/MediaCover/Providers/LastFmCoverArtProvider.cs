using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MediaCover.Providers
{
    /// <summary>
    /// Fetches cover art from the Last.fm API.
    /// Requires a free API key registered at last.fm/api.
    /// Image quality is low (≤ 300 px) — this provider is used as a fallback only.
    /// Results with Last.fm's generic placeholder image are filtered out.
    /// </summary>
    public class LastFmCoverArtProvider : ICoverArtProvider
    {
        // Hash that appears in Last.fm's generic "no image" placeholder URL
        private const string PlaceholderHash = "2a96cbd8b46e442fc41c2b86b821562f";

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public string Name => "LastFm";
        public bool IsEnabled => _configService.CoverArtLastFmApiKey.IsNotNullOrWhiteSpace();

        public LastFmCoverArtProvider(IHttpClient httpClient, IConfigService configService, Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        public List<CoverArtCandidate> GetCandidates(
            string artist,
            string album,
            string foreignAlbumId,
            IEnumerable<string> foreignReleaseIds)
        {
            try
            {
                var apiKey = _configService.CoverArtLastFmApiKey;
                var url = "https://ws.audioscrobbler.com/2.0/" +
                          $"?method=album.search" +
                          $"&album={Uri.EscapeDataString(album)}" +
                          $"&artist={Uri.EscapeDataString(artist)}" +
                          $"&api_key={apiKey}" +
                          $"&format=json" +
                          $"&limit=10";

                var request = new HttpRequest(url);
                request.SuppressHttpError = true;

                var response = _httpClient.Get<LastFmSearchResponse>(request);

                if (response.HasHttpError || response.Resource?.Results?.AlbumMatches?.Albums == null)
                {
                    return new List<CoverArtCandidate>();
                }

                var candidates = new List<CoverArtCandidate>();
                foreach (var lfmAlbum in response.Resource.Results.AlbumMatches.Albums)
                {
                    var imageUrl = BestImage(lfmAlbum.Images);
                    if (imageUrl.IsNullOrWhiteSpace() || imageUrl.Contains(PlaceholderHash))
                    {
                        continue;
                    }

                    candidates.Add(new CoverArtCandidate
                    {
                        Source = Name,
                        ImageUrl = imageUrl,
                        ThumbnailUrl = imageUrl,
                        Types = new List<string> { "Front" },
                        ReleaseTitle = lfmAlbum.Name,
                        ReleaseUrl = lfmAlbum.Url
                    });
                }

                return candidates;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Last.fm cover art search failed for '{0} - {1}'", artist, album);
                return new List<CoverArtCandidate>();
            }
        }

        /// <summary>Returns the largest non-placeholder image URL from a Last.fm image list.</summary>
        private static string BestImage(List<LastFmImage> images)
        {
            if (images == null || !images.Any())
            {
                return null;
            }

            // Last.fm sizes in descending preference
            var preferredSizes = new[] { "mega", "extralarge", "large", "medium", "small" };

            foreach (var size in preferredSizes)
            {
                var match = images.FirstOrDefault(i =>
                    string.Equals(i.Size, size, StringComparison.OrdinalIgnoreCase));

                if (match?.Text.IsNotNullOrWhiteSpace() == true)
                {
                    return match.Text;
                }
            }

            return images.Select(i => i.Text).FirstOrDefault(u => u.IsNotNullOrWhiteSpace());
        }

        // ── JSON response models ─────────────────────────────────────────────

        private class LastFmSearchResponse
        {
            [JsonProperty("results")]
            public LastFmResults Results { get; set; }
        }

        private class LastFmResults
        {
            [JsonProperty("albummatches")]
            public LastFmAlbumMatches AlbumMatches { get; set; }
        }

        private class LastFmAlbumMatches
        {
            [JsonProperty("album")]
            public List<LastFmAlbum> Albums { get; set; }
        }

        private class LastFmAlbum
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("artist")]
            public string Artist { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("image")]
            public List<LastFmImage> Images { get; set; }
        }

        private class LastFmImage
        {
            [JsonProperty("#text")]
            public string Text { get; set; }

            [JsonProperty("size")]
            public string Size { get; set; }
        }
    }
}
