<div align="center">

![Konvert by Shardapps — a file changing into new formats](docs/assets/brand-banner.png)

# Konvert

**Select a file. Press Ctrl+K. Choose its next form.**

[![Windows CI](https://github.com/shardapps/Konvert-any/actions/workflows/ci.yml/badge.svg)](https://github.com/shardapps/Konvert-any/actions/workflows/ci.yml)
[![CodeQL](https://github.com/shardapps/Konvert-any/actions/workflows/codeql.yml/badge.svg)](https://github.com/shardapps/Konvert-any/actions/workflows/codeql.yml)
[![Windows 11](https://img.shields.io/badge/Windows-11-a9e8fa?labelColor=1c2835)](docs/INSTALLATION.md)
[![Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-a9e8fa?labelColor=1c2835)](LICENSE)

[Downloads](https://github.com/shardapps/Konvert-any/releases) · [Formats](docs/FORMATS.md) · [Design](docs/DESIGN.md) · [Contribute](CONTRIBUTING.md) · [Report a bug](https://github.com/shardapps/Konvert-any/issues/new/choose)

</div>

Konvert is a free, open source **Windows file converter** by **Shardapps**. Select one file in Explorer or on the desktop and press **Ctrl+K**. A 224 DIP circle opens below the selection when there’s room, with up to three labelled formats. Pick one to save beside the original at standard quality. Open **Options** for the full format list, a folder picker, image size, JPEG quality, and DPI.

**0.3.3 alpha** adds a clearer Glass lens, an image size control, and more image, document, table, and media formats. The first-launch picker and file popup both dismiss on an outside click. Active exports finish in the background. Compact **x64 and ARM64 setup EXEs** use the shared Microsoft .NET 10 Desktop Runtime.

## Stay in your flow

| Select | Open | Convert |
| --- | --- | --- |
| Choose one filesystem file in Explorer or on the desktop. | Press **Ctrl+K**, use the Explorer **Konvert** command, or pass a file path. | Choose a format. Existing files and the original stay untouched. |

The small native listener registers Ctrl+K **only while Explorer or the desktop is active**, so browsers and editors keep their own shortcut. It uses Windows foreground events and queries selection only when invoked. The managed UI exits after the popup closes and any background export finishes. Pause or quit the listener from its tray menu.

## A quieter surface

**Glass** is the default. The desktop shows through an icy blue lens with a lit rim. Solid controls keep format labels readable. Six additional themes are **Pearl, Graphite, Midnight, Ocean, Lavender and Forest**. Settings includes live previews and controls for reducing transparency or motion.

The design draws on Apple’s floating material layers, Google’s control hierarchy, and Shardapps’ compact utilities. [Design decisions and references](docs/DESIGN.md).

![Konvert’s first-launch picker, quick conversion ring, and expanded image options rendered over a generated background](docs/assets/clear-glass-picker.png)

## Conversions that tell you what they need

| Family | Examples | Engine |
| --- | --- | --- |
| Images | PNG ↔ JPG/BMP/TIFF/GIF; image → ICO or PDF | Built in |
| Modern images | HEIC/HEIF/AVIF/WebP input; PNG/JPG and more | Available Windows image codecs |
| WebP | PNG/JPG and other still images → lossless WebP; WebP → PNG | Local FFmpeg |
| Text | Raw text → UTF-8 TXT or escaped HTML text page | Built in |
| Tables | CSV ↔ TSV; CSV/TSV → JSON; flat JSON → CSV or TSV | Built in |
| Document text | DOCX/ODT → TXT | Built in |
| Documents | DOCX, DOC, ODT, RTF → PDF and office formats | Local LibreOffice |
| Spreadsheets | XLSX, XLS, ODS, CSV/TSV → PDF and spreadsheet formats | Local LibreOffice |
| Presentations | PPTX, PPT, ODP → PDF and presentation formats | Local LibreOffice |
| Audio/video | Video → MOV/AVI/MP4/WebM/MKV/TS/GIF; audio → AAC/AIFF/CAF/MP3 and more | Local FFmpeg |

Native routes work without LibreOffice or FFmpeg. LibreOffice is optional for layout-aware Office exports such as DOCX → PDF. Konvert probes the Windows decoder for modern images and checks FFmpeg encoders and containers before offering media exports. It uses FFprobe when present to inspect the source tracks.

The format matrix documents loss from re-encoding, Office layout differences, first-frame image exports, and flat-table restrictions. [Format matrix](docs/FORMATS.md).

## Install

1. Pick **x64** for Intel/AMD Windows or **ARM64** for Windows on Arm. Find packages in [Releases](https://github.com/shardapps/Konvert-any/releases) or successful [CI artifacts](https://github.com/shardapps/Konvert-any/actions/workflows/ci.yml).
2. Run Setup in the existing Konvert folder to upgrade. A fresh per-user install defaults to **%LOCALAPPDATA%\\Programs\\Shardapps\\Konvert**. Setup verifies the matching [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) before it changes the installation.
3. Choose Explorer/PATH integration and startup preferences in setup. Ctrl+K is ready after the listener starts; open a **new terminal** to pick up PATH changes.
4. Select one file in Explorer and press **Ctrl+K**. For office/media exports, open **Settings → Converters** and configure LibreOffice/FFmpeg.

Windows 11 is the support target. The ARM64 setup contains a native ARM64 app and listener. Physical ARM64 testing remains pending. Windows 11 shows the Konvert context entry under **Show more options**. MSIX is a separate, unsigned Store-preparation artifact. [Install, upgrade and uninstall details](docs/INSTALLATION.md).

## Local, bounded work

No accounts, uploads, ads or telemetry. Preferences remain local. Original and existing files are protected by staged outputs and no-overwrite commits. Heavy conversion runs away from the UI thread; only one heavy job runs per process. Text/diagnostics use bounded buffers, and native image/table sizes have explicit limits.

The resident component is a small **C++20 Win32 listener**. The **.NET 10/WPF UI** opens on demand. The compact installer contains the app and listener. The separate MSIX build includes its runtime. [Measurements and methodology](docs/PERFORMANCE.md).

## From a terminal

```powershell
# Open the radial popup
Konvert.exe "C:\\Files\\report.docx"

# Wait for a headless conversion
$job = Start-Process Konvert.exe -ArgumentList '--convert "C:\\Files\\photo.png" --to jpg --output "C:\\Exports"' -Wait -PassThru
$job.ExitCode  # 0 = success, 1 = failure

Konvert.exe --settings
```

## Build and contribute

```powershell
dotnet build Konvert.sln -c Release
dotnet run --project tests/Konvert.Tests -c Release
./scripts/Build-NativeHost.ps1 -Architecture x64
./scripts/Build-Installer.ps1 -Architecture x64
```

Building the listener requires Visual Studio C++ desktop tools and the Windows SDK. Building setup requires Inno Setup 6. CI compiles both architectures, tests converters, verifies setup/repair/removal and scans C# plus C++ with CodeQL. All changes go through PRs.

[Contributing](CONTRIBUTING.md) · [Architecture](docs/ARCHITECTURE.md) · [Development](docs/DEVELOPMENT.md) · [Roadmap](docs/ROADMAP.md) · [Project facts](docs/PROJECT_FACTS.md)

---

Made by **[Shardapps](https://github.com/shardapps)**, the product brand behind utilities published as **ShardCoders**. See [Screen Dimmer](https://github.com/shardapps/screen) and [Clippboard](https://github.com/shardapps/clippboard).

[Apache-2.0](LICENSE) · [Security](SECURITY.md) · [Privacy](PRIVACY.md) · [Code of conduct](CODE_OF_CONDUCT.md) · [Changelog](CHANGELOG.md) · [Third-party notices](THIRD_PARTY_NOTICES.md)

The banner is generated brand artwork, not a screenshot. [Asset provenance](docs/assets/README.md).
