using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MediaCover.Providers
{
    /// <summary>
    /// Fetches cover art from the Spotify Web API.
    /// Requires a Client ID and Client Secret from a registered Spotify Developer App.
    /// Uses the OAuth2 Client Credentials flow; token is cached in-process and
    /// proactively refreshed 60 seconds before expiry.
    ///
    /// Note: development mode apps are capped at 5 allowlisted users, which is
    /// correct for self-hosted Lidarr instances.
    /// Maximum image size returned by Spotify is 640 × 640 px.
    /// </summary>
    public class SpotifyCoverArtProvider : ICoverArtProvider
    {
        private const string TokenUrl = "https://accounts.spotify.com/api/token";
        private const string SearchUrl = "https://api.spotify.com/v1/search";

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        // Token state — guarded by _tokenLock
        private readonly object _tokenLock = new object();
        private string _accessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public string Name => "Spotify";

        public bool IsEnabled =>
            _configService.CoverArtSpotifyClientId.IsNotNullOrWhiteSpace() &&
            _configService.CoverArtSpotifyClientSecret.IsNotNullOrWhiteSpace();

        public SpotifyCoverArtProvider(IHttpClient httpClient, IConfigService configService, Logger logger)
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
                var token = GetAccessToken();
                if (token.IsNullOrWhiteSpace())
                {
                    return new List<CoverArtCandidate>();
                }

                var query = Uri.EscapeDataString($"artist:{artist} album:{album}");
                var url = $"{SearchUrl}?q={query}&type=album&limit=20";

                var request = new HttpRequest(url);
                request.SuppressHttpError = true;
                request.Headers.Set("Authorization", $"Bearer {token}");

                var response = _httpClient.Get<SpotifySearchResponse>(request);

                // Retry once on 401 (stale cached token)
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    InvalidateToken();
                    token = GetAccessToken();
                    if (token.IsNullOrWhiteSpace())
                    {
                        return new List<CoverArtCandidate>();
                    }

                    request.Headers.Set("Authorization", $"Bearer {token}");
                    response = _httpClient.Get<SpotifySearchResponse>(request);
                }

                if (response.HasHttpError || response.Resource?.Albums?.Items == null)
                {
                    return new List<CoverArtCandidate>();
                }

                var candidates = new List<CoverArtCandidate>();
                foreach (var item in response.Resource.Albums.Items)
                {
                    if (item.Images == null || !item.Images.Any())
                    {
                        continue;
                    }

                    // Largest image first (Spotify returns them in descending size order)
                    var largest = item.Images.OrderByDescending(i => i.Width ?? 0).First();
                    var thumbnail = item.Images.OrderBy(i => Math.Abs((i.Width ?? 0) - 300)).First();

                    candidates.Add(new CoverArtCandidate
                    {
                        Source = Name,
                        ImageUrl = largest.Url,
                        ThumbnailUrl = thumbnail.Url,
                        Width = largest.Width,
                        Height = largest.Height,
                        Types = new List<string> { "Front" },
                        ReleaseTitle = item.Name,
                        ReleaseUrl = item.ExternalUrls?.Spotify
                    });
                }

                return candidates;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Spotify cover art search failed for '{0} - {1}'", artist, album);
                return new List<CoverArtCandidate>();
            }
        }

        // ── Token management ─────────────────────────────────────────────────

        private string GetAccessToken()
        {
            lock (_tokenLock)
            {
                if (_accessToken.IsNotNullOrWhiteSpace() && DateTime.UtcNow < _tokenExpiry)
                {
                    return _accessToken;
                }

                return RefreshToken();
            }
        }

        private void InvalidateToken()
        {
            lock (_tokenLock)
            {
                _accessToken = null;
                _tokenExpiry = DateTime.MinValue;
            }
        }

        private string RefreshToken()
        {
            try
            {
                var clientId = _configService.CoverArtSpotifyClientId;
                var clientSecret = _configService.CoverArtSpotifyClientSecret;

                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

                var request = new HttpRequest(TokenUrl);
                request.Method = HttpMethod.Post;
                request.SuppressHttpError = true;
                request.Headers.Set("Authorization", $"Basic {credentials}");
                request.Headers.ContentType = "application/x-www-form-urlencoded";
                request.SetContent("grant_type=client_credentials");

                var response = _httpClient.Execute(request);

                if (response.HasHttpError)
                {
                    _logger.Warn("Spotify token request failed with HTTP {0}", response.StatusCode);
                    return null;
                }

                var tokenResponse = Json.Deserialize<SpotifyTokenResponse>(response.Content);
                if (tokenResponse?.AccessToken.IsNullOrWhiteSpace() == true)
                {
                    return null;
                }

                // Cache with 60 s buffer before nominal expiry
                _accessToken = tokenResponse.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);

                return _accessToken;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to obtain Spotify access token");
                return null;
            }
        }

        // ── JSON response models ─────────────────────────────────────────────

        private class SpotifyTokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }

            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }
        }

        private class SpotifySearchResponse
        {
            [JsonProperty("albums")]
            public SpotifyAlbumPage Albums { get; set; }
        }

        private class SpotifyAlbumPage
        {
            [JsonProperty("items")]
            public List<SpotifyAlbum> Items { get; set; }
        }

        private class SpotifyAlbum
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("images")]
            public List<SpotifyImage> Images { get; set; }

            [JsonProperty("external_urls")]
            public SpotifyExternalUrls ExternalUrls { get; set; }
        }

        private class SpotifyImage
        {
            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("width")]
            public int? Width { get; set; }

            [JsonProperty("height")]
            public int? Height { get; set; }
        }

        private class SpotifyExternalUrls
        {
            [JsonProperty("spotify")]
            public string Spotify { get; set; }
        }
    }
}
