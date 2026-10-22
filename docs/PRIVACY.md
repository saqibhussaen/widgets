# Privacy policy

Updated: 2026-10-22. Applies to Widgets maintained by Saqib Hussain.

Widgets has no app accounts, advertising, analytics, telemetry, automatic
updates or project-operated backend. Settings, custom messages, display positions,
named themes and the latest weather cache are saved in the Windows user's local
application-data folder. Backups may contain previous values. None belongs in Git
or a public bug report. Uninstall instructions are in the README; deleting that
data folder after exiting removes its local settings and caches.

## Optional weather and city search

Weather is opt-in. Automatic weather requests Windows location access and rounds
coordinates to two decimal places before sending them to Open-Meteo. The provider
also receives the requester's IP address. A manual city search sends the typed
query to Open-Meteo's geocoding service; selecting a city saves its name and rounded
coordinates locally. Custom Text is independent of the forecast city and is not
sent to the weather provider.

While enabled with consent, weather refreshes approximately every 30 minutes;
automatic location is refreshed at startup/resume and hourly. Hiding weather
pauses requests; the separate clear-location action revokes app consent and removes
the saved location and weather cache/backups. Windows controls its own system
location permissions. Weather access follows [Open-Meteo's terms/privacy](https://open-meteo.com/en/terms)
and its geocoding data is based on [GeoNames](https://www.geonames.org/).

## Optional media controls

The 0.5 media implementation is an opt-in preview. When enabled, it reads media
sessions provided by Windows, including player identity, title, artist and playback
capabilities. Those values remain in memory; this app does not save listening
history, record audio or upload track details. Playback and system output mute
commands are sent only when the user presses the corresponding controls. Disabling
media stops its subscriptions and audio-state polling. Windows and the media
players retain their own privacy policies.

## Source hosting and signing

Visiting GitHub, submitting an issue, or applying for a signing account uses those
services and their respective privacy policies. CI/signing should receive only
repository source and build artifacts. Maintainer application contact details,
authentication tokens, private keys and end-user settings are not public project
data. Code-signing approval is pending and does not imply collection of user data.

Report privacy issues through the maintainer's GitHub contact facilities. Avoid
posting personal settings, coordinates or track metadata publicly.
