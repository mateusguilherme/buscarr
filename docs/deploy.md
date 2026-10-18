# Deploying buscarr

How to self-host buscarr with Docker, next to an [slskd](https://github.com/slskd/slskd) instance and an OpenSubsonic server (Navidrome, for example).

## Image

Build from the repository root:

```sh
docker build -t buscarr .
```

The image carries ffmpeg (used by verification and transcode) and listens on port 3000 (`ASPNETCORE_URLS=http://0.0.0.0:3000`).

## Compose

```yaml
services:
  buscarr:
    image: buscarr
    container_name: buscarr
    user: "1000:1000"
    ports:
      - "127.0.0.1:3000:3000"
    volumes:
      - ./buscarr-data:/data
      - /path/to/music:/music
      - /path/to/slskd/downloads/complete:/downloads/complete
    environment:
      ConnectionStrings__Buscarr: "Data Source=/data/buscarr.db"
      Slskd__Url: "http://slskd:5030"
      Slskd__ApiKey: "…"
      Slskd__DownloadsPath: "/downloads/complete"
      Library__Path: "/music"
      Subsonic__Url: "http://navidrome:4533"
      Subsonic__User: "…"
      Subsonic__Password: "…"
      FanartTv__ApiKey: "…"
      Spotify__ClientId: "…"
      Spotify__ClientSecret: "…"
    restart: unless-stopped
```

The variables under `environment` (other than the connection string) only seed the server settings on the first start. After that they live in the database and are changed on the settings page by the admin, the first account created. The page checks each folder when saving, so a wrong path is caught there rather than at the first import.

Notes:

- **`user`** must be able to write to the library, to slskd's `complete/` folder and to the `/data` volume (the SQLite database lives there, and the startup migration writes to it).
- **Paths are what buscarr sees.** `Slskd__DownloadsPath` is the `complete/` folder as mounted *in this container*; it does not have to match slskd's internal path, because buscarr finds each album's folder by its file names.
- **`Library__RejectedPath`** defaults to a `rejected/` folder next to the downloads; folders that fail verification are moved there with a `.reason.txt` explaining why, never deleted.
- **Optional pieces degrade gracefully.** Without `Library__Path` + `Slskd__DownloadsPath`, buscarr only downloads into `complete/` (state "Downloaded") and never touches the library. Without `Subsonic__*`, it does not skip albums the library already has. Without `Spotify__*` (a client ID and secret from [developer.spotify.com](https://developer.spotify.com/dashboard)), covers missing from both the Cover Art Archive and Deezer stay blank.
- Every external call has a timeout (HTTP clients and the ffmpeg processes), so a stuck dependency cannot hang a worker forever; no external watchdog is needed.

## Soulseek account

slskd logs into Soulseek, not buscarr — and **one Soulseek account holds one session**. If another client already uses an account, give the slskd that buscarr uses an account of its own (Soulseek creates it on first login with any free name), or the two sessions will keep kicking each other out. slskd itself runs at most 2 concurrent searches; buscarr's queue is paced for that.

## Monitoring

- **`GET /health`** needs no login and suits any HTTP monitor (Uptime Kuma, for example). It returns 200 when healthy and 503 when the database is unreachable or a worker stopped beating (search worker: 10 min; generator: 13 h, one beat per 12 h cycle). The JSON body carries the reasons, plus slskd's connection state — slskd being down does *not* degrade `/health`, so watch slskd separately if you want an alert for it.

## Before trusting rejections

The spectral check that catches fake quality (a 128 kbps rip re-encoded as 320 or re-packed as FLAC) ships with conservative thresholds: a cutoff below 19 kHz is always suspect, 19–20 kHz only with a steep rolloff. Verify against your own known-good files before trusting `fake-audio` rejections on a large backlog. Rejected folders sit in `rejected/` with their reasons, so a wrong rejection is recoverable.
