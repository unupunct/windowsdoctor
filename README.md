# Windows Doctor

A Windows diagnostic and repair utility built with WPF and .NET 8 — inspired by PC Health Check, Sysinternals, and HWiNFO.

Windows Doctor scans your system for hardware, storage, security, network, and software issues, explains what it found in plain language, and offers one-click repairs where it's safe to do so.

## Features

- **8 diagnostic modules** — Hardware, Storage, Windows Health, Performance, Security, Network, Software, Event Logs
- **Health score** — a weighted 0–100 score per category and overall, updated on every scan
- **Disk Life & Performance** — real S.M.A.R.T. data read via ATA pass-through (health %, temperature, power-on hours, reallocated/pending sectors, full attribute table) plus a live, cache-bypassed throughput benchmark
- **Event Log troubleshooting** — reads real System/Application log entries and matches known patterns (GPU driver crashes, BSOD bug-check codes, hardware errors, disk I/O errors) to a plain-language cause and a suggested fix, with a one-click launch into Event Viewer
- **Repair Center** — 17+ safe, confirmable repairs (SFC, DISM, DNS flush, Winsock reset, temp cleanup, Defender/Firewall enable, restore point creation, and more), each with a risk badge and elevation check
- **Scan history** — every scan is saved to a local SQLite database for comparison over time
- **Reports** — export scan results to HTML, JSON, or CSV
- **Advanced Tools** — quick shortcuts to Windows' own diagnostic tools (Device Manager, Event Viewer, Reliability Monitor, God Mode, and more)

Windows Doctor never performs a destructive or system-changing action without an explicit confirmation.

## Requirements

- Windows 10 or 11 (x64)
- No installation required — single self-contained `.exe`
- **Administrator privileges** are required for full S.M.A.R.T. disk data; without them, Windows Doctor still runs normally but shows a banner explaining the limitation

## Getting started

Download the latest `WindowsDoctor.exe` from the [Releases](../../releases) page and run it — no installer, no dependencies.

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/unupunct/windowsdoctor.git
cd windowsdoctor
./Build.ps1 -Publish
```

The published single-file executable is written to `src/WindowsDoctor.UI/bin/publish/WindowsDoctor.exe`.

## Architecture

Clean Architecture across 9 projects with a strict, one-directional dependency flow:

```
Common  →  Core  →  Database / Services / Diagnostics / Repair  →  Infrastructure  →  UI
```

- **Common** — shared models and enums (no dependencies)
- **Core** — service interfaces
- **Database** — EF Core + SQLite scan history
- **Services** — system info, disk health/SMART, event log analysis, settings, reports
- **Diagnostics** — the 8 diagnostic modules
- **Repair** — repair definitions and execution
- **Infrastructure** — dependency injection wiring, navigation
- **UI** — WPF, MVVM (CommunityToolkit.Mvvm), dark theme

Logging via Serilog; DI via `Microsoft.Extensions.DependencyInjection` / `IHost`.

## License

MIT — see [LICENSE](LICENSE).
