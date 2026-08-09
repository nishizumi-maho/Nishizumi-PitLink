# Nishizumi PitLink

A small Windows tray app that watches iRacing and tells **MOZA Pit House** which Motor preset to
load for the car you're currently driving — nothing more.

![Platform](https://img.shields.io/badge/platform-Windows-blue.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

## What this app does (and doesn't do)

Nishizumi PitLink is a **connector, not an FFB editor**. All force feedback tuning happens in MOZA
Pit House, exactly like today. This app only:

1. Watches iRacing's telemetry to know which car (and, optionally, which track) you're on.
2. Looks up which MOZA Pit House Motor preset you assigned to that car.
3. Reads that preset fresh from disk and pushes it to the wheelbase through the official MOZA SDK.

It never invents, edits, or stores its own FFB values. If you change a preset in Pit House, the
next switch (or "Reapply preset for current car") picks up the new values automatically — there's
no separate import/sync step to remember.

## Features

- Detects the current car by reading iRacing's shared-memory telemetry directly (no third-party
  SDK wrapper — see [How it works](#how-it-works)) and matches it to a MOZA Pit House Motor preset
  (`.mzpreset`), by car or by car class.
- **Optional per-track profiles**: turn on "Enable per-track profiles" in Settings to map a preset
  to one specific car on one specific track/layout. A car+track mapping always wins over a plain
  car or class mapping when it matches.
- Lists cars iRacing has reported that don't have a preset assigned yet, so you can assign one in
  a couple of clicks.
- Runs from the tray, optionally starting with Windows.
- Reads presets straight from Pit House's own preset folder (`Documents\MOZA Pit House\Presets\Motor`)
  — no copies, no drift.

## How it works

```
iRacing (telemetry) --> Nishizumi PitLink --> reads .mzpreset --> MOZA SDK --> wheelbase
                              ^
                              | you configure this mapping
                          (Cars tab)
```

Matching priority when a car is detected: **car + track** (if enabled and mapped) → **car** →
**car class**. Only the subset of FFB fields the MOZA SDK has a confirmed mapping for (strength,
torque, spring, damper, friction, speed damping, angle) is pushed — road-feel equalizer bands and
soft-limit tuning are intentionally left untouched, since Pit House itself already applies them
when you load the preset there.

Car and track identity come straight from iRacing's own shared-memory telemetry block (the same
memory-mapped file and header layout iRacing documents in its public SDK), read directly by this
app in [`Services/IRacingCarWatcher.cs`](Services/IRacingCarWatcher.cs) — only the small slice of
session-info YAML needed to identify the car and track is parsed; the full telemetry variable
buffer is never touched. This is a deliberate choice: it keeps the app dependency-light and
avoids any third-party telemetry SDK wrapper (several of which are GPL-licensed, which would force
this whole repo under GPL terms) so the project can stay plain MIT end to end.

## Requirements

- Windows 10/11 (64-bit)
- [MOZA Pit House](https://www.moza-r.com/) installed and running (this app talks to the
  wheelbase *through* Pit House, so Pit House must stay open)
- iRacing with memory-mapped telemetry enabled (`irsdkEnableMem=1` in `app.ini`, on by default)
- .NET 8 Desktop Runtime (x64) to run a published build, or the .NET 8 SDK to build from source

## Getting the MOZA SDK

This repo does not include MOZA's proprietary SDK binaries. Download the official MOZA Racing SDK
and copy these three files into [`lib/x64/`](lib/x64/) before building:

- `MOZA_API_C.dll`
- `MOZA_API_CSharp.dll`
- `MOZA_SDK.dll`

## Building

```
dotnet build
```

or open `NishizumiPitLink.csproj` in Visual Studio / Rider.

## Using the app

1. Open MOZA Pit House and tune/save your Motor presets there as usual.
2. Launch Nishizumi PitLink and open the **Cars** tab.
3. Play iRacing normally — unmapped cars show up under "Cars seen in iRacing without an assigned
   preset". Click **Assign preset** and pick the Pit House preset that car should use.
4. (Optional) In **Settings**, enable per-track profiles if you want a specific car+track
   combination to use a different preset than that car's default. Then use **Map current car for
   this track only** (Cars tab) or **Assign for this track only** (in the discovered-cars list)
   while you're on that track.
5. From then on, switching cars in iRacing automatically pushes the matching preset to the
   wheelbase. Toggle "Automatic switching active" in the top bar to pause this at any time.

Mappings are stored in `%AppData%\NishizumiPitLink\state.json`.

## License

MIT — see [license.md](license.md).
