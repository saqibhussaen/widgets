# Decisions

Maintain this log with every architectural, product, privacy, dependency, and licensing change. Record date, context, alternatives, choice, reasoning, consequences, and status. Keep superseded entries and link their replacements. Dates use Asia/Karachi.

## D001 — Original minimalist design
- Date: 2026-10-22. Status: accepted.
- Context: a readable, aesthetic desktop clock with adjustable typography.
- Alternatives: opaque card; decorated dashboard; transparent typography.
- Choice: transparent date → time → weather, white text and optional soft shadow. Nunito 112 DIP, weight 550; Inter alternative. Horizontal and stacked layouts.
- Reasoning: emphasizes the time and supports user customization without visual clutter.
- Consequences: original icons and branding only; no copied product graphics or references. Text color/shadow are user-controlled for wallpaper contrast.

## D002 — Native Windows stack
- Date: 2026-10-22. Status: accepted.
- Context: Windows 11 x64 is the first supported platform.
- Alternatives: browser wrapper; WinUI; WPF.
- Choice: .NET 10 LTS, WPF, a small original C++ DirectWrite bridge, and Windows location APIs.
- Reasoning: mature transparent windows/settings and direct access to Windows desktop features, with no third-party application NuGet packages.
- Consequences: requires .NET 10 SDK, Visual Studio C++ build tools, and Windows SDK to build. Release is a self-contained portable ZIP. Windows/SDK/compiler remain platform/toolchain prerequisites, not open-source project assets.
- Native release choice: fixed-size buffers and `/NODEFAULTLIB /NOENTRY` remove the bridge's C/C++ runtime dependency. Only the operating-system DirectWrite API is imported.

## D003 — Actual continuous font weights
- Date: 2026-10-22. Status: accepted.
- Context: selecting nearby intermediate weights must change the real font outlines.
- Alternatives: static font faces; simulated bold; DirectWrite variable axes.
- Choice: native/ClockFont.cpp creates an IDWriteFontFace5 with the wght axis and returns glyph outlines through a callback. ClockGlyph.cs builds cached WPF geometry.
- Reasoning: true variable-font interpolation and a small auditable native boundary.
- Consequences: bundle unmodified Nunito/Inter variable fonts; maintain their OFL notices. Stable digit advances prevent positional jitter. Cache the current text/font/weight/size geometry, not an unbounded history. Automated tests compare outlines at weights 550, 551, and 600.

## D004 — Desktop hosting isolated from presentation (superseded by D011)
- Date: 2026-10-22. Status: superseded by D011 after user-reported invisibility.
- Context: stay above wallpaper and behind normal applications, including Show Desktop.
- Alternatives: ordinary bottommost window; always-on-top only; Explorer WorkerW host.
- Choice: isolate Explorer WorkerW attachment in DesktopHost.cs; recover periodically and after display changes. Always-on-top is an explicit option.
- Reasoning: matches the intended desktop placement while containing undocumented shell assumptions.
- Consequences: new Explorer builds may require fixes. Report failed attachment in settings; do not claim verified compatibility with every Windows build. Never hide or mutate Explorer-owned windows. Interactive Show Desktop/Explorer restart and mixed-DPI checks remain release QA requirements.
- Local prototype result: desktop attach → always-on-top/click-through → desktop restore/clear click-through passed. Set WS_CHILD/remove WS_POPUP for desktop mode, reversing the style when detaching. Initialize the HWND again at Loaded to cover WPF event ordering. Restricted execution is expected to deny cross-process parenting.

## D005 — Weather consent and privacy
- Date: 2026-10-22. Status: accepted.
- Context: automatic weather without accounts, personal keys, or telemetry.
- Alternatives: silent IP geolocation; manual city only; Windows location with consent and manual fallback.
- Choice: weather off until an explicit automatic/manual choice after disclosure. Automatic requests Windows location, rounds coordinates to two decimals, reacquires hourly/startup/resume. Refresh weather every 30 minutes.
- Reasoning: honors the requested automatic option without silently transmitting location.
- Consequences: provider receives IP/coordinates; city search sends query text. Local settings/cache contain the selected city/rounded coordinates, never history. Turning off clears selected location and weather cache. Permission failures explain manual city fallback.
- Permission requests are limited to the explicit automatic-weather button in the foreground settings window. Startup/hourly/resume refreshes reuse the existing OS grant; denied access offers city fallback instead of prompting from the background.

## D006 — Replaceable weather provider and bounded cache
- Date: 2026-10-22. Status: accepted.
- Context: weather outages should not break the clock.
- Alternatives: shared backend/key; per-user commercial key; direct Open-Meteo.
- Choice: IWeatherProvider with OpenMeteoProvider; inject provider and location service into WeatherController. Cache latest reading, mark stale at one hour, hide at 24 hours, never show another location's cache.
- Reasoning: key-free noncommercial use, no operated backend, and deterministic offline tests.
- Consequences: free hosted service is noncommercial, subject to limits. Keep service terms separate from MIT; commercial forks need appropriate service access. Weather data attribution and transformation notes are required. No provider server source is incorporated.

## D007 — License and asset provenance
- Date: 2026-10-22. Status: accepted; review required for each release.
- Context: publishing on GitHub with documented reuse permissions and minimal licensing burden.
- Alternatives: GPLv3; Apache-2.0; MIT.
- Choice: MIT for original application code/docs/artwork, copyright Saqib Hussain. Bundled third-party source/assets require explicit compatible open-source licenses; preserve component-specific terms and notices.
- Reasoning: simple permissive reuse. Dependencies/fonts are not relicensed MIT merely because the app is MIT.
- Consequences: licenses/fonts.json pins font commits/hashes. licenses/INVENTORY.md records tools/runtime/reference packages and obligations. Preserve upstream runtime/distribution notices. Verify all assets/notices before release. Copyright-free is not the goal; licensed reuse is. No guarantee against all possible legal claims.

## D008 — Local storage and recovery
- Date: 2026-10-22. Status: accepted.
- Context: portable executable with per-user preferences and safe recovery.
- Alternatives: repository-relative settings; registry settings; local JSON.
- Choice: versioned JSON under %LocalAppData%/ClockWidget, atomic temp-file replacement, normalization and corrupt-file defaults. One process per login session; opt-in startup entry under HKCU.
- Reasoning: no administrator privileges; repository stays free of personal data.
- Consequences: never commit runtime settings/cache. Disk failures remain visible through tray status; app can continue without saving. Move portable executable only after disabling launch at sign-in or update the startup entry afterward.

## D009 — Git identity deferred
- Date: 2026-10-22. Status: pending user details; implementation is allowed locally.
- Context: initial repository has no commits or remote; author and connected GitHub profile report a work email. Local push credentials could not be inspected.
- Alternatives: keep work identity; change global configuration; configure only this repository.
- Choice: do not commit, configure a remote, or publish until the user supplies personal username/Gmail. Then set repository-local author identity, verify GitHub email and push authentication.
- Reasoning: avoids accidental work attribution and changes to unrelated repositories.
- Consequences: leave global settings untouched. Never record credentials or personal location in documentation. No push is part of this implementation.

## D010 — Release and verification boundaries
- Date: 2026-10-22. Status: accepted.
- Context: source and distributable should be reviewable and reproducible.
- Alternatives: installer/store distribution; unsigned portable release.
- Choice: x64 portable ZIP, pinned runtime, font integrity/license checks, Windows build/test CI and tag-based release workflow. No installer/update service in v1.
- Reasoning: smallest deployment surface and straightforward GitHub distribution.
- Consequences: unsigned binaries may receive Windows reputation prompts. Build tooling is not shipped. No external test framework is needed: the console test runner returns nonzero on failure. Manual checks and observed results are tracked in docs/VALIDATION.md; never mark unrun checks passed.

## D011 — Visible top-level desktop window replaces wallpaper parenting
- Date: 2026-10-22. Status: accepted; supersedes D004.
- Context: the user could open settings but could not see the clock. The previous structural probe tested parent handles, not actual desktop visibility.
- Alternatives: continue patching WorkerW child hosting; force always-on-top; preserve a normal layered top-level WPF window and order it above the desktop surface.
- Choice: never call SetParent or spawn a wallpaper WorkerW. Keep the WPF HWND top-level, locate the visible icon desktop surface, and maintain our window immediately above it/below normal apps. Refresh z-order every 500 ms without activating the clock. Explorer owns no parent relationship to destroy.
- Reasoning: avoids transparent WPF child/compositor failures and placing the clock below wallpaper. Explicit always-on-top remains optional.
- Consequences: add Show clock to settings/tray, briefly revealing for five seconds. A second app launch signals the existing instance to reveal/open settings. Recovery must not unhide a deliberately hidden widget. Probe now checks top-level/visible state and an uncovered point inside a real glyph after reveal; user independently confirmed the clock is visible. Full hardware/shell-event QA remains distinct.

## D012 — Original dark settings and live preview
- Date: 2026-10-22. Status: accepted.
- Context: user rejected the old standard Windows form appearance.
- Alternatives: reuse default theme; add an external UI framework; create original WPF templates.
- Choice: original dark palette, lavender accent, sidebar navigation, rounded segmented options/buttons, custom sliders/toggles, color swatches, persistent live preview and Show clock recovery actions. All existing clock/weather capabilities remain available.
- Reasoning: cohesive appearance with immediate visual feedback and no added dependency/license obligations.
- Consequences: SettingsTheme.cs contains original MIT templates; SettingsWindow renders a VisualBrush of the actual clock. Custom templates retain native WPF control types/keyboard behavior. Test/preview sessions can use an isolated --session-dir. No stock wallpaper or outside artwork is included.

## D013 — Report blocked test execution honestly
- Date: 2026-10-22. Status: current environment limitation.
- Context: Windows Application Control blocks the rebuilt ClockWidget.Tests.dll with error 0x800711C7; rebuilding outside the sandbox did not resolve it. The app/preview execute successfully.
- Choice: do not change security settings or relabel old results as current. Local packaging may explicitly use -SkipTests, with an emitted warning and validation record. Default build/CI still require a passing suite.
- Consequences: the revision is validated by successful build, rendered UI, native visibility probe and user confirmation; current automated suite execution remains blocked pending a permitted CI/test environment.

## D014 — Preserve the chosen clock and save every change locally
- Date: 2026-10-22. Status: accepted; updates D001/D008.
- Context: user chose larger Nunito numerals, seconds, and top-center placement in the earlier test profile; a replacement instance used a different profile. User wants automatic persistence, not a Save button or account requirement.
- Choice: restore the chosen appearance locally; default new installs to Nunito size 163/weight 864 with seconds. Keep personal locations/coordinates/display positions out of source/assets. Normal launches use one %LocalAppData%/ClockWidget profile.
- Reasoning: the requested setup was recoverable, so login/cloud storage adds no needed capability.
- Consequences: settings schema 2 adds typography/display/visibility fields while reading legacy settings. Every change writes flushed JSON to a temporary file, preserves the last valid primary as settings.json.bak, and atomically replaces the primary. Loading falls back to the backup if primary data is damaged. Close/exit/session end also flush; the footer reports actual failures. No Save button.

## D015 — Independent date/weather typography and a visible city line
- Date: 2026-10-22. Status: accepted.
- Context: user wants independently resizable date/weather text and the same weight slider; weather city was not displayed.
- Choice: add DateSize/DateWeight and WeatherSize/WeatherWeight. Shape date, city, and conditions with DirectWrite text layout, real wght axes, and the bundled font collection. Keep tabular-digit rendering for time.
- Reasoning: preserves genuine intermediate weights and proper shaping rather than simulated bold or unshaped character-by-character text.
- Consequences: city appears above temperature, scales/weights with weather, and is editable in Weather. Manual city choice fills it automatically. Automatic location uses a user label or honest "Current location" fallback; no new reverse-geocoding service or undisclosed coordinate sharing is introduced.

## D016 — Explicit displays, reliable lock, one Show/Hide control
- Date: 2026-10-22. Status: accepted.
- Context: user requests second-display selection, a working lock, a single top Show/Hide button, and clocks that survive closing settings.
- Choice: select any connected display or show one clock per display; remember positions per device. One header button toggles shared persisted visibility and changes its label. Remove duplicate preview-side actions and persistent clicked-focus button styling.
- Reasoning: separate visibility state from brief reveal z-order; synchronize switches with the actual model to avoid misleading lock state.
- Consequences: Program manages a main clock plus optional mirrors; DisplayService resolves missing monitors safely. Lock cancels pending capture, blocks every drag stage and native system Move commands. Settings remains independent of clock lifetime; only tray Exit shuts down. Isolated lifecycle checks cover both actual displays, model/UI/disk lock consistency, visibility toggles, and ticking clocks after settings closes.

## D017 — Verify the expanded native renderer before publishing
- Date: 2026-10-22. Status: accepted.
- Context: a failed sandbox link left an invalid intermediate native DLL, which incremental build could mistakenly reuse.
- Choice: link into a staged DLL, verify its imports, then publish ClockFont.dll only after success.
- Consequences: native imports remain limited to Windows DirectWrite. The current 32-test suite passed in the normal review output, including shaped date/city/weather weight interpolation and backup recovery; no security policy was changed. The previous blocked 0.2 test result remains historical.

## D018 — Independent components and inline period
- Date: 2026-10-22. Status: accepted; updates D012/D015.
- Context: user wants time, date, weather and city independently optional, organized by component.
- Alternatives: keep a combined Appearance page; reuse the global visibility flag; add separate component flags.
- Choice: sidebar order Clock, Date, Weather, Settings, About. Schema 3 adds ShowTime/ShowDate/ShowWeather/ShowCity plus ShowPeriod and PeriodSize. Existing global ClockVisible still controls the complete widget. Legacy hidden row positions migrate to switches while retaining useful Above/Below placement.
- Reasoning: supports date-only and weather-only compositions without losing desktop controls. DirectWrite-shaped AM/PM sits beside the time, uses its font/weight, and has independent size/visibility.
- Consequences: hiding weather pauses requests and retains configuration; explicit clear-location still revokes consent and deletes cache. Empty compositions stay recoverable through settings/tray. City search remains explicitly initiated in Weather.

## D019 — Local appearance themes with a preserved Default
- Date: 2026-10-22. Status: accepted.
- Context: user wants the current style selectable as Default, with custom presets and room for later themes.
- Alternatives: hard-code only a factory style; store whole private settings as a theme; explicitly allowlist appearance fields.
- Choice: ThemePreset.Capture/Apply explicitly copy only appearance fields. First launch captures the current appearance as Default in themes.json; later launches retain it. Named custom presets persist immediately with JSON backup recovery. Matching custom names update a preset; Default remains protected.
- Reasoning: preserves the chosen existing style without embedding private preferences into shared source or allowing a theme to grant location access.
- Consequences: no city values, coordinates, consent, startup or display settings enter captured presets. Appearance switches do not bypass permission checks. A preset save action is distinct from settings, which continue to autosave. No new dependencies or license obligations.

### D017 validation update for 0.4
The review output passed all 36 tests. The standard output runner was subsequently blocked again by Application Control (0x800711C7), as was its app executable. D013 remains applicable to that output. Local packaging uses the documented explicit -SkipTests flag; no policy changes or extra passing test run are claimed.

## D020 — Keep Top center available throughout settings
- Date: 2026-10-22. Status: accepted.
- Context: user wants a side-panel realignment action available from every section.
- Alternatives: repeat the action in each tab; keep a single shared control outside tab content.
- Choice: pin a Top center button below the persistent right-side preview, outside its scrolling content. Reuse Program.ResetPosition and each widget's existing target display.
- Reasoning: one consistent action stays accessible across Clock, Date, Weather, Settings and About, even in a shorter settings window.
- Consequences: all active display widgets realign and autosave through the existing positioning path; movement lock remains unchanged. No new dependency or data storage.

## D021 — Plan media controls as an independent optional component
- Date: 2026-10-22. Status: planned; implementation deferred until after feasibility spikes.
- Context: user supplied taskbar-strip and expanded now-playing references and asked for a plan within this app.
- Alternatives: another paid app; copy existing product code/assets; original optional media UI using Windows APIs.
- Choice: original compact previous/play-pause/next/system-mute strip and expanded panel. Investigate GSMTC plus Core Audio using current SDK projections. Keep independent enablement and preserve the clock lifecycle, privacy and MIT licensing rules. Details: MEDIA_CONTROLS.md.
- Reasoning: media and clock can share settings/tray infrastructure without sharing visibility or forcing new accounts/services.
- Consequences: exact in-taskbar placement remains an explicit prototype gate; documented appbar placement is separate from embedding. Do not promise or silently substitute it. Any shell-host dependency needs version/license/compatibility review. No media commands or monitoring are implemented or executed for this plan.

## D022 — Promote the chosen appearance and decouple custom text
- Date: 2026-10-22. Status: implemented.
- Context: user wants their current preferences to become Default and the city-label switch to become Custom Text.
- Alternatives: rename the city setting in place; keep forecast identity separate from an editable label.
- Choice: fresh typography defaults match the read appearance (Nunito 163/864, date/weather 29/745). Schema 4 migrates the legacy visible city label into local CustomText, preserving ShowCity as a migration field. The new text and switch do not alter forecast city/coordinates. Presets exclude message content. First upgrade refreshes Default from current appearance and retains Previous Default; a Themes action can refresh it again.
- Reasoning: preserves current personalization while preventing an arbitrary text edit from changing weather data or leaking personal text into shared presets.
- Consequences: no location/text values added to source. Every text/switch edit autosaves; toggling text leaves content intact. DefaultProfileVersion marks a successful promotion so startup does not replace Default repeatedly.

## D023 — Begin media as an opt-in toolbar preview
- Date: 2026-10-22. Status: implemented preview; updates D021, full compatibility pending.
- Context: user explicitly asks to start media implementation.
- Alternatives: block all media work pending shell embedding; implement an isolated functional toolbar using OS APIs first.
- Choice: GSMTC session discovery/events/commands; Core Audio endpoint mute; original WPF strip above taskbar; separate Media enable/display settings. No additional packages, no Explorer hooks and no installed shell changes. Exact taskbar embedding is still a prototype gate, not claimed implemented.
- Reasoning: validates playback/audio boundaries independently of taskbar modification while preserving clock behavior.
- Consequences: commands serialize; async work has cancellation and stale-session guards; disabling unsubscribes and releases resources. Audio state/device is polled once per second in this first increment. Expanded panel, volume, fullscreen suppression and final hosting remain deferred. Current OS Application Control blocks the new review assembly; no live media-control or full-suite passing result is claimed.

## D024 — Keep the accepted build while resolving publisher trust
- Date: 2026-10-22. Status: active blocker for new UI/media validation.
- Context: user supplied a Windows Security notification for unverified ClockWidget.Core.dll. Review app loads fail with 0x800711C7; the normal self-contained preview also cannot start.
- Alternatives: overwrite the working install with a blocked build; retain it and complete the settings-only update while preparing proper publisher signing.
- Choice: retain/reopen 0.4.1; back up local settings/themes, capture current appearance into its Default and retain Previous Default. Keep 0.5 source/package as an unverified preview. Document trusted signing requirements in docs/SIGNING.md. No security/trust policy changes or certificate purchases.
- Reasoning: a successful compile cannot establish runnable UI/media compatibility or publisher trust. Current-user certificate inspection found no usable code-signing certificate.
- Consequences: source Custom Text/media are implemented but cannot be claimed live or fully tested here. Personal publisher identity/signing access is required for the signed release route. The earlier D022 implementation status refers to source and the completed Default update, not installation of the blocked new UI.

## D025 — Personal Git ownership and free signing application
- Date: 2026-10-22. Status: accepted; supersedes deferred personal identity decisions.
- Context: user explicitly selected saqibhussaen, requested removal of the other login, and chose the free signing option first.
- Alternatives: retain the employer connection; buy a certificate immediately; use the chosen personal account and apply for SignPath Foundation review.
- Choice: repository-local Saqib Hussain identity with verified personal GitHub noreply address; remove the old local credential, authenticate and verify saqibhussaen. Preserve global Git settings. Prepare public source/CI/release evidence and signing/privacy policy for the free program; contact email stays private.
- Reasoning: separates personal ownership from employer access and avoids certificate costs before free eligibility review.
- Consequences: SignPath approval is not guaranteed, especially for a new project. Its required conduct/privacy acceptance and maintainer MFA must be completed appropriately before signing access. Shared product/version metadata covers original EXE/DLL targets, including the native font bridge. No signature is fabricated and no Windows security policy is changed.

## D026 — Name the product Widgets
- Date: 2026-10-22. Status: accepted; user explicitly replaced the working Clock Widget name.
- Context: the app now includes multiple desktop components and media controls; user asked for Widgets rather than Clock Widget.
- Choice: product/UI name Widgets, personal repository saqibhussaen/widgets, managed app output Widgets.exe/Widgets.dll and portable Widgets-win-x64.zip. Signing application and product/version metadata follow that name.
- Reasoning: describes the broader utility while keeping individual clock/weather/media sections clear.
- Consequences: preserve internal ClockWidget namespaces, font bridge/core filenames, existing profile directory, launch-at-sign-in registry key and single-instance identity for compatibility. Current installed legacy version stays working pending a trusted new release. No settings are moved or discarded.

## D027 — Describe Widgets as an evolving app
- Date: 2026-10-22. Status: accepted.
- Context: user wants the description to cover future widgets and small customization improvements, rather than imply the app stops at clock/weather/media.
- Choice: describe current features and state that additional small widgets, customization options and improvements are planned for future updates. Apply consistently to README, GitHub project description and signing application.
- Reasoning: keeps the product broad enough to grow without representing roadmap items as shipped capabilities.
- Consequences: no release schedule or unimplemented feature is promised. The current preview and its limitations remain explicit.

## D028 — Submit the free signing application with explicit consent
- Date: 2026-10-22. Status: submitted; provider review pending.
- Context: user selected the free route, provided private contact details for SignPath and explicitly approved the required conduct/privacy boxes and submission. User completed the human-verification challenge.
- Choice: submit Widgets with the personal repository, actual unsigned CI-built prerelease, successful 43-test Windows run, truthful new-project reputation statement and future-widgets description. Leave optional marketing off.
- Reasoning: provides verifiable source/build/release evidence without claiming established adoption or an existing signing partnership.
- Consequences: website displayed Form submitted and Thank you, we'll be in touch soon. No certificate or signing approval has been received; current unsigned app restrictions remain. Keep application contact details and screenshot evidence outside public source. Maintainer MFA and manual release approval remain requirements before signing access/use.

### Signing metadata follow-up
Inspection of the actual CI artifact showed that .NET appended the Git commit to managed ProductVersion while the native DLL used 0.5.0. Directory.Build.props now disables that informational-version suffix for subsequent builds, so all original signing targets can enforce the same product version. CI/release provenance retains the source commit separately. The initial unsigned preview is preserved as built; it is not a signed artifact.
