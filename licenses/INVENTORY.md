# Component and license inventory

Review date: 2026-10-22. Original code/artwork: Saqib Hussain, MIT. No copied app designs, screenshots, proprietary font files, or third-party icon sets are included. Retain the license files listed below in source and binary releases. Unknown dependencies are rejected by scripts/verify-licenses.ps1.

| Component | Version/source | License and distribution obligations | Modifications |
| --- | --- | --- | --- |
| Clock Widget and ClockFont | 0.3.0; this repository | MIT; retain LICENSE | Original project code |
| Nunito / Inter variable fonts | google/fonts commit `45e3d5269e4a17d823f6d84470bc084dc5ae46f6`; exact URLs and SHA-256 in fonts.json | OFL-1.1; retain each full OFL/copyright file, keep fonts under OFL, never sell fonts alone | None |
| .NET runtime | 10.0.13; https://github.com/dotnet/runtime/tree/v10.0.13 | MIT source plus component notices; retain runtime/LICENSE.TXT and runtime/THIRD-PARTY-NOTICES.TXT. Official Microsoft binary distribution terms also accompany the package in distribution/ | None |
| WPF / Windows Desktop runtime | 10.0.13; https://github.com/dotnet/wpf/tree/v10.0.13 | MIT source plus component notices; retain wpf/ notices and distribution/ notices. Do not relabel Microsoft's binary distribution terms as MIT | None |
| Windows SDK .NET reference/projection pack | Microsoft.Windows.SDK.NET.Ref 10.0.19041.57, official NuGet targeting pack; https://www.nuget.org/packages/Microsoft.Windows.SDK.NET.Ref/10.0.19041.57 | Windows SDK terms in windows-sdk/SDK-LICENSE.rtf; WinRT support/projection implementation source: https://github.com/microsoft/CsWinRT under MIT, preserved in windows-sdk/LICENSE.txt. Preserve both; runtime WinRT.Runtime.dll and Microsoft.Windows.SDK.NET.dll are projections, not proprietary project assets | None |
| SDK binary distribution notices | Official .NET SDK 10.0.402 archive, bundling runtime 10.0.13; https://dotnet.microsoft.com/download/dotnet/10.0 | distribution/LICENSE.txt and distribution/ThirdPartyNotices.txt are retained verbatim; they include Microsoft distribution terms and a superset of component notices. Build SDK itself is not shipped | None |
| Original clock/weather icons | native vector/drawing code in WidgetWindow.cs and scripts/create-icon.ps1 | MIT; covered by LICENSE | Original artwork |

## External services and platform prerequisites

- Weather: https://open-meteo.com/ . Data CC BY 4.0, https://creativecommons.org/licenses/by/4.0/ . Display attribution, license link in About/notices, and transformation information (temperature rounding/conversion and mapped weather codes). The free hosted API is noncommercial and rate limited; see https://open-meteo.com/en/terms . These service terms do not restrict the MIT grant for original application code. No AGPL server code is incorporated.
- Geocoding: https://open-meteo.com/en/docs/geocoding-api . Location data based on GeoNames, https://www.geonames.org/ . Credit both sources in the UI and notices. No flags from the API documentation are used.
- Windows 11 x64, DirectWrite, Windows location, and Segoe UI for ordinary system UI text are operating-system prerequisites. No Windows system font files or OS binaries are copied into project assets. These are not claimed as open-source components of the app.
- Visual Studio C++ compiler/Windows SDK are build-only prerequisites with Microsoft terms. The original ClockFont DLL has no CRT imports; `/NODEFAULTLIB /NOENTRY` prevents bundling a separate MSVC runtime. Windows SDK interfaces are invoked through operating-system DLLs.
- Build CI uses actions/checkout@v4, actions/setup-dotnet@v4, actions/upload-artifact@v4, and actions/download-artifact@v4 from their official MIT-licensed GitHub repositories. They execute in CI and are not bundled in the app. GitHub CLI is a runner tool, also not redistributed.

## Release review

1. Restore/build and inspect project.assets.json; the only allowed package dependency is the pinned Microsoft Windows SDK pack. Framework/runtime packs are separately pinned by release-runtime.json.
2. Verify font hashes and complete licenses with scripts/verify-licenses.ps1. Review any asset/dependency change before adjusting the allowlist.
3. Verify release runtime versions, copied notice tree and font files. Inspect ClockFont.dll imports when changing compiler/link flags.
4. Distribute the complete ZIP, including LICENSE, THIRD-PARTY-NOTICES.md, licenses/, and Assets/Fonts/*/OFL.txt. Original project MIT terms do not replace third-party terms.
5. Review docs/VALIDATION.md before publishing a release. Do not commit credentials, private settings/location, or build caches.

This inventory documents permissions and obligations; it is not a guarantee that no third party could assert a legal claim. Publishing source under MIT does not waive existing third-party copyrights or platform/service terms.

## Media increment 0.5

Media session projections use the existing pinned Windows SDK reference pack. Core Audio, User32 and Windows media APIs are operating-system prerequisites; original interop declarations and vector paths are MIT project code. No new package, font, bundled artwork or shell extension is introduced. Runtime track artwork is not bundled, saved or redistributed.
