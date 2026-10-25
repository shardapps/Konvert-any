# Performance and resource budget

## 0.2 architecture

Version 0.2 keeps only the C++ native shortcut listener resident. The WPF UI is created on demand and released when its windows close. Measure these separately: the historical 0.1 visible-window numbers below are not the idle-listener footprint. `KonvertHost.exe` sleeps in the Windows message loop, registers Ctrl+K only for Explorer/desktop, and has no polling or animation timer. Quit it from the tray to stop it completely. Office/media engines run only during detection/conversion as described in the format reference.

The release validation records native listener idle samples, popup behavior and installer results. Large-file peak memory and physical ARM64 profiling remain ongoing; no universal performance guarantee is made.

The goal is quick, unobtrusive conversion. No universal startup, memory or conversion-time guarantee is made. Filesystem, antivirus, runtime cache, image dimensions and optional codecs change results.

## Design controls

- No Windows service, polling timer or permanent frame loop. Closing releases the managed conversion UI after cancellation/cleanup. The explicitly requested native listener remains available in the tray and can optionally start at sign-in.
- Conversion runs on a worker thread or asynchronous child process. A process-local semaphore permits one heavy job. Separate app processes can still overlap.
- Image metadata is checked before decode; 24 megapixels maximum. One normalized render surface avoids a full managed pixel-array copy. Native decoder/render allocations remain significant for large images.
- Streaming text uses an 8 KiB character buffer. CSV/TSV rows are yielded and written incrementally. Input text and JSON documents remain in memory, bounded to 16 MiB input, 100,000 rows and 1,000 columns; JSON structures can use several times their file size.
- FFmpeg requests two codec threads and two filter threads; codecs can allocate additional internal resources. LibreOffice is a larger optional process, with a fresh profile per conversion.
- Both engine pipes drain concurrently; retained diagnostics are capped at 64 KiB per stream. Native codec calls cannot be interrupted until returning; external processes have deadlines and cancellation.

## Reproduce idle measurement

```powershell
./scripts/Measure-Idle.ps1 -Executable ./artifacts/win-x64-compact/Konvert.exe
```

The script waits for GUI input readiness, then records five one-second working-set/private-byte and cumulative CPU samples. It closes the app afterward. Record Windows build, hardware, runtime version, cold/warm launch, engine availability and whether a file is loaded. Compare like-for-like. Child engines, GPU/compositor memory and system runtime sharing are not included in app working set.

## Historical 0.1 alpha observations · 2026-10-24

Windows 11 build 26200, Intel Core i7-14650HX, x64, .NET Desktop Runtime 10.0.13. Portable framework-dependent app copied to a local Windows temporary folder for measurement because the development workspace is on a streamed Google Drive volume. No file loaded; FFmpeg available; LibreOffice absent. Five one-second samples per launch. The current managed DLL was hash-checked against the measured preview.

| Mode | Working set | Private bytes | Input ready | Sample CPU increase |
| --- | --- | --- | --- | --- |
| Initial default WPF rendering | 147.15–147.18 MiB | 92.77–92.84 MiB | 1,900 ms | 0 ms |
| Default rendering, later warm run | 130.22–130.25 MiB | 86.17–86.25 MiB | 1,436 ms | 0 ms |
| Current software WPF rendering | 89.65–89.66 MiB | 50.16–50.20 MiB | 612 ms | 0 ms |

The app now uses software rendering for its small, mostly static picker, avoiding a dedicated GPU rendering pipeline. This is an observed memory reduction on one machine, not a general benchmark result. Startup cache conditions differ; do not infer a controlled startup speedup from these launches. Working set includes shared pages and does not equal total system RAM cost. Closing the measured process ended it; no resident Konvert process remained.

Raw records: [initial](benchmarks/2026-10-24-hardware-first.json), [warm default](benchmarks/2026-10-24-hardware-repeat.json), [verified software](benchmarks/2026-10-24-software-verified.json), [software repeat](benchmarks/2026-10-24-software-verified-repeat.json). Large-file peak memory, sustained media workloads, high DPI and physical ARM64 still need profiling. Never replace measurements with a marketing estimate.
