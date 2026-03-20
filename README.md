# UploadSite

`UploadSite` is a custom ASP.NET Core app for staging and reviewing music uploads before they land in a Jellyfin library.

## Current scope

- Cookie-based login with admin and user roles
- SQLite-backed users and import history
- Drag-and-drop single-track upload flow for `mp3`, `m4a`, and `flac`
- Tag validation for required metadata and embedded cover art
- Admin review queue with metadata editing
- Import into `Artist/Year - Album/01 - Title.ext`
- `cover.jpg` extraction from embedded art

## Environment variables

- `UPLOADSITE_ADMIN_USERNAME`
- `UPLOADSITE_ADMIN_PASSWORD`
- `UPLOADSITE_DB_PATH`
- `UPLOADSITE_KEYS_PATH`
- `UPLOADSITE_STAGING_ROOT`
- `UPLOADSITE_LIBRARY_ROOT`

## Local run

```bash
dotnet run --project UploadSite.Web
```

The first launch creates the SQLite database and a seed admin user.

## TrueNAS SCALE notes

- Mount one persistent app-data path to `/data`
- Mount your Jellyfin music dataset to `/music`
- Put the app behind a reverse proxy and forward `X-Forwarded-For` and `X-Forwarded-Proto`
- Change `UPLOADSITE_ADMIN_PASSWORD` before the first production launch
- See the fuller deployment guide in `docs/truenas-scale.md`
- A compose-style template for custom app deployment is in `docs/truenas-compose.yaml`

## What comes next

- Inline metadata editing in the browser before final save
- Stronger duplicate detection across the existing library
- Cover preview and tag editing for failed uploads
- Optional integration with external metadata lookup providers
