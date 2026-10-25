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

Konvert is a free, open source **Windows file converter** by **Shardapps**. Select one file in Explorer or on the desktop and press **Ctrl+K**. A compact radial popup appears around the selection, with the source at its center and destination formats around it. Choose a format to create a separate converted file beside the original, or in your preferred folder.

**0.2 alpha:** native shortcut listener, file-centered popup, seven themes and conventional **x64 / ARM64 setup EXEs**. GitHub previews are unsigned. Microsoft Store publication and physical ARM64 validation remain separate release work.

## Stay in your flow

| Select | Open | Convert |
| --- | --- | --- |
| Choose one filesystem file in Explorer or on the desktop. | Press **Ctrl+K**, use the Explorer **Konvert** command, or pass a file path. | Choose a format. Existing files and the original stay untouched. |

The small native listener registers Ctrl+K **only while Explorer or the desktop is active**, so browsers and editors keep their own shortcut. It uses Windows foreground events and queries selection only when invoked. No keyboard recording, clipboard capture, selection polling or Windows service. Closing the popup releases the managed conversion UI; pause or quit the listener from its tray menu.

## A quieter surface

**Glass** is the default: translucent floating controls, subtle edge highlights and short transitions. Six additional themes are **Pearl, Graphite, Midnight, Ocean, Lavender and Forest**. Settings includes live previews, original vector icons, custom sliders and switches, output controls, converter setup and Windows integration.

The design takes principles from Apple's floating material layers and Material's color/motion hierarchy, with Shardapps' small utility interaction as the product reference. It is an original Windows implementation, not Apple's Liquid Glass renderer. [Design decisions and references](docs/DESIGN.md).

## Conversions that tell you what they need

| Family | Examples | Engine |
| --- | --- | --- |
| Images | PNG ↔ JPG/BMP/TIFF/GIF; image → PDF | Native Windows codecs |
| Text | Raw text → UTF-8 TXT or escaped HTML text page | Built in |
| Tables | CSV ↔ TSV; CSV/TSV → JSON; flat JSON → CSV | Built in |
| Document text | DOCX → TXT | Built in |
| Documents | DOCX, DOC, ODT, RTF → PDF and office formats | Local LibreOffice |
| Spreadsheets | XLSX, XLS, ODS, CSV/TSV → PDF and spreadsheet formats | Local LibreOffice |
| Presentations | PPTX, PPT, ODP → PDF and presentation formats | Local LibreOffice |
| Audio/video | MP4 → MP3, WAV → FLAC, video → MP4/WebM/GIF and more | Local FFmpeg |

Native routes work immediately. Office and media engines are separate installations to keep the base application smaller. Missing-engine routes stay visible with **Add engine**, which opens setup guidance; they are not presented as working conversions. FFmpeg output encoders are probed before use.

Formats are not interchangeable without consequences. Lossy codecs, office layout/fonts, first-frame image exports and flat-table restrictions are documented in the [exact format matrix](docs/FORMATS.md). PDF input/OCR, WebP/HEIC exports and batches are not implemented yet. No “anything to anything without loss” claim is made.

## Install

1. Get **Konvert-Setup-0.2.0-alpha.1-x64.exe** for Intel/AMD Windows, or **Konvert-Setup-0.2.0-alpha.1-arm64.exe** for Windows on Arm from [Releases](https://github.com/shardapps/Konvert-any/releases). PR previews are also available in successful [CI artifacts](https://github.com/shardapps/Konvert-any/actions/workflows/ci.yml).
2. Setup installs for the current user under **%LOCALAPPDATA%\\Programs\\Shardapps\\Konvert** and includes the .NET Desktop Runtime. No separate runtime installation is required.
3. Choose Explorer/PATH integration and startup preferences in setup. Ctrl+K is ready after the listener starts; open a **new terminal** to pick up PATH changes.
4. Select one file in Explorer and press **Ctrl+K**. For office/media exports, open **Settings → Converters** and configure LibreOffice/FFmpeg.

Windows 11 is the support target. ARM64 packages contain native ARM64 app/runtime/listener binaries, but are cross-built and still need physical-device testing. Windows 11's portable/installer context entry appears under **Show more options**. MSIX is a separate, unsigned Store-preparation artifact. [Install, upgrade and uninstall details](docs/INSTALLATION.md).

## Local, bounded work

No accounts, uploads, ads or telemetry. Preferences remain local. Original and existing files are protected by staged outputs and no-overwrite commits. Heavy conversion runs away from the UI thread; only one heavy job runs per process. Text/diagnostics use bounded buffers, and native image/table sizes have explicit limits.

The resident component is a small **C++20 Win32 listener**; the **.NET 10/WPF UI is transient**. No embedded browser or third-party application NuGet packages. Setup includes the runtime, while conversion engines remain separate. [Measurements and methodology](docs/PERFORMANCE.md) distinguish idle listener memory from visible UI and active conversion.

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
