# Changelog

All notable changes to StarHealth will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/)
and this project adheres to [Semantic Versioning](https://semver.org/).

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