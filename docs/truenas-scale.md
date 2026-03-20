# TrueNAS SCALE deployment notes

Use the app as a custom containerized service behind your reverse proxy.

## Storage layout

- Mount a persistent app-data dataset to `/data`
- Mount the Jellyfin music dataset to `/music`
- Keep `/music` writable for this app, because final imports and `cover.jpg` are written there

## Required environment variables

- `ASPNETCORE_ENVIRONMENT=Production`
- `UPLOADSITE_ADMIN_USERNAME=admin`
- `UPLOADSITE_ADMIN_PASSWORD=<strong-random-password>`
- `UPLOADSITE_DB_PATH=/data/uploadsite.db`
- `UPLOADSITE_STAGING_ROOT=/data/staging`
- `UPLOADSITE_LIBRARY_ROOT=/music`

## Ports

- Container port: `8080`
- Expose it only to your reverse proxy if possible

## Reverse proxy

Forward these headers:

- `X-Forwarded-For`
- `X-Forwarded-Proto`

The app already trusts forwarded headers in production startup.

## First launch

1. Start the app with an admin username and password in env vars.
2. Sign in with that seeded admin account.
3. Create day-to-day upload users from the admin page.
4. Replace the seed password in your deployment config if you rotate it later.
