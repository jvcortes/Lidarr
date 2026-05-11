using System.Collections.Generic;

namespace NzbDrone.Core.MediaCover
{
    /// <summary>
    /// Implemented by each cover art source (iTunes, MusicBrainz/CAA, Discogs, Spotify, Last.fm).
    /// Providers are discovered via DI and fanned out in parallel by CoverArtAggregatorService.
    /// </summary>
    public interface ICoverArtProvider
    {
        /// <summary>Display name used in the Source field of CoverArtCandidate.</summary>
        string Name { get; }

        /// <summary>
        /// False when required credentials are absent/empty.
        /// The aggregator skips disabled providers silently.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Fetch cover art candidates for the given album.
        /// Must never throw; return an empty list on any error.
        /// </summary>
        /// <param name="artist">Artist display name.</param>
        /// <param name="album">Album title.</param>
        /// <param name="foreignAlbumId">MusicBrainz Release Group ID (always present).</param>
        /// <param name="foreignReleaseIds">MusicBrainz Release IDs for each pressing (may be empty).</param>
        List<CoverArtCandidate> GetCandidates(
            string artist,
            string album,
            string foreignAlbumId,
            IEnumerable<string> foreignReleaseIds);
    }
}
