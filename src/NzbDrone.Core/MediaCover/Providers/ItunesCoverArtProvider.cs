using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.MediaCover.Providers
{
    /// <summary>
    /// Fetches cover art from the iTunes Search API.
    /// No authentication required. Always enabled.
    /// Returns images up to ~4000 px by manipulating the artwork URL suffix.
    /// </summary>
    public class ItunesCoverArtProvider : ICoverArtProvider
    {
        private static readonly Regex ArtworkDimensionRegex =
            new Regex(@"\d+x\d+bb\.(jpg|png)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public string Name => "iTunes";
        public bool IsEnabled => true;

        public ItunesCoverArtProvider(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
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
                var term = Uri.EscapeDataString($"{artist} {album}");
                var request = new HttpRequest(
                    $"https://itunes.apple.com/search?term={term}&entity=album&limit=25");
                request.AllowAutoRedirect = true;
                request.SuppressHttpError = true;

                var response = _httpClient.Get<ItunesSearchResponse>(request);

                if (response.HasHttpError || response.Resource?.Results == null)
                {
                    return new List<CoverArtCandidate>();
                }

                var candidates = new List<CoverArtCandidate>();
                foreach (var result in response.Resource.Results
                             .Where(r => r.ArtworkUrl100.IsNotNullOrWhiteSpace()))
                {
                    var thumbnail = result.ArtworkUrl100;
                    var fullRes = ArtworkDimensionRegex.Replace(result.ArtworkUrl100, "3000x3000bb.$1");

                    candidates.Add(new CoverArtCandidate
                    {
                        Source = Name,
                        ImageUrl = fullRes,
                        ThumbnailUrl = thumbnail,
                        Width = 3000,
                        Height = 3000,
                        Types = new List<string> { "Front" },
                        ReleaseTitle = result.CollectionName,
                        ReleaseUrl = result.CollectionViewUrl
                    });
                }

                return candidates;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "iTunes cover art search failed for '{0} - {1}'", artist, album);
                return new List<CoverArtCandidate>();
            }
        }

        // ── JSON response models ─────────────────────────────────────────────

        private class ItunesSearchResponse
        {
            [JsonProperty("resultCount")]
            public int ResultCount { get; set; }

            [JsonProperty("results")]
            public List<ItunesAlbumResult> Results { get; set; }
        }

        private class ItunesAlbumResult
        {
            [JsonProperty("artistName")]
            public string ArtistName { get; set; }

            [JsonProperty("collectionName")]
            public string CollectionName { get; set; }

            [JsonProperty("artworkUrl100")]
            public string ArtworkUrl100 { get; set; }

            [JsonProperty("collectionViewUrl")]
            public string CollectionViewUrl { get; set; }
        }
    }
}
