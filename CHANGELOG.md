# Changelog

## 0.2.0-alpha.1 — 2026-10-25

- Replace the large conversion screen with a frameless, file-centered radial popup; keep a separate home/onboarding screen.
- Add a C++ native Ctrl+K listener that reads the active Explorer/desktop selection on demand, yields the shortcut to other apps, and sleeps on Windows messages when idle.
- Add Glass by default plus six solid themes, live previews, original vector icons, custom sliders, switches, scrollbars and a settings sidebar.
- Show missing-engine routes with setup guidance instead of silently hiding DOCX-to-PDF and other optional conversions.
- Build self-contained x64 and ARM64 setup EXEs, install under the per-user Shardapps folder, and test install/repair/removal in CI.
- Preserve another Konvert copy's integration when uninstalling a sandbox copy; wait for the native listener to exit during upgrades.

Unsigned alpha; Microsoft Store publication and physical ARM64 testing remain separate release work.

## 0.1.0-alpha.1 — 2026-10-24

- Native .NET 10/WPF app with graphite/cyan orbital menu, file picking/drop, keyboard controls and reduced motion.
- Native image exports and image PDF, text/HTML exports, DOCX text extraction, CSV/TSV/JSON tables.
- Optional LibreOffice and FFmpeg detection for documents, spreadsheets, presentations, audio and video.
- Local settings, output folder, JPEG quality, image DPI, portable Explorer/PATH integration and command arguments.
- Background conversion, bounded concurrency, protected outputs, cancellation and cleanup.
- Regression/engine tests, Windows CI, x64/ARM64 packages, CodeQL and Store packaging groundwork.
- Open source policies, project facts, visual README and documented format limits.

Not released to Store. Production signing, batches, PDF input, WebP/HEIC export and modern packaged Explorer integration remain planned.
