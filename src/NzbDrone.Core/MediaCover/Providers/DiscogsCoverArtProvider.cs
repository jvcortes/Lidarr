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
    /// Fetches cover art from the Discogs API.
    /// Requires a Personal Access Token (PAT) set in Settings > Cover Art.
    ///
    /// Flow:
    ///   1. Search releases by artist + album title.
    ///   2. Fetch /releases/{id} for the top results to obtain the full images[] array.
    ///
    /// Rate limit: 60 req/min authenticated. Throttled by limiting to 5 search results.
    /// </summary>
    public class DiscogsCoverArtProvider : ICoverArtProvider
    {
        private const string SearchUrl = "https://api.discogs.com/database/search";
        private const string ReleaseUrl = "https://api.discogs.com/releases/{0}";
        private const int MaxSearchResults = 5;

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public string Name => "Discogs";
        public bool IsEnabled => _configService.CoverArtDiscogsToken.IsNotNullOrWhiteSpace();

        public DiscogsCoverArtProvider(IHttpClient httpClient, IConfigService configService, Logger logger)
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
                var token = _configService.CoverArtDiscogsToken;
                var searchResults = SearchReleases(artist, album, token);

                var candidates = new List<CoverArtCandidate>();
                foreach (var result in searchResults.Take(MaxSearchResults))
                {
                    var releaseImages = FetchReleaseImages(result.Id, result.Title, token);
                    candidates.AddRange(releaseImages);
                }

                return candidates;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Discogs cover art search failed for '{0} - {1}'", artist, album);
                return new List<CoverArtCandidate>();
            }
        }

        private List<DiscogsSearchResult> SearchReleases(string artist, string album, string token)
        {
            var url = $"{SearchUrl}?artist={Uri.EscapeDataString(artist)}" +
                      $"&release_title={Uri.EscapeDataString(album)}" +
                      $"&type=release&per_page={MaxSearchResults}&token={token}";

            var request = new HttpRequest(url);
            request.SuppressHttpError = true;

            var response = _httpClient.Get<DiscogsSearchResponse>(request);

            if (response.HasHttpError || response.Resource?.Results == null)
            {
                _logger.Warn("Discogs search returned HTTP {0}", response.StatusCode);
                return new List<DiscogsSearchResult>();
            }

            return response.Resource.Results;
        }

        private List<CoverArtCandidate> FetchReleaseImages(int releaseId, string releaseTitle, string token)
        {
            var url = $"{string.Format(ReleaseUrl, releaseId)}?token={token}";
            var request = new HttpRequest(url);
            request.SuppressHttpError = true;

            var response = _httpClient.Get<DiscogsRelease>(request);

            if (response.HasHttpError || response.Resource?.Images == null)
            {
                return new List<CoverArtCandidate>();
            }

            return response.Resource.Images
                .Where(i => i.Uri.IsNotNullOrWhiteSpace())
                .Select(i => new CoverArtCandidate
                {
                    Source = Name,
                    ImageUrl = i.Uri,
                    ThumbnailUrl = i.Uri150.IsNotNullOrWhiteSpace() ? i.Uri150 : i.Uri,
                    Width = i.Width > 0 ? (int?)i.Width : null,
                    Height = i.Height > 0 ? (int?)i.Height : null,
                    Types = new List<string> { MapDiscogsType(i.Type) },
                    ReleaseTitle = releaseTitle,
                    ReleaseUrl = $"https://www.discogs.com/release/{releaseId}"
                })
                .ToList();
        }

        private static string MapDiscogsType(string discogsType) =>
            discogsType switch
            {
                "primary" => "Front",
                "secondary" => "Back",
                _ => discogsType ?? "Unknown"
            };

        // ── JSON response models ─────────────────────────────────────────────

        private class DiscogsSearchResponse
        {
            [JsonProperty("results")]
            public List<DiscogsSearchResult> Results { get; set; }
        }

        private class DiscogsSearchResult
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("cover_image")]
            public string CoverImage { get; set; }
        }

        private class DiscogsRelease
        {
            [JsonProperty("images")]
            public List<DiscogsImage> Images { get; set; }
        }

        private class DiscogsImage
        {
            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("uri")]
            public string Uri { get; set; }

            [JsonProperty("uri150")]
            public string Uri150 { get; set; }

            [JsonProperty("width")]
            public int Width { get; set; }

            [JsonProperty("height")]
            public int Height { get; set; }
        }
    }
}
