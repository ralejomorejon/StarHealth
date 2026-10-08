# StarHealth — agent context (Claude Code handoff)

Native Windows (WinUI 3) dashboard for Starlink dishes. Live unauthenticated
LAN gRPC (`http://192.168.100.1:9200`, schema via server reflection — never
stale `.proto` files). Desktop shell mirrors the official app's 10 sections.
Cross-OS ready: all dish logic is UI-free for a future Uno head. es/en UI.
Falls back to simulated DEMO data when the dish is unreachable.

## Stack

- `net10.0-windows10.0.26100.0`, WinUI 3, Microsoft.WindowsAppSDK **2.5.1**
  (CsWinRT 2.2.0), CommunityToolkit.Mvvm 8.4.0, H.NotifyIcon, SkiaSharp 3.119.4
- Self-contained unpackaged exe, installed per-user via Inno Setup
  (`installer/StarHealth.iss`). No MSIX identity → see identity rule below.
- Trimming is ON for Release (`PublishTrimmed=true`). **Never turn it off**
  (untrimmed builds die ~2s after launch — CsWinRT static vtable coverage).

## Layout

```text
src/StarHealth.Core/            models, IDishClient, DishPollingService, DemoDishClient,
                                Localization/Text.cs (252 es/en keys), Format
src/StarHealth.Data.Grpc/       DishEndpointOptions (+DishEndpointValidation),
                                GrpcDishClient (reflection-based), ProtoCodec
src/StarHealth.Data.Store/      HistoryStore (SQLite 30-day samples, SLA, reports)
src/StarHealth.App/             WinUI head (the only UI project)
  App.xaml.cs                   composition root + crash.log breadcrumb
  LocalData.cs                  identity-safe storage (read the doc comment)
  AppVersion.cs                 version parse/compare + release-feed picking (UI-free)
  MainWindow.xaml.cs            tray icon (H.NotifyIcon), close-to-tray, Quit
  ViewModels/DashboardViewModel.cs  THE god object (~950 lines): fleet, polling,
                                formatting, toasts, updater, all page state
  Views/ShellPage               left rail + centered 1060px column + switch curtain
  Views/Status|Stats|Network|Account|Obstructions|Alignment|Speed|Settings|About|Help
  Controls/DishHero(.xaml.cs)   drag-orbit dish hero (60fps, time-based damping)
  Controls/DishHeroScene.cs     pure SkiaSharp scene (shared with PNG harness)
  Controls/ThroughputChart, Sparkline  hand-rolled Canvas charts
installer/StarHealth.iss        Inno Setup, parameterized AppVersion
.github/workflows/release.yml  tag v* → publish → ISCC → GitHub release + .sha256
docs/animation-plan.md          hero research + P0-P3 plan
CHANGELOG.md                    Keep-a-Changelog, per-version entries
```

## Build / verify (Windows PowerShell here — notes below)

```powershell
dotnet build StarHealth.slnx -v q --nologo
dotnet publish src/StarHealth.App/StarHealth.App.csproj -c Release -r win-x64 --self-contained -p:WindowsAppSDKSelfContained=true --nologo -v q
# Launch test (installed-style, unpackaged): alive + window handle = pass
$p = Start-Process 'src\StarHealth.App\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\StarHealth.App.exe' -PassThru
Start-Sleep -Seconds 30; $p.Refresh()
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; 'ALIVE' } else { $p.ExitCode }
# Page-proof flip (can't click headlessly): temporarily change Go("Estado") in
# ShellPage.xaml.cs to the target tag, publish, launch-test, REVERT before ship.
```

Headless harnesses (throwaway, in TEMP not repo): link a UI-free `.cs` into a
console project and assert it. Precedents in `%LOCALAPPDATA%\Temp\`: `HeroShot`
(PNG renders of `DishHeroScene`), `LocalDataTest` (7 asserts), `UpdateTest`
(15 asserts), `EndpointTest` (14 asserts), `CtsTest` (proved the CTS race 20/20).

## Crash triage playbook (used for every bug below)

1. `%LOCALAPPDATA%\StarHealth\crash.log` — managed breadcrumb (may be absent on
   native FailFast deaths; then step 2).
2. Event Viewer → Windows Logs → Application: `Application Error 1000`
   (faulting module + `Exception code`) and `.NET Runtime 1026` (managed
   `Exception Info` with stack). WER `ReportArchive\AppCrash_StarHealth*` has
   `Report.wer` — `Sig[6].Value` is the HRESULT (`80073d54` no package identity,
   `80004003` E_POINTER, `80070490` element-not-found, `e0434352` managed).
3. Repro headlessly when possible (launch test, page-flip test, mechanics harness).

## Hard rules (each paid for in crashes — do not regress)

1. **No `ObservableCollection` (ANY `T`, incl. `string`) bound to
   `ItemsControl.ItemsSource` / `ComboBox.ItemsSource`.** Marshalling it to the
   ABI crashes the process (`E_POINTER` via CsWinRT vtable synthesis hitting a
   phantom `ComInterfaceEntry` in CsWinRT 2.2.0 + net10). Lists cross as
   **preformatted strings** into `TextBlock`s, or WinRT-native items created in
   code-behind. Verified: custom collections die in 2–14s, strings-in-lists die
   identically; text/primitives never do. (Grep must show zero `ItemsSource={`.)
2. **Never mutate a `ComboBox`'s `Items` inside its `SelectionChanged`
   cascade** (native reentrancy fault, `E_NOTFOUND`, no managed stack). Resyncs
   go through `DispatcherQueue.TryEnqueue` + `_syncDish`-style guard +
   page-active guard. See `SettingsPage.xaml.cs`.
3. **No `Windows.Storage.ApplicationData` except via `LocalData`.**
   Unpackaged = no package identity = `0x80073D54` instant kill. `LocalData`
   falls back to `%LOCALAPPDATA%\StarHealth` (+ `settings\*.txt` for values).
   Bools persist as `"true"`/`"false"` (`bool.TryParse` rejects `"1"`/`"0"`;
   reads still accept legacy files).
4. **Poll-loop lifecycle:** `SwitchTo` cancels+disposes the old CTS while its
   loop is mid-poll — `LoopAsync` must tolerate `ObjectDisposedException`
   (awaiting a disposed CTS's timer throws ODE, never OCE) or the faulted
   task kills the process on GC. Switches are serialized behind
   `IsSwitchingDish` + shell curtain, first-poll release, 12s timeout.
   `SwitchTo` returns bool and never throws (rebuilds polling on failure).
5. **`x:Bind` without `Mode` is `OneTime`.** Any value that changes needs
   explicit `Mode=OneWay` (this bit us on the dish indicator).
6. **Every poll-driven UI update goes through `Apply`, which is guarded** to
   log-to-`crash.log` instead of closing on a malformed snapshot.
7. **Hosts must form URIs.** `GrpcChannel.ForAddress` throws `UriFormatException`
   on the UI thread for `my dish` / `1.2.3.4:9200` / unbracketed IPv6.
   `DishEndpointValidation` enforces `http://host:port/` shape; unreachable-but-
   wellformed stays valid (lazy connect → demo fallback).
8. **Icons:** exe icon via `<ApplicationIcon>`, file copy via
   `CopyToPublishDirectory` (WinUI `Content` doesn't reach unpackaged publish
   otherwise). Both verified present in publish output.
9. **New UI strings go in `Text.cs`** as `("es", "en")` tuples; XAML reads them
   via `Vm.Tr('key')`; one-shot labels reseeded in `ReseedStaticText`.
   Missing keys render as the key itself, never blank.

## Release process

```powershell
git add -A; git commit -m "..."; git push origin main
git tag vX.Y.Z-alpha; git push origin vX.Y.Z-alpha   # CI does the rest
```

CI stamps `-p:InformationalVersion=<tag>` (the app's only self-version for the
in-app updater), builds the installer, attaches `.exe` + `.exe.sha256`, release
body uses `${{ github.ref_name }}` (never hardcode a version there). Unsigned
alpha → SmartScreen warns (More info → Run anyway); signing cert is still open.
Prefer new tags over moving old ones. In-app updater (About page): version
display + feed check + SHA256-verified download + quit-and-install.

## Current state

- Released: **v0.1.8-alpha** (`origin/main`). Installs, launches, polls,
  multi-dish switching, updater, tray/toasts, icons all verified on hardware
  or headlessly (Status 90s; Stats/Obstructions/Settings/About 30–40s each).
- Local `main` is 3 commits ahead, unreleased: hero-motion rework (60fps
  OrbitControls parity), dish-switch CTS fix, malformed-host hardening.
- Data dir (unpackaged): `%LOCALAPPDATA%\StarHealth\history.db`,
  `settings\`, `crash.log`. Poll cadence 2s. Dish timestamps are ns since GPS
  epoch 1980 (naive unix conversion lands in 2016 — known).

## Open / next

- Manual tap-through: add bogus dish → DEMO fallback → switch ×3 → delete
  (needs fingers; headless can't click).
- Updater end-to-end (check → 44MB download → hash → quit → installer).
- Code-signing cert (kills SmartScreen).
- Hero P3 polish; obstruction-dome tie-in; router client list; all still open.

## Environment quirks (this box)

- Shell is PowerShell 5.1: no `&&`, no `head`/`tail`/`grep`/`cat -n`. Chain with
  `;`, page with `Select-Object -First N`. `pwsh` (PS 7) also exists.
- No python3. Temp harnesses go in `$env:TEMP\<Name>\` (pre-approved dir).
- `dotnet run` registers a sparse MSIX identity (works); raw exes and the
  installer build don't (that's what `LocalData` is for).
- GUI apps under test: launch with `Start-Process -PassThru`, poll
  `$p.HasExited`, `Stop-Process` to end. Never leave test windows running.
- CRLF warnings on git add are normal here.
