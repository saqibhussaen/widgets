# Third-party notices

Original application code, documentation, and artwork: Copyright (c) 2026 Saqib Hussain, MIT. See LICENSE.

## Bundled fonts

- Nunito: Copyright 2014 The Nunito Project Authors, SIL Open Font License 1.1. Full notice: Assets/Fonts/nunito/OFL.txt (src/ClockWidget/Assets/Fonts/nunito/OFL.txt in the source repository).
- Inter: Copyright 2020 The Inter Project Authors, SIL Open Font License 1.1. Full notice: Assets/Fonts/inter/OFL.txt (src/ClockWidget/Assets/Fonts/inter/OFL.txt in the source repository).

The original, unmodified font binaries remain OFL-licensed. They are not covered by the application's MIT grant. Exact source commits and SHA-256 hashes are in licenses/fonts.json. Do not sell the font files by themselves. Retain each complete font license when redistributing.

## .NET runtime and WPF

The self-contained distribution includes .NET 10.0.13 and Windows Desktop runtime components. The .NET runtime and WPF projects use MIT, with additional notices for incorporated third-party components. Retain the full upstream license/notice files in licenses/runtime/ and licenses/wpf/, plus the distribution notices in licenses/distribution/. Their individual licenses continue to apply. See licenses/INVENTORY.md.

No external application NuGet libraries are used. The Windows SDK .NET reference package supplies official WinRT projections; SDK distribution terms and the MIT license for WinRT support source are preserved in licenses/windows-sdk/. Windows, DirectWrite, and the Windows location service are operating-system prerequisites, not redistributed by this project. MSVC is a build tool. The original native bridge has no C/C++ runtime dependency; it links only to the Windows DirectWrite API. The project does not claim operating-system or toolchain components are open source.

## Weather service and data

Weather data by Open-Meteo: https://open-meteo.com/ . Data license: Creative Commons Attribution 4.0 International, https://creativecommons.org/licenses/by/4.0/ . Data is presented as rounded temperature and mapped condition text/icons; Fahrenheit is calculated from Celsius when selected. No endorsement is implied.

Free hosted API access is restricted to noncommercial use and subject to limits: https://open-meteo.com/en/terms . This is separate from the MIT application license. Commercial distributions must arrange appropriate provider access. No Open-Meteo server source is incorporated into this application. City search is served by Open-Meteo's geocoding API (GeoNames data); see https://open-meteo.com/en/docs/geocoding-api .

Original weather icons and the clock app icon are project-authored vector artwork under MIT. No wallpaper or third-party screenshots are bundled.
