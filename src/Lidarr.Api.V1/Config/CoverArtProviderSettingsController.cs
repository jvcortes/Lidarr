using Lidarr.Http;
using Lidarr.Http.REST.Attributes;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;

namespace Lidarr.Api.V1.Config
{
    [V1ApiController("config/coverartproviders")]
    public class CoverArtProviderSettingsController : ConfigController<CoverArtProviderSettingsResource>
    {
        public CoverArtProviderSettingsController(IConfigService configService)
            : base(configService)
        {
        }

        protected override CoverArtProviderSettingsResource ToResource(IConfigService model)
        {
            return CoverArtProviderSettingsResourceMapper.ToResource(model);
        }

        [RestPutById]
        public override ActionResult<CoverArtProviderSettingsResource> SaveConfig(
            [FromBody] CoverArtProviderSettingsResource resource)
        {
            _configService.CoverArtDiscogsToken = resource.DiscogsToken ?? string.Empty;
            _configService.CoverArtSpotifyClientId = resource.SpotifyClientId ?? string.Empty;

            // Only update the secret if the user submitted a real value (not the redacted placeholder)
            if (resource.SpotifyClientSecret != "****")
            {
                _configService.CoverArtSpotifyClientSecret = resource.SpotifyClientSecret ?? string.Empty;
            }

            _configService.CoverArtLastFmApiKey = resource.LastFmApiKey ?? string.Empty;

            return Accepted(resource.Id);
        }
    }
}
