# Konvert project facts

Updated 2026-10-25 for 0.2.0-alpha.1. Canonical repository: [shardapps/Konvert-any](https://github.com/shardapps/Konvert-any).

| Field | Fact |
| --- | --- |
| Product | Konvert, by Shardapps |
| Category | Local Windows file converter |
| Platform | Windows 11; x64 and native ARM64 builds; physical ARM64 verification pending |
| Interaction | Select one Explorer/desktop file, press Ctrl+K, choose a destination in a radial popup |
| Persistent component | Small C++20 event-driven shortcut listener with tray pause/quit |
| Transient component | .NET 10/WPF conversion UI; no embedded browser |
| Themes | Glass default; Pearl, Graphite, Midnight, Ocean, Lavender, Forest |
| Installers | Per-user self-contained setup EXEs for x64 and ARM64 |
| Default install directory | %LOCALAPPDATA%\\Programs\\Shardapps\\Konvert |
| Native conversions | Images/image PDF, raw text/HTML, DOCX text, CSV/TSV/flat JSON |
| Optional conversions | LibreOffice office exports and FFmpeg media exports |
| Output | Beside original or configured directory; no overwrite |
| Privacy | Local processing; no app telemetry, accounts or upload backend |
| License | Apache-2.0 |
| Status | Unsigned alpha; no Konvert Microsoft Store listing yet |

Short description: **Konvert is an open source Windows file converter with a Ctrl+K radial popup, seven themes, local processing, safe output naming and optional LibreOffice/FFmpeg integrations.**

Do not describe it as a universal lossless converter, OCR/PDF editor, certified Store app, or an app with no resident process. The listener is persistent while enabled; the UI is transient. [Format limits](FORMATS.md) and [performance methodology](PERFORMANCE.md) control performance/fidelity claims.
