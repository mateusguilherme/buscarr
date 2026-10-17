# buscarr

An artist search engine built on [MusicBrainz](https://musicbrainz.org). It aims to find the right artist where existing tools get it wrong, such as namesakes, and keeps the artists you want in a list. The name is a nod to the *arr apps (Sonarr, Radarr, Lidarr…).

The next step is managing downloads through [slskd](https://github.com/slskd/slskd), the Soulseek client.

## Features

- **Artist search:** lists every MusicBrainz artist whose name matches. Each one comes with the description MusicBrainz uses to tell namesakes apart and with its area of origin, so you pick the right artist instead of getting the first match.
- **My list:** a button next to each search result adds the artist to a personal list, choosing the kinds of releases to get, and shows whether the artist is already in it. The list shows each artist with their photo, description and area, and the same button edits or removes them.
- **Editions:** a global setting picks which edition of each album to get: the original, the most recent remaster or the deluxe, falling back to the original when an album has no such edition.
- **Quality:** a global setting picks the files to download: MP3 at 320 kbps or FLAC.
- **Languages:** the interface follows the browser's language, in Portuguese or English.
