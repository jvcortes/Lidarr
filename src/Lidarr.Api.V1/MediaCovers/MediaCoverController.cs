using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Lidarr.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;

namespace Lidarr.Api.V1.MediaCovers
{
    [V1ApiController]
    public class MediaCoverController : Controller
    {
        private static readonly Regex RegexResizedImage = new Regex(@"-\d+(?=\.(jpg|png|gif)$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly IContentTypeProvider _mimeTypeProvider;

        private static readonly HashSet<string> AllowedProxyHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "is1-ssl.mzstatic.com",          // iTunes / Apple Music
            "coverartarchive.org",           // MusicBrainz / CAA
            "ia800504.us.archive.org",       // Internet Archive (CAA image storage)
            "ia800505.us.archive.org",
            "ia800506.us.archive.org",
            "i.discogs.com",                 // Discogs
            "i.scdn.co",                     // Spotify
            "lastfm.freetls.fastly.net",     // Last.fm
        };

        private readonly IHttpClient _httpClient;

        public MediaCoverController(IAppFolderInfo appFolderInfo, IDiskProvider diskProvider, IHttpClient httpClient)
        {
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _httpClient = httpClient;
            _mimeTypeProvider = new FileExtensionContentTypeProvider();
        }

        [HttpGet(@"artist/{artistId:int}/{filename:regex((.+)\.(jpg|png|gif))}")]
        public IActionResult GetArtistMediaCover(int artistId, string filename)
        {
            var filePath = Path.Combine(_appFolderInfo.GetAppDataPath(), "MediaCover", artistId.ToString(), filename);

            if (!_diskProvider.FileExists(filePath) || _diskProvider.GetFileSize(filePath) == 0)
            {
                // Return the full sized image if someone requests a non-existing resized one.
                // TODO: This code can be removed later once everyone had the update for a while.
                var basefilePath = RegexResizedImage.Replace(filePath, "");
                if (basefilePath == filePath || !_diskProvider.FileExists(basefilePath))
                {
                    return NotFound();
                }

                filePath = basefilePath;
            }

            return PhysicalFile(filePath, GetContentType(filePath));
        }

        [HttpGet(@"album/{albumId:int}/{filename:regex((.+)\.(jpg|png|gif))}")]
        public IActionResult GetAlbumMediaCover(int albumId, string filename)
        {
            var filePath = Path.Combine(_appFolderInfo.GetAppDataPath(), "MediaCover", "Albums", albumId.ToString(), filename);

            if (!_diskProvider.FileExists(filePath) || _diskProvider.GetFileSize(filePath) == 0)
            {
                // Return the full sized image if someone requests a non-existing resized one.
                // TODO: This code can be removed later once everyone had the update for a while.
                var basefilePath = RegexResizedImage.Replace(filePath, "");
                if (basefilePath == filePath || !_diskProvider.FileExists(basefilePath))
                {
                    return NotFound();
                }

                filePath = basefilePath;
            }

            return PhysicalFile(filePath, GetContentType(filePath));
        }

        private string GetContentType(string filePath)
        {
            if (!_mimeTypeProvider.TryGetContentType(filePath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return contentType;
        }

        /// <summary>
        /// Proxies an external cover art thumbnail URL through Lidarr so the browser
        /// can load it without CORS issues during the selection screen.
        /// Only URLs whose host appears in AllowedProxyHosts are accepted.
        /// </summary>
        [HttpGet("proxy")]
        public IActionResult ProxyCoverArt([FromQuery] string url)
        {
            if (url.IsNullOrWhiteSpace())
            {
                return BadRequest("url parameter is required.");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                (parsed.Scheme != "https" && parsed.Scheme != "http"))
            {
                return BadRequest("Invalid URL.");
            }

            if (!AllowedProxyHosts.Contains(parsed.Host))
            {
                return BadRequest($"Host '{parsed.Host}' is not in the cover art proxy allowlist.");
            }

            try
            {
                var request = new HttpRequest(url);
                request.AllowAutoRedirect = true;
                request.SuppressHttpError = true;

                var response = _httpClient.Execute(request);

                if (response.HasHttpError)
                {
                    return StatusCode((int)response.StatusCode);
                }

                var contentType = response.Headers.ContentType.IsNotNullOrWhiteSpace()
                    ? response.Headers.ContentType
                    : "image/jpeg";

                return File(response.ResponseData, contentType);
            }
            catch (Exception)
            {
                return StatusCode(502);
            }
        }
    }
}
