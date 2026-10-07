# Dish hero animation plan (Online tab)

Goal: floating Starlink dish over a starfield, interactive, imitating the
official app's hero — recreated from scratch, no SpaceX assets.

## What the official app does (researched, Oct 2026)

- React Native + Expo; **real-time 3D via Three.js on ExpoGL**, declarative
  with React Three Fiber (source: SpaceX engineer Aaron Grider, App.js conf
  talk; notjust.dev writeup; corroborated across RN-news roundups).
- One shared 3D canvas behind a transparent navigator; the camera
  interpolates between per-screen base positions on navigation.
- Custom orbit controls ported to native touch (drag rotate, pinch zoom);
  touches plumbed through the overlay content to the canvas.
- **Data-driven scene:** beam color ← obstruction count, wire color ← signal
  strength, real actuator tilt mirrored live; Mars-mode Easter egg proves the
  scene is fully parametric.
- Their admitted pain: single JS thread stutters, custom model loading for
  low-end Android.

## Constraints for StarHealth

- No SpaceX models/textures (copyright) — dish drawn procedurally.
- Same code must run on Uno targets later (Android/iOS/macOS/Linux/Web).
- Offline, 60 fps on iGPU, dark palette, es/en, respect reduced-motion.

## Options considered

- **A. SkiaSharp canvas — RECOMMENDED.** `SkiaSharp.Views.WinUI.SKXamlCanvas`
  on Windows, SkiaSharp Uno views elsewhere: one codebase everywhere.
  Starfield parallax + dish slab projected from live az/el/tilt + additive
  beam tinted by obstruction/state + drag-orbit with inertia.
- B. WebView2 + local three.js page. Closest imitation (their exact stack),
  real 3D + OrbitControls free. Rejected for v1: WebView2 runtime dependency,
  memory weight, weak Uno story, JS↔C# bridge for live data.
- C. Lottie / AnimatedVisualPlayer. Buttery but canned — zero interactivity,
  fails the requirement.
- D. Win2D. Nice on Windows, dead end for Uno — rejected on portability.
- E. XAML shapes only. No projection, weak imitation.

## Phases

- **P0 — shell:** SkiaSharp package + `DishHero` control in the StatusPage
  hero slot. Static frame: 3-layer starfield, dish slab, beam. Acceptance:
  renders at 60 fps, no data wired yet.
- **P1 — live bindings:** tilt + boresight from `alignment_stats`, beam color
  from obstruction fraction/state (green→amber→red), offline dimming.
  All values already exist in `DishSnapshot` today.
- **P2 — interaction:** drag to orbit (clamped azimuth/elevation), inertia,
  slow idle drift; static frame when Windows reduced-motion is on.
- **P3 — polish:** satellite dot traversing the beam, misalignment emphasis,
  tie-in with the obstruction dome when it ships.

Non-goals: AR sky scanner (camera + ML, separate project), ripping their
models, WebView2 fallback unless SkiaSharp proves insufficient.

## Bonus finding (separate spike, not this plan)

titleos.dev teardown (Jul 2026, APK + firmware analysis): the dish serves
**Slate JSON telemetry over WebSocket on port 8065** (push events incl.
obstructions) and **gRPC-Web on port 9021** without TLS/auth. Candidate
future live-event source to replace 2 s polling — needs its own spike to
confirm accessibility from a normal LAN client in bypass mode.
