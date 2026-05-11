using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.MediaCover
{
    public interface ICoverArtAggregatorService
    {
        List<CoverArtCandidate> GetCandidates(Album album);
    }

    /// <summary>
    /// Fans out to all registered ICoverArtProvider implementations in parallel,
    /// merges and deduplicates the results, and returns a sorted candidate list.
    ///
    /// Sort order:
    ///   1. iTunes and MusicBrainz (highest image quality) — sorted by width desc within tier
    ///   2. Discogs and Spotify — sorted by width desc
    ///   3. Last.fm — always last (lowest image quality)
    /// </summary>
    public class CoverArtAggregatorService : ICoverArtAggregatorService
    {
        private readonly IEnumerable<ICoverArtProvider> _providers;
        private readonly Logger _logger;

        public CoverArtAggregatorService(IEnumerable<ICoverArtProvider> providers, Logger logger)
        {
            _providers = providers;
            _logger = logger;
        }

        public List<CoverArtCandidate> GetCandidates(Album album)
        {
            var artist = album.ArtistMetadata?.Value?.Name ?? string.Empty;
            var albumTitle = album.Title ?? string.Empty;
            var foreignAlbumId = album.ForeignAlbumId ?? string.Empty;
            var foreignReleaseIds = album.AlbumReleases?.Value?
                .Select(r => r.ForeignReleaseId)
                .Where(id => id != null)
                .ToList() ?? new List<string>();

            var enabledProviders = _providers.Where(p => p.IsEnabled).ToList();

            if (!enabledProviders.Any())
            {
                return new List<CoverArtCandidate>();
            }

            // Fan out to all providers in parallel
            var tasks = enabledProviders.Select(provider => Task.Run(() =>
            {
                _logger.Debug("Fetching cover art candidates from {0}", provider.Name);
                return provider.GetCandidates(artist, albumTitle, foreignAlbumId, foreignReleaseIds);
            })).ToArray();

            Task.WaitAll(tasks);

            // Merge results from all providers
            var all = tasks.SelectMany(t => t.Result).ToList();

            // Deduplicate by exact ImageUrl
            var seen = new HashSet<string>();
            var deduplicated = all.Where(c => seen.Add(c.ImageUrl ?? string.Empty)).ToList();

            // Sort: tier 1 (iTunes, MusicBrainz) → tier 2 (Discogs, Spotify) → tier 3 (LastFm)
            return deduplicated
                .OrderBy(c => Tier(c.Source))
                .ThenByDescending(c => c.Width ?? 0)
                .Take(100)
                .ToList();
        }

        private static int Tier(string source) =>
            source switch
            {
                "iTunes" => 0,
                "MusicBrainz" => 0,
                "Discogs" => 1,
                "Spotify" => 1,
                "LastFm" => 2,
                _ => 1
            };
    }
}
