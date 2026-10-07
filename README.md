# StarHealth

Native Windows (WinUI 3) dashboard for Starlink dishes. Desktop shell that mirrors
the official app's sections (left rail: En línea, Estadísticas, Red, Suscripción,
Obstrucciones, Alineación, Prueba de velocidad, Configuración, Asistencia), with
every datum labeled by source. Cross-OS ready: all dish logic lives in UI-free
libraries that move unchanged into an Uno Platform head.

## Install (regular Windows users)

Download **StarHealth-Setup-*-win-x64.exe** from
[Releases](https://github.com/ralejomorejon/StarHealth/releases)
and run it — no extra runtimes needed (self-contained, 64-bit Windows).
Unsigned alpha: Windows SmartScreen will warn on first launch.
Run it on the same LAN as your dish (`192.168.100.1`).

## Monitor behavior

- **Local history (SQLite, 30 days):** every poll is recorded, so the
  Estadísticas chart offers 15 min / 24 h / 7 d ranges, availability
  percentages, and one-click outage reports (clipboard or
  `Documents\StarHealth`) even across restarts.
- **Tray + notifications:** close minimizes to the tray (toggleable); toasts
  fire on connection loss/recovery, long outages (≥ 5 s), pending update
  reboots and new misalignment warnings. The tray tooltip always shows the
  live status line.

## Run from source

## Where each datum comes from (read this)

The official app mixes three sources. This app reads the first, parts of the
second, and honestly marks the third.

| Screen datum | Source | How |
|---|---|---|
| Estado, actividad, throughput, latencia, pérdida | Dish gRPC `:9200`, `get_status` | unauthenticated LAN |
| Ping exitoso %, latencia mediana (15 min) | Dish-computed `get_history` stats; else derived locally from the poll ring | `total_ping_drop/samples`, `deciles_full_ping_latency[5]` |
| Obstrucción total 24 h, cuñas, mapa | Dish `obstruction_stats` | note: per-wedge detail is obsolete on new firmware |
| Interrupciones con hora | Dish `get_history.outages[]` (cause + GPS-epoch timestamps) | validated live: 14 entries, NO_PINGS/NO_DOWNLINK, 0.7 s each |
| Eventos | Dish `event_log` UX events (severity/reason) | shown in Estadísticas |
| Mapa del cielo (heatmap) | Dish `dish_get_obstruction_map` (123×123 SNR grid live) | rendered on demand in Obstrucciones |
| Ethernet, calidad de señal, batería, config | Dish status fields (`eth_speed_mbps`, `signal_quality`, `battery_stats`, `dish_get_config`) + `get_diagnostics` (self-test, stowed) | SNR itself is obsolete; `signal_quality` replaces it |
| Alertas, equipo, software, updates | Dish `alerts`, `device_info`, `software_update_*` | full alert bit list incl. heating, water, power-save |
| Orientación (acimut/elevación) | Dish `boresight_azimuth_deg/_elevation_deg` | still reported, incl. fixed dishes |
| Inclinación + objetivo + desviación | Dish `alignment_stats`: `tilt_angle_deg`, `desired_boresight_*` | angular separation actual vs desired → the "desalineada por N°" card, computed locally |
| Ubicación | Dish `get_location` (lla) | needs the in-app privacy opt-in or returns PERMISSION_DENIED |
| Energía (W) | Dish `get_history` power stats **when the hardware reports them** (0.0 = unsupported) | your Mini reports ~32 W mean / ~40 W live — shown as "Medido por la antena" |
| Red Wi-Fi, clientes, señal por cliente | **Router** gRPC (`192.168.1.1:9000`), not the dish | separate API, same reflection trick fits |
| Plan, facturación, referidos, chat, objetivo exacto de alineación | **SpaceX cloud, logged-in only** | no public API; not replicable without credentials |
| Prueba de velocidad | Local measurement (this PC → Cloudflare) | the official dish↔internet test is orchestrated by their cloud |

Deliberately not replicated: anything requiring your SpaceX login. The app never
asks for it. Plan name is stored locally (Suscripción page) for the header.

## Framework choice

Pure WinUI 3 is Windows-only, which conflicts with cross-OS goals. So:

- `StarHealth.Core` — models, `IDishClient`, polling, demo client. **Zero UI deps.**
- `StarHealth.Data.Grpc` — live dish client via server reflection. **Zero UI deps.**
- `StarHealth.App` — thin WinUI 3 head. XAML uses only controls that exist in
  Uno's WinUI flavor; charts are hand-rolled `Canvas`+`Polyline`.

To ship other OSes: `dotnet new install Uno.Templates`, new Uno (WinUI) app,
move `Views/`, `Controls/`, `ViewModels/` plus the two libraries unchanged.

## Dish access (no stale .proto files)

Plaintext gRPC (h2c) at `http://192.168.100.1:9200`, service
`SpaceX.API.Device.Device/Handle`. Field numbers are discovered at runtime via
**gRPC server reflection** (same trick as `sparky8512/starlink-grpc-tools`'
`dish_control.py`); only stable field *names* are mapped, with semantics from
that project's docs (e.g. state derives from the `outage` field, status `snr`
and wedge detail are obsolete). If the dish is unreachable the app banners the
error and keeps showing **demo data**.

> Own router? You need a route to `192.168.100.1` from your LAN, exactly like
> the official app requires. If it works there, this works.

## Run

```powershell
dotnet build StarHealth.slnx
dotnet run --project src/StarHealth.App/StarHealth.App.csproj
```

## Layout

```text
src/StarHealth.Core/        models, IDishClient, DishPollingService, DemoDishClient, Format
src/StarHealth.Data.Grpc/   DishEndpointOptions, GrpcDishClient, ProtoCodec (reflection-based)
src/StarHealth.App/         WinUI 3 head: ShellPage + 9 section pages,
                            Controls/ThroughputChart+Sparkline, ViewModels/DashboardViewModel
```

## Deliberate non-goals for v1

- No dish *control* (reboot/stow/sleep) — read-only.
- No obstruction heatmap rendering — plumbing exists (`ObstructionMap`).
- No router client list yet — reachability check only.
- No history backfill across restarts — in-memory ring.
- No remote (off-LAN) access — that path goes through SpaceX's cloud, not the dish.
