# Plan: Return Non-Proxied Thumbnail URLs for Cover Art Candidates

## Problem

The `GET /api/v1/album/{id}/coverartcandidates` endpoint currently rewrites every `thumbnailUrl` to a local proxy endpoint (`/api/v1/MediaCover/proxy?url=...`). The frontend then appends `&apikey=${window.Lidarr.apiKey}` to load those thumbnails. This means:

1. Lidarr is proxying every candidate image through the backend, adding unnecessary CPU/memory/bandwidth overhead.
2. The thumbnails are tied to the Lidarr API key, even though the underlying images are hosted on public CDNs (Apple Music, CAA, Discogs, Spotify, Last.fm).
3. The `[AllowAnonymous]` attribute on the proxy endpoint exists specifically to avoid auth for images, which is a smell—images should not need to hit the app server at all.

## Goal

Return the original CDN thumbnail URLs directly from each provider. Let the browser fetch them straight from the source, removing the proxy and the API-key dependency for thumbnails.

## Files to Change

### Backend

1. **`src/Lidarr.Api.V1/Albums/AlbumController.cs`**
   - In `GetCoverArtCandidates`, change `ThumbnailUrl = BuildProxyUrl(c.ThumbnailUrl)` to `ThumbnailUrl = c.ThumbnailUrl`.
   - Remove the private `BuildProxyUrl(string)` method entirely (dead code).

2. **`src/Lidarr.Api.V1/Albums/CoverArtCandidateResource.cs`**
   - Update the XML doc on the `ThumbnailUrl` property: remove "proxied local URL" language and state it is the direct remote thumbnail URL from the provider.

3. **`src/NzbDrone.Core/MediaCover/CoverArtCandidate.cs`**
   - Update XML docs to clarify that `ThumbnailUrl` is a remote CDN URL and is returned directly to the UI.

4. **`src/Lidarr.Api.V1/MediaCovers/MediaCoverController.cs`**
   - Remove the `[AllowAnonymous] [HttpGet("proxy")] ProxyCoverArt` action.
   - Remove the `AllowedProxyHosts` HashSet.
   - Remove the `IHttpClient` field injection if it is no longer used elsewhere in the controller.

### Frontend

5. **`frontend/src/Album/CoverArt/SelectCoverArtModalContent.js`**
   - Change the `<img>` `src` from:
     ```jsx
     src={`${window.Lidarr.urlBase}${candidate.thumbnailUrl}&apikey=${window.Lidarr.apiKey}`}
     ```
     to:
     ```jsx
     src={candidate.thumbnailUrl}
     ```
   - All provider thumbnail URLs are already absolute HTTPS strings, so no `urlBase` prefix is required.

## Rationale

- **CORS is not an issue for `<img>` tags.** Cross-Origin Resource Sharing restrictions prevent JavaScript from reading pixel data (e.g., via `<canvas>`), but they do *not* stop the browser from rendering an image. Since the cover-art selection modal only *displays* thumbnails, direct URLs work without proxying.
- **Provider URLs are public.** Every provider (iTunes/Apple Music, MusicBrainz/CAA, Discogs, Spotify, Last.fm) returns standard HTTPS CDN URLs that are intended for hotlinking.
- **Reduced server load.** Removing the proxy eliminates outbound HTTP requests, response buffering, and streaming from the Lidarr host for every thumbnail.
- **Simpler code.** Fewer endpoints, no allowlist maintenance, no query-string API-key wrangling on the frontend.

## Verification Steps

1. Open an album → **Select Cover Art**.
2. Open browser DevTools → **Network** tab.
3. Confirm thumbnail requests go directly to provider CDNs (e.g., `is1-ssl.mzstatic.com`, `coverartarchive.org`, `i.discogs.com`, `i.scdn.co`, `lastfm.freetls.fastly.net`) instead of `/api/v1/MediaCover/proxy`.
4. Confirm no `apikey` query parameter is present on image requests.
5. Confirm the grid renders correctly and dimensions/resolutions are still shown.
6. Select a new cover and confirm the full-resolution `imageUrl` is still sent correctly via `PUT /api/v1/album/{id}/coverart`.

## Backward Compatibility

- The proxy endpoint (`/api/v1/MediaCover/proxy`) is only referenced internally by the cover-art candidate flow. No other API consumer or frontend component calls it.
- Removing it is safe. If a third-party client relies on the old proxied `thumbnailUrl`, it will now receive a direct HTTPS URL, which is actually *more* useful (no dependence on the Lidarr instance being online).
