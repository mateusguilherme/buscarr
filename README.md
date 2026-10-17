# buscarr

An artist search engine built on [MusicBrainz](https://musicbrainz.org). It aims to find the right artist where existing tools get it wrong, such as namesakes, and then lists that artist's original releases. The name is a nod to the *arr apps (Sonarr, Radarr, Lidarr…).

The next step is managing downloads through [slskd](https://github.com/slskd/slskd), the Soulseek client.

## Features

- **Artist search:** lists every MusicBrainz artist whose name matches. Each one comes with the description MusicBrainz uses to tell namesakes apart and with its area of origin, so you pick the right artist instead of getting the first match.
- **Releases:** official releases only, by type (albums, EPs, singles or all). Each album appears once, as its first edition with the full date, and live releases get a section of their own. Compilations, remixes, demos and soundtracks are left out.
- **Editions:** a setting picks which edition of each album to get: the original, the most recent remaster or the deluxe, falling back to the original when an album has no such edition. Each album shows the chosen edition's track count, counting CD and digital editions only.
- **Languages:** the interface follows the browser's language, in Portuguese or English.
