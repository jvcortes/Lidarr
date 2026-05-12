using Lidarr.Http.REST;
using NzbDrone.Core.Configuration;

namespace Lidarr.Api.V1.Config
{
    public class CoverArtProviderSettingsResource : RestResource
    {
        public string DiscogsToken { get; set; }
        public string SpotifyClientId { get; set; }

        /// <summary>
        /// Write-only on GET responses: redacted to "****" when set,
        /// empty string when unset.
        /// </summary>
        public string SpotifyClientSecret { get; set; }
        public string LastFmApiKey { get; set; }

        public bool EnableMusicBrainz { get; set; }
        public bool EnableItunes { get; set; }
        public bool EnableDiscogs { get; set; }
        public bool EnableSpotify { get; set; }
        public bool EnableLastFm { get; set; }
    }

    public static class CoverArtProviderSettingsResourceMapper
    {
        public static CoverArtProviderSettingsResource ToResource(IConfigService model)
        {
            return new CoverArtProviderSettingsResource
            {
                DiscogsToken = model.CoverArtDiscogsToken,
                SpotifyClientId = model.CoverArtSpotifyClientId,

                // Redact secret on read — never expose it back to the browser
                SpotifyClientSecret = string.IsNullOrWhiteSpace(model.CoverArtSpotifyClientSecret)
                    ? string.Empty
                    : "****",
                LastFmApiKey = model.CoverArtLastFmApiKey,
                EnableMusicBrainz = model.EnableCoverArtMusicBrainz,
                EnableItunes = model.EnableCoverArtItunes,
                EnableDiscogs = model.EnableCoverArtDiscogs,
                EnableSpotify = model.EnableCoverArtSpotify,
                EnableLastFm = model.EnableCoverArtLastFm
            };
        }
    }
}
