using System.Collections.Generic;

namespace NzbDrone.Core.MediaCover
{
    /// <summary>
    /// A single cover art image candidate returned by a provider.
    /// ImageUrl and ThumbnailUrl are remote URLs from the provider's CDN.
    /// The API layer rewrites ThumbnailUrl to the local proxy endpoint before
    /// sending the response to the browser.
    /// </summary>
    public class CoverArtCandidate
    {
        /// <summary>Provider name: "iTunes", "MusicBrainz", "Discogs", "Spotify", "LastFm"</summary>
        public string Source { get; set; }

        /// <summary>Full-resolution remote image URL.</summary>
        public string ImageUrl { get; set; }

        /// <summary>Small/thumbnail remote image URL (≤ 500 px). Used in the selection UI.</summary>
        public string ThumbnailUrl { get; set; }

        /// <summary>Image width in pixels, if known.</summary>
        public int? Width { get; set; }

        /// <summary>Image height in pixels, if known.</summary>
        public int? Height { get; set; }

        /// <summary>Image type tags from the provider, e.g. ["Front"], ["Back"], ["Medium"].</summary>
        public List<string> Types { get; set; }

        /// <summary>Human-readable release title from the provider (e.g. "OK Computer OKNOTOK").</summary>
        public string ReleaseTitle { get; set; }

        /// <summary>
        /// Deep-link URL back to this album on the provider's site.
        /// Required for Spotify attribution; shown as a link in the UI.
        /// </summary>
        public string ReleaseUrl { get; set; }
    }
}
