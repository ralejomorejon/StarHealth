# Changelog

All notable changes to StarHealth will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/)
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed
- **Switching dishes crashed the app.** `SwitchTo` cancelled AND disposed the
  old `CancellationTokenSource` while its polling loop was still mid-poll;
  when that loop then awaited `PeriodicTimer.WaitForNextTickAsync` with the
  dead token it threw `ObjectDisposedException` (proven 20/20 in a harness —
  it never throws `OperationCanceledException` there), which `LoopAsync`
  didn't catch. The faulted fire-and-forget task killed the process on GC,
  seconds after the switch. The wait now breaks cleanly on both exceptions,
  and the per-loop timer is disposed with its loop.
- **Dish hero motion (OrbitControls parity).** The loop ran at ~30 fps off
  vsync and physics was per-tick, so movement stuttered; release velocity
  came from raw per-event pixels, so flings varied with mouse poll rate and
  elevation had no inertia at all; vertical drags also scrolled the page
  because pointer events were never marked handled. Now: ~60 fps loop,
  time-based exponential damping (deg/sec) on both axes with poll-rate
  independent release velocity, `e.Handled` during drags, grab/move pointer
  cursors, idle drift at autoRotate-like 10°/s. Matches the official app's
  damped-orbit feel within what CPU SkiaSharp rendering allows.

## [v0.1.7-alpha] - 2026-10-07

### Added
- **In-app updater (About page).** Shows the installed version, checks the
  GitHub Releases feed, and on newer tags downloads the installer with
  progress, verifies its SHA256 sidecar (fail-closed when missing or
  mismatched), then quits the app and launches the installer so files can
  replace. CI now stamps `-p:InformationalVersion` with the tag and attaches
  a `.sha256` file next to every installer.
- `AppVersion`/`UpdateFeed` helpers (version parse/compare, newest-with-asset
  picking) are UI-free and covered by a headless harness (15/15 asserts).

### Fixed
- Verified the About page headlessly (40 s live run, previously unproven).

## [v0.1.6-alpha] - 2026-10-07

### Fixed
- **Notification (and tray) toggles didn't stick on installed builds.**
  `LocalData` wrote booleans as `"1"`/`"0"` but read them back with
  `bool.TryParse`, which only accepts `"true"`/`"false"` — so every restart
  silently reset both toggles to ON and notifications kept showing. Writes
  are now `"true"`/`"false"`, reads still accept legacy `"1"`/`"0"` files.
- Verified with a headless round-trip harness against the real `LocalData`
  in unpackaged conditions (7/7: false/true persist, strings persist,
  missing-key defaults, legacy `"0"`/`"1"` compat).

## [v0.1.5-alpha] - 2026-10-07

### Fixed
- **Installed app showed no icon (taskbar, window, tray).** Two root causes:
  `Assets/AppIcon.ico` never reached unpackaged publish output (verified
  missing), so `SetIcon` silently no-opped and the tray probe fell through —
  and the exe itself carried only the default dotnet icon, so even the
  fallback was wrong.
- `Assets\AppIcon.ico` is now embedded as the Win32 exe icon
  (`ApplicationIcon`: taskbar, Alt-Tab, Explorer, installer shortcuts, and
  the tray fallback all use it) and also copied next to the exe
  (`CopyToPublishDirectory`, picked up automatically by the Inno script's
  recursive publish-dir include), so `SetIcon` and the tray's first probe
  path both hit the real file.
- Verified against a Release publish: `Assets\AppIcon.ico` ships, app holds
  its window 30 s with the icon paths live.

## [v0.1.4-alpha] - 2026-10-07

### Fixed
- **Statistics tab crashed the app (E_POINTER).** Root cause, proven via WER
  fault buckets + a `crash.log` breadcrumb + headless page-by-page launch
  runs: marshalling any `ObservableCollection` (custom-type AND string) into
  `ItemsControl.ItemsSource`/`ComboBox.ItemsSource` dies inside CsWinRT's
  runtime vtable synthesis (`GetAbiToProjectionVftblPtr` NRE / phantom
  top-level `ComInterfaceEntry` TypeLoad in CsWinRT 2.2.0 + net10). It looked
  like "clicking Stats crashes" but the first poll landing after navigation
  was the actual trigger.
- Alerts, timeline, and obstruction wedges now render as preformatted-string
  `TextBlock`s (per-group colors kept: green/amber/dim); the dish switcher
  `ComboBox` is populated imperatively in code-behind. Only
  strings/primitives cross the ABI now. Deleted `AlertTemplateSelector`,
  `TimelineTemplateSelector`, `SyncRows`, and the dead `AlertRow`/`WedgeRow`
  records.
- Trimming stays ON (required for CsWinRT static vtable coverage — untrimmed
  builds die ~2 s after launch on every page).
- Added permanent `crash.log` breadcrumb (`%LOCALAPPDATA%\StarHealth\`) for
  future field reports.
- Verified headlessly against Release builds with live polling: Status 90 s,
  Stats / Obstructions / Settings 40 s each, zero crashes (previously died in
  2–14 s).

## [v0.1.3-alpha] - 2026-10-07

### Fixed
- **Installed app crashed silently on launch (0x80073D54).** The Inno installer
  ships an unpackaged build with no MSIX package identity, where
  `Windows.Storage.ApplicationData` throws `APPMODEL_ERROR_NO_PACKAGE` — the
  unprotected `LocalFolder` call in `App.OnLaunched` killed the app before any
  window appeared (confirmed via WER fault bucket + local launch repro).
- New `LocalData` helper: `LocalFolder`/`LocalSettings` when packaged, file
  fallback under `%LOCALAPPDATA%\StarHealth` when not. All dish endpoints,
  language, plan, and toggle settings plus `history.db` now persist for
  installed users too. Verified: fixed build launches, holds a window, and
  writes `history.db` via the fallback path.
- `AppWindow.SetIcon("Assets/AppIcon.ico")` wrapped in try/catch — publish
  output ships no `Assets/` folder, so this was the next crash in line.
- Release notes body now uses `${{ github.ref_name }}` instead of a hardcoded
  version string.

## [v0.1.2-alpha] - 2026-10-07

### Changed
- Release notes are plain text (version + SmartScreen first-launch notice)
  instead of auto-generated commit lists or a markdown link.
- README documents the SmartScreen bypass (More info → Run anyway) and the
  `%LOCALAPPDATA%\StarHealth` fallback location is now the actual data dir
  for unpackaged installs.

## [v0.1.1-alpha] - 2026-10-07

### Added
- **Dish hero scene** (P0-P2): SkiaSharp-rendered floating dish over starfield with status beam
- **Drag-orbit interaction**: mouse drag to orbit, inertia on release, idle drift, reduced-motion freeze
- **Live tilt/beam bindings**: `Alignment.TiltDeg` → dish tilt, obstruction fraction → beam color (green/amber/red), offline dimming
- **StatusPage hero slot**: DishHero control integrated into the Status page layout
- **PNG scene renders**: headless verification of ok/warn/bad states at two sizes

### Changed
- **StatusPage layout**: Dish hero now headline section; cards below (ping, latency, throughput, obstruction, alignment)
- **DashboardViewModel**: `HeroTilt` and `HeroBeam` properties wired from `Alignment` and `Obstruction` data
- **ThroughputChart**: 24px footer lane for legend; hover readouts (HH:mm:ss + ↓/↑ Mbsp); flicker fix with keyed SyncRows + content signature guard
- **Obstruction graph**: replaced with greyed "coming soon" dome draft
- **Power sparkline**: in ENERGÍA card with power-series binding
- **Green alerts template selector**: heater, power-save idle, routine reboot render green
- **Outages + events card**: merged single "events and interruptions" card with toggle for brief (<1s) events
- **Always-visible navigation bar**: gradient-to-transparent fade
- **Content centering**: shell-driven width constraints (`min(viewport, 1060)` centered)
- **Multi-dish endpoint support**: named entries, persistence in `LocalSettings`, switcher + editor with validation
- **DEMO fallback**: when gRPC unreachable, shared demo client keeps every screen functional
- **SQLite HistoryStore**: 30-day retention, per-poll samples, deduped outages/events, SLA availability percentages, one-click outage reports
- **Tray icon**: H.NotifyIcon with context menu (Show/Quit), close-minimizes-to-tray toggle, toast notifications
- **Full English/Spanish localization**: 252 shared keys verified across UI, ViewModel, and dish client
- **Release automation**: GitHub Actions workflow (publish → Inno → release); CI validated on test tag

### Fixed
- Signal card removed from Estadísticas (SNR obsolete on current firmware)
- Obstruction graph replaced with greyed "coming soon" dome draft
- SmartScreen warning noted for unsigned alpha builds

## [v0.1.0-alpha] - 2026-10-07

### Added
- Initial alpha release
- Desktop shell with 9 NavigationView sections (Status, Stats, Network, Account, Obstructions, Alignment, Speed, Settings, About)
- Live gRPC dish client via server reflection (no stale .proto files)
- Multi-dish endpoint support: named entries, persistence in LocalSettings, switcher + editor with validation
- SQLite HistoryStore: per-poll samples (30-day retention), deduped outages/events, availability (SLA) percentages, one-click outage reports
- Tray icon from shipped .ico with layout-probe fallback
- Tray + notifications: Show/Quit context menu, close-minimizes-to-tray toggle, toasts (offline/online transitions, outages ≥5s, pending update reboots, misalignment warnings)
- Full English/Spanish localization: 252 shared keys verified across UI, ViewModel, dish client
- Side-by-side availability (24h/7d) with 1px divider in Estadísticas
- Obstruction graph replaced with greyed "coming soon" dome draft
- Power sparkline in ENERGÍA card
- Green alerts template selector (heater, power-save idle, routine reboot reboots render green)
- Merged outages + dish events into single "events and interruptions" card with toggle for brief (<1s) events
- Always-visible navigation bar with gradient-to-transparent fade
- Content centering via shell-driven width constraints (min(viewport, 1060) centered)
- Throughput chart legend overlap fixed (24px footer lane)
- Hover readouts on throughput chart (HH:mm:ss + ↓/↑ Mbps) and sparklines
- Flicker fix: keyed SyncRows + content signature guard in RefreshTimeline
- About tab: how it works, stack, unofficial project notice, GitHub link (ralejomorejon)
- Signal card removed from Estadísticas
- Release automation: GitHub Actions workflow (publish → Inno → release)
- CI validated on test tag v0.0.0-ci-test

---

Pre-release alpha builds: Windows SmartScreen will warn on first launch. Signing with a trusted cert removes that warning (a later release step).