using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;

namespace NzbDrone.Core.MediaCover
{
    public interface IMapCoversToLocal
    {
        void ConvertToLocalUrls(int entityId, MediaCoverEntity coverEntity, ICollection<MediaCover> covers);
        string GetCoverPath(int entityId, MediaCoverEntity coverEntity, MediaCoverTypes coverType, string extension, int? height = null);
        bool EnsureAlbumCovers(Album album);
        void WriteCoverToAlbumFolders(Album album, IEnumerable<TrackFile> trackFiles);
    }

    public class MediaCoverService :
        IHandleAsync<ArtistRefreshCompleteEvent>,
        IHandleAsync<ArtistsDeletedEvent>,
        IHandleAsync<AlbumAddedEvent>,
        IHandleAsync<AlbumDeletedEvent>,
        IMapCoversToLocal
    {
        private readonly IMediaCoverProxy _mediaCoverProxy;
        private readonly IImageResizer _resizer;
        private readonly IAlbumService _albumService;
        private readonly IHttpClient _httpClient;
        private readonly IDiskProvider _diskProvider;
        private readonly ICoverExistsSpecification _coverExistsSpecification;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        private readonly string _coverRootFolder;

        // ImageSharp is slow on ARM (no hardware acceleration on mono yet)
        // So limit the number of concurrent resizing tasks
        private static SemaphoreSlim _semaphore = new SemaphoreSlim((int)Math.Ceiling(Environment.ProcessorCount / 2.0));

        public MediaCoverService(IMediaCoverProxy mediaCoverProxy,
                                 IImageResizer resizer,
                                 IAlbumService albumService,
                                 IHttpClient httpClient,
                                 IDiskProvider diskProvider,
                                 IAppFolderInfo appFolderInfo,
                                 ICoverExistsSpecification coverExistsSpecification,
                                 IConfigFileProvider configFileProvider,
                                 IEventAggregator eventAggregator,
                                 Logger logger)
        {
            _mediaCoverProxy = mediaCoverProxy;
            _resizer = resizer;
            _albumService = albumService;
            _httpClient = httpClient;
            _diskProvider = diskProvider;
            _coverExistsSpecification = coverExistsSpecification;
            _configFileProvider = configFileProvider;
            _eventAggregator = eventAggregator;
            _logger = logger;

            _coverRootFolder = appFolderInfo.GetMediaCoverPath();
        }

        public string GetCoverPath(int entityId, MediaCoverEntity coverEntity, MediaCoverTypes coverType, string extension, int? height = null)
        {
            var heightSuffix = height.HasValue ? "-" + height.ToString() : "";

            if (coverEntity == MediaCoverEntity.Album)
            {
                return Path.Combine(GetAlbumCoverPath(entityId), coverType.ToString().ToLower() + heightSuffix + GetExtension(coverType, extension));
            }

            return Path.Combine(GetArtistCoverPath(entityId), coverType.ToString().ToLower() + heightSuffix + GetExtension(coverType, extension));
        }

        public void ConvertToLocalUrls(int entityId, MediaCoverEntity coverEntity, ICollection<MediaCover> covers)
        {
            if (entityId == 0)
            {
                // Artist isn't in Lidarr yet, map via a proxy to circument referrer issues
                foreach (var mediaCover in covers)
                {
                    mediaCover.RemoteUrl = mediaCover.Url;
                    mediaCover.Url = _mediaCoverProxy.RegisterUrl(mediaCover.RemoteUrl);
                }

                return;
            }

            if (!covers.Any())
            {
                PopulateCoverWithCache(entityId, coverEntity, covers);
            }

            foreach (var mediaCover in covers)
            {
                if (mediaCover.CoverType == MediaCoverTypes.Unknown)
                {
                    continue;
                }

                var filePath = GetCoverPath(entityId, coverEntity, mediaCover.CoverType, mediaCover.Extension, null);

                mediaCover.RemoteUrl = mediaCover.Url;

                if (coverEntity == MediaCoverEntity.Album)
                {
                    mediaCover.Url = _configFileProvider.UrlBase + @"/MediaCover/Albums/" + entityId + "/" + mediaCover.CoverType.ToString().ToLower() + GetExtension(mediaCover.CoverType, mediaCover.Extension);
                }
                else
                {
                    mediaCover.Url = _configFileProvider.UrlBase + @"/MediaCover/" + entityId + "/" + mediaCover.CoverType.ToString().ToLower() + GetExtension(mediaCover.CoverType, mediaCover.Extension);
                }

                if (_diskProvider.FileExists(filePath))
                {
                    var lastWrite = _diskProvider.FileGetLastWrite(filePath);
                    mediaCover.Url += "?lastWrite=" + lastWrite.Ticks;
                }
                else if (mediaCover.CoverType == MediaCoverTypes.Cover)
                {
                    // The expected extension (from SkyHook metadata) doesn't match the file on
                    // disk — e.g. a user-selected cover was saved with a different extension.
                    // Scan the cache directory for any cover.* file and redirect the URL to it.
                    var cacheDir = coverEntity == MediaCoverEntity.Album
                        ? GetAlbumCoverPath(entityId)
                        : GetArtistCoverPath(entityId);

                    if (_diskProvider.FolderExists(cacheDir))
                    {
                        var actual = _diskProvider.GetFileInfos(cacheDir)
                            .FirstOrDefault(fi => Path.GetFileNameWithoutExtension(fi.Name)
                                .Equals("cover", StringComparison.OrdinalIgnoreCase));

                        if (actual != null)
                        {
                            var actualExt = actual.Extension; // e.g. ".jpeg"
                            if (coverEntity == MediaCoverEntity.Album)
                            {
                                mediaCover.Url = _configFileProvider.UrlBase + "/MediaCover/Albums/" + entityId + "/cover" + actualExt;
                            }
                            else
                            {
                                mediaCover.Url = _configFileProvider.UrlBase + "/MediaCover/" + entityId + "/cover" + actualExt;
                            }

                            mediaCover.Url += "?lastWrite=" + _diskProvider.FileGetLastWrite(actual.FullName).Ticks;
                        }
                    }
                }
            }
        }

        private string GetArtistCoverPath(int artistId)
        {
            return Path.Combine(_coverRootFolder, artistId.ToString());
        }

        private string GetAlbumCoverPath(int albumId)
        {
            return Path.Combine(_coverRootFolder, "Albums", albumId.ToString());
        }

        private bool EnsureArtistCovers(Artist artist)
        {
            var updated = false;
            var toResize = new List<Tuple<MediaCover, bool>>();

            foreach (var cover in artist.Metadata.Value.Images)
            {
                if (cover.CoverType == MediaCoverTypes.Unknown)
                {
                    continue;
                }

                var fileName = GetCoverPath(artist.Id, MediaCoverEntity.Artist, cover.CoverType, cover.Extension);
                DateTime? lastModified = null;
                var alreadyExists = false;

                try
                {
                    var serverFileHeaders = _httpClient.Head(new HttpRequest(cover.Url) { AllowAutoRedirect = true }).Headers;
                    lastModified = serverFileHeaders.LastModified;

                    alreadyExists = _coverExistsSpecification.AlreadyExists(lastModified, serverFileHeaders.ContentLength, fileName);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "HEAD request failed for {0}, will attempt download regardless", cover.Url);
                    alreadyExists = false;
                }

                if (!alreadyExists)
                {
                    try
                    {
                        DownloadCover(artist, cover, lastModified ?? DateTime.Now);
                        updated = true;
                    }
                    catch (HttpException e)
                    {
                        _logger.Warn("Couldn't download media cover for {0}. {1}", artist, e.Message);
                    }
                    catch (WebException e)
                    {
                        _logger.Warn("Couldn't download media cover for {0}. {1}", artist, e.Message);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Couldn't download media cover for {0}", artist);
                    }
                }

                toResize.Add(Tuple.Create(cover, alreadyExists));
            }

            try
            {
                _semaphore.Wait();

                foreach (var tuple in toResize)
                {
                    EnsureResizedCovers(artist, tuple.Item1, !tuple.Item2);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return updated;
        }

        private void PopulateCoverWithCache(int entityId, MediaCoverEntity coverEntity, ICollection<MediaCover> covers)
        {
            var folderPath = coverEntity == MediaCoverEntity.Album ? GetAlbumCoverPath(entityId) : GetArtistCoverPath(entityId);

            if (_diskProvider.FolderExists(folderPath))
            {
                foreach (var fileInfo in _diskProvider.GetFileInfos(folderPath))
                {
                    var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    var extension = Path.GetExtension(fileInfo.Name);
                    if (fileName.Contains('-'))
                    {
                        continue;
                    }

                    if (Enum.TryParse(fileName, true, out MediaCoverTypes coverType) && !covers.Any(c => c.CoverType == coverType))
                    {
                        var filePath = fileInfo.FullName;
                        var diskCover = new MediaCover(coverType, filePath)
                        {
                            RemoteUrl = filePath
                        };

                        covers.Add(diskCover);
                    }
                }
            }
        }

        public bool EnsureAlbumCovers(Album album)
        {
            var updated = false;

            var coverImages = album.Images
                .Where(e => e.CoverType == MediaCoverTypes.Cover)
                .AsEnumerable();

            if (album.UserSelectedCoverUrl.IsNotNullOrWhiteSpace())
            {
                coverImages = new[]
                {
                    new MediaCover(MediaCoverTypes.Cover, album.UserSelectedCoverUrl)
                };

                // Remove any existing cover files with a different extension so that
                // ConvertToLocalUrls doesn't serve a stale file from a previous source.
                var albumCacheDir = GetAlbumCoverPath(album.Id);
                var newExt = GetExtension(MediaCoverTypes.Cover, Path.GetExtension(album.UserSelectedCoverUrl));
                if (_diskProvider.FolderExists(albumCacheDir))
                {
                    foreach (var fi in _diskProvider.GetFileInfos(albumCacheDir))
                    {
                        var baseName = Path.GetFileNameWithoutExtension(fi.Name);
                        if (baseName.Equals("cover", StringComparison.OrdinalIgnoreCase) &&
                            !fi.Extension.Equals(newExt, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.Debug("Removing stale cover file {0}", fi.FullName);
                            _diskProvider.DeleteFile(fi.FullName);
                        }
                    }
                }
            }

            foreach (var cover in coverImages)
            {
                if (cover.CoverType == MediaCoverTypes.Unknown)
                {
                    continue;
                }

                var fileName = GetCoverPath(album.Id, MediaCoverEntity.Album, cover.CoverType, cover.Extension, null);
                DateTime? lastModified = null;
                var alreadyExists = false;

                try
                {
                    var serverFileHeaders = _httpClient.Head(new HttpRequest(cover.Url) { AllowAutoRedirect = true }).Headers;
                    lastModified = serverFileHeaders.LastModified;

                    alreadyExists = _coverExistsSpecification.AlreadyExists(lastModified, serverFileHeaders.ContentLength, fileName);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "HEAD request failed for {0}, will attempt download regardless", cover.Url);
                    alreadyExists = false;
                }

                if (!alreadyExists)
                {
                    try
                    {
                        DownloadAlbumCover(album, cover, lastModified ?? DateTime.Now);
                        updated = true;
                    }
                    catch (HttpException e)
                    {
                        _logger.Warn("Couldn't download media cover for {0}. {1}", album, e.Message);
                    }
                    catch (WebException e)
                    {
                        _logger.Warn("Couldn't download media cover for {0}. {1}", album, e.Message);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Couldn't download media cover for {0}", album);
                    }
                }
            }

            return updated;
        }

        private void DownloadCover(Artist artist, MediaCover cover, DateTime lastModified)
        {
            var fileName = GetCoverPath(artist.Id, MediaCoverEntity.Artist, cover.CoverType, cover.Extension);

            _logger.Info("Downloading {0} for {1} {2}", cover.CoverType, artist, cover.Url);
            _httpClient.DownloadFile(cover.Url, fileName);

            try
            {
                _diskProvider.FileSetLastWriteTime(fileName, lastModified);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to set modified date for {0} image for artist {1}", cover.CoverType, artist);
            }
        }

        private void DownloadAlbumCover(Album album, MediaCover cover, DateTime lastModified)
        {
            var fileName = GetCoverPath(album.Id, MediaCoverEntity.Album, cover.CoverType, cover.Extension, null);

            _logger.Info("Downloading {0} for {1} {2}", cover.CoverType, album, cover.Url);
            _httpClient.DownloadFile(cover.Url, fileName);

            try
            {
                _diskProvider.FileSetLastWriteTime(fileName, lastModified);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to set modified date for {0} image for album {1}", cover.CoverType, album);
            }
        }

        private void EnsureResizedCovers(Artist artist, MediaCover cover, bool forceResize, Album album = null)
        {
            var heights = GetDefaultHeights(cover.CoverType);

            foreach (var height in heights)
            {
                var mainFileName = GetCoverPath(artist.Id, MediaCoverEntity.Artist, cover.CoverType, cover.Extension);
                var resizeFileName = GetCoverPath(artist.Id, MediaCoverEntity.Artist, cover.CoverType, cover.Extension, height);

                if (forceResize || !_diskProvider.FileExists(resizeFileName) || _diskProvider.GetFileSize(resizeFileName) == 0)
                {
                    _logger.Debug("Resizing {0}-{1} for {2}", cover.CoverType, height, artist);

                    try
                    {
                        _resizer.Resize(mainFileName, resizeFileName, height);
                    }
                    catch
                    {
                        _logger.Debug("Couldn't resize media cover {0}-{1} for artist {2}, using full size image instead.", cover.CoverType, height, artist);
                    }
                }
            }
        }

        private int[] GetDefaultHeights(MediaCoverTypes coverType)
        {
            switch (coverType)
            {
                default:
                    return Array.Empty<int>();

                case MediaCoverTypes.Poster:
                case MediaCoverTypes.Disc:
                case MediaCoverTypes.Cover:
                case MediaCoverTypes.Logo:
                case MediaCoverTypes.Headshot:
                    return new[] { 500, 250 };

                case MediaCoverTypes.Banner:
                    return new[] { 70, 35 };

                case MediaCoverTypes.Fanart:
                case MediaCoverTypes.Screenshot:
                    return new[] { 360, 180 };
            }
        }

        private static string GetExtension(MediaCoverTypes coverType, string defaultExtension)
        {
            return coverType switch
            {
                MediaCoverTypes.Clearlogo => ".png",
                _ => defaultExtension.IsNotNullOrWhiteSpace()
                    ? defaultExtension
                    : ".jpg"
            };
        }

        public void WriteCoverToAlbumFolders(Album album, IEnumerable<TrackFile> trackFiles)
        {
            // Determine the extension used when the cover was cached
            var url = album.UserSelectedCoverUrl;
            if (url.IsNullOrWhiteSpace())
            {
                var img = album.Images.FirstOrDefault(x => x.CoverType == MediaCoverTypes.Cover);
                if (img == null)
                {
                    _logger.Debug("No cover image found for {0}, skipping folder copy", album);
                    return;
                }

                url = img.Url;
            }

            var ext = Path.GetExtension(url);
            if (ext.IsNullOrWhiteSpace())
            {
                ext = ".jpg";
            }

            var coverCachePath = GetCoverPath(album.Id, MediaCoverEntity.Album, MediaCoverTypes.Cover, ext, null);
            if (!_diskProvider.FileExists(coverCachePath))
            {
                _logger.Debug("Cached cover not found at {0}, skipping folder copy", coverCachePath);
                return;
            }

            var albumFolders = trackFiles
                .Select(f => Path.GetDirectoryName(f.Path))
                .Distinct()
                .Where(d => d.IsNotNullOrWhiteSpace() && _diskProvider.FolderExists(d))
                .ToList();

            if (!albumFolders.Any())
            {
                _logger.Debug("No album folders found for {0}, skipping folder copy", album);
                return;
            }

            foreach (var folder in albumFolders)
            {
                var destPath = Path.Combine(folder, "cover" + ext);

                // Remove any stale cover files with a different extension so the folder
                // never ends up with both cover.jpg and cover.jpeg at the same time.
                try
                {
                    foreach (var fi in _diskProvider.GetFileInfos(folder))
                    {
                        if (Path.GetFileNameWithoutExtension(fi.Name).Equals("cover", StringComparison.OrdinalIgnoreCase) &&
                            !fi.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.Debug("Removing stale cover file from album folder: {0}", fi.FullName);
                            _diskProvider.DeleteFile(fi.FullName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to clean up stale cover files in {0}", folder);
                }

                _logger.Info("Writing cover to album folder: {0}", destPath);
                try
                {
                    _diskProvider.CopyFile(coverCachePath, destPath, overwrite: true);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to write cover to {0}", destPath);
                }
            }
        }

        public void HandleAsync(ArtistRefreshCompleteEvent message)
        {
            var updated = EnsureArtistCovers(message.Artist);

            var albums = _albumService.GetAlbumsByArtist(message.Artist.Id);
            foreach (var album in albums)
            {
                updated |= EnsureAlbumCovers(album);
            }

            _eventAggregator.PublishEvent(new MediaCoversUpdatedEvent(message.Artist, updated));
        }

        public void HandleAsync(ArtistsDeletedEvent message)
        {
            foreach (var artist in message.Artists)
            {
                var path = GetArtistCoverPath(artist.Id);
                if (_diskProvider.FolderExists(path))
                {
                    _diskProvider.DeleteFolder(path, true);
                }
            }
        }

        public void HandleAsync(AlbumAddedEvent message)
        {
            if (message.DoRefresh)
            {
                var updated = EnsureAlbumCovers(message.Album);

                _eventAggregator.PublishEvent(new MediaCoversUpdatedEvent(message.Album, updated));
            }
        }

        public void HandleAsync(AlbumDeletedEvent message)
        {
            var path = GetAlbumCoverPath(message.Album.Id);
            if (_diskProvider.FolderExists(path))
            {
                _diskProvider.DeleteFolder(path, true);
            }
        }
    }
}
