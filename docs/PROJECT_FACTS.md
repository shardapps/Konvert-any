# Konvert project facts

Updated 2026-11-10 for 0.3.1-alpha.1. [shardapps/Konvert-any](https://github.com/shardapps/Konvert-any).

| Field | Fact |
| --- | --- |
| Product | Konvert, by Shardapps |
| Category | Local Windows file converter |
| Platform | Windows 11; x64 and native ARM64 builds; physical ARM64 verification pending |
| Interaction | Ctrl+K on one selected file opens a 224 DIP circle. A chevron reveals extra formats and per-conversion settings. |
| Persistent component | Small C++20 event-driven shortcut listener with tray pause/quit |
| Transient component | .NET 10/WPF conversion UI; no embedded browser |
| Themes | Glass default; Pearl, Graphite, Midnight, Ocean, Lavender, Forest |
| Installers | Compact x64 and ARM64 setup EXEs. Runtime preflight, in-place upgrades and exact-match cleanup of known legacy files. |
| Default install directory | %LOCALAPPDATA%\\Programs\\Shardapps\\Konvert |
| Native conversions | Images/image PDF, raw text/HTML, DOCX text, CSV/TSV/flat JSON |
| Optional conversions | LibreOffice office exports and FFmpeg media exports |
| Output | Beside original or configured directory; no overwrite |
| Privacy | Local processing; no app telemetry, accounts or upload backend |
| License | Apache-2.0 |
| Status | Unsigned alpha; no Konvert Microsoft Store listing yet |

Short description: **Konvert is an open source Windows file converter with a Ctrl+K radial popup, seven themes, local processing, safe output naming and optional LibreOffice/FFmpeg integrations.**

Do not describe it as a universal lossless converter, OCR/PDF editor, certified Store app, or an app with no resident process. The listener is persistent while enabled; the UI is transient. [Format limits](FORMATS.md) and [performance methodology](PERFORMANCE.md) control performance/fidelity claims.
