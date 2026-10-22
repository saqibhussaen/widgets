# Media controls feature plan

Date: 2026-10-22. Status: first implementation in 0.5.0; taskbar embedding and expanded panel remain planned.

## Implemented first increment (0.5.0)

- Optional Media section with enable switch and display choice; independent toolbar just above the taskbar, with previous/play-pause/next, system mute, title tooltip, session picker and settings shortcut.
- Original WindowsMediaSessionService, AudioEndpointService, MediaController and MediaStripWindow implementations use the existing SDK and operating-system APIs. No package or shell extension was added.
- Media is disabled by default and excluded from theme enablement. Disable closes its window, stops audio polling and unsubscribes sessions. The clock stays independent.
- Media metadata is event-driven; this first increment reads default output/mute state once per second while enabled so external mute/device changes are reflected. Device notifications, artwork panel, volume slider, fullscreen suppression and exact taskbar integration remain follow-up work. The preview is topmost and may overlap fullscreen content; keep it disabled during presentations if needed.
- Build passes. Current Windows Application Control blocks the review app assembly (0x800711C7), including fake-media tests and live probes; live compatibility and UI appearance are not yet verified for this increment. No playback or mute commands were sent during testing.

The remaining sections describe the intended feature and acceptance gates; they do not claim deferred controls are implemented.

## Intended experience

Add an optional media component to the same Windows app, free and open source. The user's two screenshots establish the desired behavior: an always-visible compact strip in the taskbar area, with playback buttons and a truncated title, opening a larger now-playing panel. Create original layout details, vector icons and styling; the screenshots are references, not redistributable project assets.

- Compact strip: Previous, Play/Pause, Next, a dedicated Mute/Unmute button, and optional title. Do not require hovering over an app icon to reach these controls.
- Expanded panel on click: artwork when supplied by the player, title, artist/source, available sessions, playback buttons and volume slider. Click outside or Escape dismisses it. It opens only on an explicit action.
- Shuffle, repeat and seeking are a later increment, available only when the selected player supports them.
- Media can be used by itself or with any clock/date/weather combination. Hiding the clock must not inadvertently hide media controls. Closing settings keeps both running; tray Exit closes both.
- Disabled initially. No premium tier, accounts, telemetry, advertisements or additional media subscriptions imposed by this app.

## Placement: prototype before promising taskbar embedding

**Target:** a strip within the taskbar like the reference. Its feasibility remains open. The existing desktop clock host must not be reused for media: media controls need to remain reachable with ordinary windows open.

Microsoft documents thumbnail toolbars within taskbar previews; these alone do not satisfy the always-visible requirement. It also documents appbars: separate edge-docked windows that reserve desktop space and cooperate with the taskbar. An appbar on the taskbar's edge sits inward from it, rather than inside its icon strip. See [Taskbar Extensions](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar-extensions) and [Application Desktop Toolbars](https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars).

Prototype two hosts behind one placement interface:

1. **Taskbar embedding investigation.** Check the actual supported Windows 11 build(s), centered/left taskbar, secondary taskbars, occupied space and Explorer restart. Identify the exact extension mechanism. If it needs an Explorer hook, third-party shell extension or undocumented window manipulation, document compatibility, crash isolation, required privileges and license implications before adoption. Do not silently install a mod or make embedded mode the default based on an untested assumption.
2. **Documented fallback:** a small independent toolbar just above the taskbar, or an optional appbar reserving a narrow edge strip. A floating toolbar must avoid covering taskbar buttons; an appbar changes usable screen area. Expose that tradeoff clearly. This is a fallback, not proof that taskbar embedding has been achieved.

Exit criterion: demonstrate the real placement with maximized apps, Win+D, auto-hide taskbar, fullscreen apps, Explorer restart, multiple monitors and mixed DPI. If embedding cannot meet these requirements, record the result and settle the placement with the user before implementing the final host. Fullscreen presentation should suppress the toolbar by default on that display, with an explicit override; lock-screen controls are outside scope.

## Playback and audio

Use the Windows `GlobalSystemMediaTransportControlsSessionManager` through the existing Windows SDK projections. It exposes only sessions from apps integrated with Windows media controls, so browser/player compatibility requires testing. Follow the system's current session by default and offer an explicit session picker. Listen for session, playback, metadata and timeline changes; marshal immutable state snapshots onto WPF's dispatcher. See [session manager](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager?view=winrt-26100).

Previous/next/play-pause use the session's asynchronous `TrySkipPreviousAsync`, `TrySkipNextAsync` and `TryTogglePlayPauseAsync` requests. Check reported capabilities, returned success and subsequent state events; do not optimistically display a successful transition after a rejected command. Disable unsupported buttons. No usable session shows a compact neutral state instead of fabricated track metadata. See [session methods/events](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession?view=winrt-26100).

**Mute means system output mute**, labelled in its tooltip and settings. Use Core Audio `IMMDeviceEnumerator` and `IAudioEndpointVolume.GetMute/SetMute`; synchronize with external volume/mute changes and rebind when the default output device changes. Volume uses the same endpoint. GSMTC sessions do not provide the endpoint mute operation. Per-player mute is deferred because mapping a browser tab/media session to an audio session needs separate validation. See [IAudioEndpointVolume.SetMute](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-setmute).

The first technical spike must verify unpackaged/self-contained WPF access and any `globalMediaControl` capability or packaging requirements on supported Windows versions. Do not assume an AppX capability can simply be added to the current Win32 manifest. Failure must leave the clock usable and explain that media access is unavailable.

## Settings and appearance

Add a Media section after Weather and before Settings when this feature is implemented. Keep the shared Top center clock action; give media its own alignment controls to avoid moving one component accidentally when editing the other.

- Enable media controls; show/hide title and artwork; strip size, text size, color, opacity and background; selected display, placement, anchor, gap from taskbar, and position lock.
- Default to current-system session; permit choosing another available session. Do not persist transient session handles. If an optionally remembered source is absent, explain the fallback to the system session.
- Keep the strip present while paused; an explicit option can hide it when no sessions exist. Controls and context menu remain interactive even if the clock is click-through.
- All preferences autosave using the current atomic JSON/backup store. Existing profiles migrate with media disabled and preserve all clock settings.
- Extend appearance presets using an explicit allowlist for media style only. Preset application must not enable media monitoring, change audio state, select another output device, or store track history.
- Match the app's original dark style, accessible focus states, keyboard operation, accessible button names and high-contrast behavior. A tooltip expands an ellipsized title. Use a generic original icon when artwork is missing.

## Proposed code boundaries

MediaState, IMediaSessionService (in MediaState.cs), WindowsMediaSessionService, AudioEndpointService, MediaController and MediaStripWindow now exist. NowPlayingWindow and IMediaStripHost remain proposed. Flow.md documents the implemented subset.

| Proposed file | Responsibility |
| --- | --- |
| `src/ClockWidget.Core/MediaState.cs` | Immutable session/metadata/capability state and command result types; no Windows dependency. |
| `src/ClockWidget/Media/IMediaSessionService.cs` | Replaceable interface for available sessions, state changes and commands; fakes for tests. |
| `src/ClockWidget/Media/WindowsMediaSessionService.cs` | GSMTC discovery, active-session subscriptions, asynchronous commands and cancellation. |
| `src/ClockWidget/Media/AudioEndpointService.cs` | Core Audio interop, mute/volume/device notifications and COM lifetime. |
| `src/ClockWidget/Media/MediaController.cs` | Selected session, lifecycle, state snapshots, stale-result rejection and UI dispatch. |
| `src/ClockWidget/Media/MediaStripWindow.cs` | Compact interactive strip and keyboard/accessibility behavior. |
| `src/ClockWidget/Media/NowPlayingWindow.cs` | User-opened expanded panel, dismiss behavior and artwork decoding. |
| `src/ClockWidget/Media/IMediaStripHost.cs` | Placement abstraction; concrete hosts chosen after the placement spike. |

`Program.OnStartup` would start the controller only when enabled. `SettingsWindow` changes would flow through `Program.UpdateSettings`; `Program.OnExit` would cancel async work, unsubscribe session/audio events, close both media windows and release host/COM resources. Explorer restart would recreate/reposition the media host without restarting the clock or playback. Session events can arrive off-thread; only dispatcher work may update WPF views. Dispose old subscriptions on selection changes and ignore completed tasks for superseded sessions.

## Privacy, reuse and licensing

Write original MIT application code/assets attributed to Saqib Hussain. Start with existing SDK/OS APIs to avoid an additional application package. Review exact licenses and transitive dependencies before adopting any helper, shell host or library; add accepted components and notices to the inventory and portable ZIP. Public source availability alone is insufficient permission. No code, fonts, icons, branding or artwork is copied from the reference apps. The existing MIT/OFL/runtime licensing rules continue to apply.

Read metadata/artwork only from the selected Windows session while the feature is enabled. Hold it in memory, cap artwork size and discard it when sessions change. Do not scrape players, fetch cover images from a new online service, record audio, persist listening history or publish titles/artwork in logs, fixtures, presets, screenshots or Git. Runtime album art remains third-party content; access through a player does not grant redistribution rights. Use repository-authored placeholder metadata/artwork for tests and documentation. Weather consent remains independent.

## Implementation sequence and acceptance

1. **Spikes:** demonstrate media access/commands, endpoint mute and taskbar placement on the current portable WPF app. Record exact OS builds and packaging needs. Decide the host using evidence.
2. **Core experience:** implement current-session discovery, previous/play-pause/next, explicit system mute, compact title strip, independent enable/visibility and safe startup/shutdown.
3. **Expanded panel:** add session selection, artwork, metadata and volume. Add only supported optional shuffle/repeat/seek controls afterwards.
4. **Settings/themes:** autosave, migration, media appearance presets, display/position controls, accessible behavior and consistent original visuals.
5. **Release gate:** review dependencies/notices, update decisions.md/Flow.md to actual files/methods, and package only after compatibility tests pass. Do not promote the preview to a stable media release before these gates pass.

Acceptance checks include: no/one/multiple sessions; browser video and a desktop music player; unsupported buttons; session closing during a command; late metadata/artwork; rejected requests; rapid repeated clicks; mute/volume changed outside the app; headphones/Bluetooth output switching; no output device; settings restart/backup recovery; media alone with clock hidden; theme application without playback side effects; DPI/monitor removal; taskbar auto-hide; maximized/fullscreen apps; Explorer restart; sleep/resume; keyboard/high contrast; no background media work when disabled; and full COM/event/host cleanup on exit.

Use fake media/audio services for deterministic tests. Real player and shell compatibility remains an explicit interactive checklist, not something to infer from mocks. No playback or mute commands should be sent to the user's current media as part of planning.
