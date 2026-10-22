# Public Windows CI — Widgets, 2026-10-22

- Personal repository: https://github.com/saqibhussaen/widgets . Authenticated push used the selected personal account and repository-local noreply author identity; old local Git login was removed. Global Git configuration was preserved.
- CI run https://github.com/saqibhussaen/widgets/actions/runs/39834275760 passed all **43 tests**, license checks and self-contained x64 packaging for source commit c11c4bae4f8042d506d7d8599b99f476b8b9f34e.
- Published unsigned prerelease: https://github.com/saqibhussaen/widgets/releases/tag/preview-0.5.0-1 . Its ZIP and SHA-256 came directly from that CI artifact; verified hash a0d89ae7cf78396dc478bbf5883d71d8043d34e3e2f228f8e92acdad98f6110a.
- This resolves the missing full-suite result in an allowed CI environment. It does not change local Windows publisher verification or establish real player/shell behavior. Free SignPath application is being prepared; no certificate/approval is claimed.

# Revision 0.5 validation — 2026-10-22

- Build succeeds with zero warnings/errors, including GSMTC projections and original Core Audio interop; no new dependencies.
- Current review suite: 22 passed, 21 unable to execute because Windows Application Control blocks ClockWidget.dll (0x800711C7). The process reports those as failures; they are not passing tests. Core custom-text migration/default values/theme privacy checks passed. Media fake-service/native/WPF tests requiring the app assembly are blocked.
- Isolated lifecycle/media probes could not load the app assembly and exited before writing reports. No current live-media/session/audio/UI compatibility result is claimed, and no playback or mute commands were sent.
- The normal self-contained 0.5 package also exited before rendering its media preview; no screenshot was produced. User supplied Windows Security notification explicitly identifies an unverified publisher for ClockWidget.Core.dll. No security policy or trust changes were made.
- The known-working 0.4.1 installation remains running. Its Default theme was updated from current local appearance and verified to match; previous settings/themes and Previous Default were preserved. This local update is independent of the blocked new UI. No usable user-profile signing certificate is configured. See SIGNING.md.
- Existing 0.4/0.4.1 passing results below remain historical. Current preview requires permitted execution before stable release; security policy is unchanged. Local packaging explicitly skips the blocked standard runner.
- Manual follow-up: media access from self-contained/unpackaged WPF, supported players and source switching, command failure, system mute/device changes, multiple displays/DPI, fullscreen, Explorer restart and sleep/resume. Artwork panel, volume, fullscreen suppression and taskbar embedding are not implemented in this first increment.

# Revision 0.4.1 validation — 2026-10-22

- Build passed with zero warnings/errors. The isolated lifecycle probe passed 36 checks on two displays, including persistent Top center availability in all five tabs, preserving movement lock and saving display positions after its button is invoked.
- Inspected the rendered settings screenshot: Top center is pinned below the right-side preview, outside scrolling/tab content.
- Existing 0.4 unit results and standard-output Application Control limitation remain recorded below. This small UI change used focused lifecycle checks; no additional unit-suite pass is claimed. Packaging explicitly skips the blocked standard runner and verifies licenses/notices.
- Media controls are a documentation-only plan in MEDIA_CONTROLS.md. No media playback, mute, metadata or taskbar integration tests are claimed.

# Revision 0.4 validation — 2026-10-22

- Standard build/test output: build passed, but Application Control blocked ClockWidget.Tests.dll (0x800711C7) and the app executable in that output. No security settings changed. Local packaging explicitly uses -SkipTests; this is not an additional passing run.
- Release review build: zero warnings/errors; 36 executable tests passed, zero failed.
- Added checks for legacy hidden-row migration, independent compositions including all-off, AM/PM hiding/size limits, theme JSON round-trip/privacy/desktop preservation, and paused weather requests. Existing native variable-weight, weather failure, settings backup and layout checks remain passing.
- Isolated lifecycle probe: 29 checks passed on the two connected displays. Includes requested sidebar order, date-only immediate persistence, saved preset reload/application and actual Apply-theme button/page rebuild, display selection/mirrors, lock/UI/disk consistency and clocks still ticking after settings closes.
- Actual WPF/DirectWrite screenshots inspected: inline AM/PM beside time and reordered component sidebar. Deterministic illustrative preview uses no personal data/network.
- Hardware/shell/real-location scenarios listed below still require manual QA; no new claims about those events.

# Validation and release checks

Local verification date: 2026-10-22 (Asia/Karachi). Results below distinguish automated checks from interactive Windows behavior.

## Revision 0.3 — current results

- Build succeeds with zero warnings/errors. **32 automated tests passed**, including backup recovery, per-display/hidden-state persistence, and genuine intermediate weights for shaped date/city/weather text in both fonts.
- **24 isolated lifecycle checks passed on two connected displays**: header Show/Hide text and uniqueness, hiding/showing all clocks, immediate persistence, selecting either actual display, one clock on each display, lock UI/model/disk synchronization, locked mouse input not capturing/moving, independent text settings, backups, and visible/ticking clocks after settings closes and the five-second reveal has expired.
- Refreshed settings screenshot inspected: one header Hide clock button, no preview-side recovery actions, new date/weather controls, restored larger clock with seconds, and city line.
- The 0.2 Application Control blocker is historical; current tests ran successfully without changing security policy. An invalid native intermediate left by a failed link was corrected; native DLL publication now happens only after a successful staged link/import check.
- Remaining manual hardware/service checks: hot unplug/reconnect, mixed DPI while dragging, Explorer restart, sleep/resume, actual weather/location permission. Automated lock coverage uses the app's own routed mouse input and native lock hook; no third-party windows are manipulated.

## Revision 0.2 — historical results

- User reported invisible desktop clock and rejected the original settings UI. The previous parent-handle probe was insufficient and is **not** evidence of visible rendering.
- Replaced wallpaper child hosting with a top-level layered WPF window ordered above the desktop surface/below normal apps. Redesigned settings with original dark templates, live preview, color swatches and Show clock recovery.
- Current build succeeds with zero warnings/errors. Settings preview rendered and inspected; real settings panel inspected through Windows computer use.
- Native screen-position probe passed: desktop surface found, top-level HWND, window visible, always-on-top/click-through toggles, desktop order restored, click-through cleared, and a point inside a real clock glyph is uncovered after Show clock.
- User independently confirmed: pressing Show clock displays the clock outside the settings preview.
- **Current automated suite execution is blocked** by Windows Application Control (0x800711C7) loading ClockWidget.Tests.dll. Normal-context rebuild did not remove the restriction. No security settings were changed. Default build/CI still require test execution. Local packaging explicitly uses -SkipTests and retains this limitation.
- Remaining: Explorer restart, monitor/DPI changes, sleep/resume, actual location permission, and normal-app occlusion/desktop-switch cycles. Do not mark these passed based on the probe or preview.

## Historical 0.1 checks (not current-revision test results)

- Release build using .NET SDK 10.0.402, runtime 10.0.13, Windows SDK 10.0.26100.0, and MSVC x64.
- Historical suite: **28 passed, 0 failed**, with zero build warnings/errors before this revision.
- Console tests cover settings normalization/atomic persistence/corrupt recovery; 12/24-hour midnight/noon/seconds and clock changes; real DirectWrite intermediate outlines for both fonts; font endpoints/sizes/stacked fragments; repeated WPF appearance/layout updates; deterministic HTTP fixtures; units/conditions; consent; denied location/manual fallback; caching/offline/expiry/location mismatch; turning weather off; automatic-location refresh scheduling.
- HTTP fixtures are repository-authored representative responses matching the provider schema, not claims of recorded live measurements. Tests use fixed coordinates and no live location or weather calls.
- The app's offline preview renders its actual WPF/DirectWrite clock to docs/images/clock.png. No personal wallpaper or desktop content is captured.
- Historical desktop probe checked WorkerW parent handles/mode flags. The user's invisibility report disproved its adequacy as a rendering test. WorkerW parenting is removed in 0.2.
- ClockFont.dll import inspection passed: DWrite.dll is the only imported DLL; no separate C/C++ runtime is needed. The native build enforces this allowlist.
- License verification checks complete notice files, pinned font SHA-256 hashes, package allowlist, and the exact self-contained runtime version in release output.
- Complete portable ZIP extracted to a fresh folder: self-contained executable exited successfully after rendering an Inter/stacked preview, with DOTNET_ROOT deliberately pointing to a nonexistent runtime folder. Extracted font hashes/notices/runtime versions passed verification. This confirms the local package works independently of a separately configured .NET installation; clean-machine interactive QA remains below.

## Interactive Windows release QA (not yet marked passed)

Run on a normal interactive Windows 11 session; record build/OS version and results before promoting a draft release.

- [ ] Clock above wallpaper and behind icons/ordinary app windows as appropriate; Win+D twice; hide/show; always-on-top toggle.
- [ ] Restart Explorer and confirm widget survives or is recreated with position/settings intact; tray returns if Explorer recreates the notification area.
- [ ] Drag across displays at 100%, 150%, and 200% scale, including negative monitor coordinates. Disconnect saved monitor and verify recovery.
- [ ] Lock/click-through recovery through tray, right-click settings, presets, repeated settings changes, both layouts, seconds and 12/24-hour switching.
- [ ] Sleep/resume, manual clock/timezone change, midnight/date rollover and DST transition.
- [ ] First-run disclosure; Windows location allowed, denied and service unavailable; manual city search/selection; offline/stale/expired weather.
- [ ] Relaunch restores preferences and position; launch-at-sign-in toggle; second instance exits; clean exit removes tray icon.
- [ ] Extract the complete ZIP on a clean Windows 11 x64 machine without a separate .NET installation; verify native bridge, WinRT location bindings, notices and fonts.

Known boundary: desktop surface discovery/z-order still depends on Explorer layout, but the clock remains top-level and can always be explicitly revealed. Real location permission and hardware events cannot be established by offline unit tests.
