# Konvert project facts

Updated 2026-10-24 for 0.1.0-alpha.1. Canonical source: [shardapps/Konvert-any](https://github.com/shardapps/Konvert-any).

| Field | Fact |
| --- | --- |
| Name | Konvert |
| Product owner | Shardapps |
| Category | Local Windows file converter / desktop utility |
| Platform | Windows 11, x64 and ARM64 builds; ARM64 hardware validation pending |
| Stack | C#, .NET 10, WPF; no embedded browser or third-party application NuGet packages |
| License | Apache-2.0 |
| Stage | 0.1 alpha; no Konvert Microsoft Store listing yet |
| Main interaction | Select/drop/pass a file, choose a format from a native radial menu |
| Native conversions | Windows image codecs to images/image PDF; raw text/HTML, DOCX text, CSV/TSV/flat JSON |
| Optional conversions | LibreOffice office exports; FFmpeg audio/video with encoder probing |
| Output | Beside source by default or configured directory; existing files preserved with suffixes |
| Privacy | Local processing, no app telemetry/accounts/upload backend; preferences stored locally |
| Background behavior | No resident service or tray process; close exits after cancelling active work |
| Distribution | Portable ZIP and unsigned MSIX build infrastructure; Store certification remains planned |

**Short description:** Konvert is an open source Windows file converter by Shardapps with a native radial format menu, local processing, safe output naming, and optional LibreOffice/FFmpeg integrations.

Do not describe it as a universal converter, PDF editor, OCR tool, certified Store app, lossless converter for every format, or guaranteed instant/low-RAM app. [Exact format limits](FORMATS.md) and [performance methodology](PERFORMANCE.md) control those claims. Keep this page, README, repository description and changelog aligned as features change.
