# Performance and resource budget

The goal is quick, unobtrusive conversion. No universal startup, memory or conversion-time guarantee is made. Filesystem, antivirus, runtime cache, image dimensions and optional codecs change results.

## Design controls

- No service, tray resident, startup registration, polling timer or permanent frame loop. Closing exits; a pending conversion is cancelled and cleaned first.
- Conversion runs on a worker thread or asynchronous child process. A process-local semaphore permits one heavy job. Separate app processes can still overlap.
- Image metadata is checked before decode; 24 megapixels maximum. One normalized render surface avoids a full managed pixel-array copy. Native decoder/render allocations remain significant for large images.
- Streaming text uses an 8 KiB character buffer. Tables are bounded to 16 MiB input, 100,000 rows and 1,000 columns, but parsed data/JSON can use several times the input size.
- FFmpeg requests two codec threads and two filter threads; codecs can allocate additional internal resources. LibreOffice is a larger optional process, with a fresh profile per conversion.
- Both engine pipes drain concurrently; retained diagnostics are capped at 64 KiB per stream. Native codec calls cannot be interrupted until returning; external processes have deadlines and cancellation.

## Reproduce idle measurement

```powershell
./scripts/Measure-Idle.ps1 -Executable ./artifacts/win-x64-compact/Konvert.exe
```

The script waits for GUI input readiness, then records five one-second working-set/private-byte and cumulative CPU samples. It closes the app afterward. Record Windows build, hardware, runtime version, cold/warm launch, engine availability and whether a file is loaded. Compare like-for-like. Child engines, GPU/compositor memory and system runtime sharing are not included in app working set.

Measured alpha results will be recorded below after the local runnable build is verified. Never replace measurements with a marketing estimate.
