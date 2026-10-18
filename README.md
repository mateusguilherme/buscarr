# buscarr

An artist search engine built on [MusicBrainz](https://musicbrainz.org). The name is a nod to the *arr apps (Sonarr, Radarr, Lidarr…).

New releases by the artists in the list are downloaded through [slskd](https://github.com/slskd/slskd), the Soulseek client, and imported into the music library.

## Features

- **Artist search:** lists every MusicBrainz artist whose name matches. Each one comes with the description MusicBrainz uses to tell namesakes apart and with its area of origin, so you pick the right artist instead of getting the first match.
- **My list:** a button next to each search result adds the artist to a personal list, choosing the kinds of releases to get, and shows whether the artist is already in it. The list shows each artist with their photo, description and area, and the same button edits or removes them.
- **Editions:** a global setting picks which edition of each album to get: the original, the most recent remaster or the deluxe, falling back to the original when an album has no such edition.
- **Quality:** a global setting picks the files to download: MP3 at 320 kbps or FLAC. When MP3 is chosen and only lossless files show up, they are downloaded and transcoded as a fallback.
- **Downloads:** the releases of every listed artist are fetched from MusicBrainz — on start, every 12 hours and whenever the list changes — skipping albums the music library already has. Each missing album is searched on Soulseek and downloaded, retrying on a growing schedule when it fails or nothing is found.
- **Verification:** before import, every album is checked against MusicBrainz: the track count, each track's duration, the expected format and bitrate, and a spectral analysis that catches files faked from lossy sources. Rejected folders are quarantined along with the reason.
- **Import:** verified albums are tagged with MusicBrainz metadata and cover art, then filed in the library by artist and album.
- **Downloads page:** every request with its state — the wait for the next attempt, the place in the queue, download progress, the failure reason — updated live, with a cancel button.
- **Server settings:** the admin sets slskd, the folders, the music server and the fanart.tv key on the settings page, and tests the connections from there. Changes apply without a restart.
- **Languages:** the interface follows the browser's language, in Portuguese or English.

## Running it

The first account created is the admin, and only the admin sees the server part of the settings page: the slskd address and API key, the downloads, library and rejected folders, the OpenSubsonic server and the fanart.tv key. Saved values apply at once; folders are checked for existence and write access when saved, and API keys and passwords are never sent back to the browser.

On first start, those settings are seeded from `appsettings.json` or environment variables (`Section__Key`): `Slskd__Url`/`Slskd__ApiKey` for the Soulseek daemon, `Slskd__DownloadsPath` + `Library__Path` to verify and import what it downloads, `Library__RejectedPath`, `Subsonic__Url`/`__User`/`__Password` to skip albums an OpenSubsonic library already has, and `FanartTv__ApiKey` for artist photos. After that the settings page owns them, and changing the variables has no effect. All of them are optional: with none set, buscarr is just the search and the list.

`GET /health` reports the workers, the database and slskd for any HTTP monitor. See [docs/deploy.md](docs/deploy.md) for a Docker image and compose example.
