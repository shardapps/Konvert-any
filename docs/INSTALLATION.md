# Installation and removal

Konvert 0.2 alpha targets Windows 11. Choose x64 for Intel/AMD or ARM64 for Windows on Arm. Both setup EXEs include their architecture's .NET Desktop Runtime and native listener. ARM64 is cross-built; physical hardware verification remains pending. GitHub previews are unsigned.

## Setup EXE

Run the matching setup from Releases or CI. The default per-user directory is **%LOCALAPPDATA%\\Programs\\Shardapps\\Konvert**, following Screen's Shardapps folder convention. Setup creates a Start menu shortcut, registers uninstall in Windows Settings → Apps, and offers Explorer/PATH integration, startup at sign-in and a desktop shortcut.

The native listener starts after installation. Select one file in Explorer or on the desktop and press Ctrl+K. PATH alone cannot provide a global shortcut: the listener must be running. It registers Ctrl+K only while Explorer/desktop is active and yields it to other applications. The popup opens around the selected item, clamped to the monitor work area. Escape, the dismiss button and clicking outside close an idle popup.

Windows 11 shows the installer/portable Explorer command under **Show more options**. A modern packaged Explorer extension remains future work. Open a new terminal after PATH changes. The installer prepends its own directory, preserving unrelated PATH entries and removing only a previous entry Konvert recorded owning.

Run setup again for upgrade/repair. Close any visible conversion/settings windows first; setup stops the native listener before replacing files. Converted outputs and local preferences are preserved.

## Optional engines

Native images, text, tables and DOCX text work without additional engines. For DOCX-to-PDF and other office exports, install [LibreOffice](https://www.libreoffice.org/download/download-libreoffice/). For audio/video, use a trusted [FFmpeg build](https://ffmpeg.org/download.html).

Settings → Converters shows availability, download links and executable path pickers. Missing-engine popup options open that page. Set an explicit path to soffice.exe or ffmpeg.exe, save, then reopen the popup. Program Files and absolute PATH directories are also detected. The engines are not included in setup and have separate licenses.

## Portable ZIP

Extract the entire self-contained ZIP and run Konvert.exe. Keep KonvertHost.exe and licenses/ beside it. It starts the shortcut listener when you open the UI. Register Explorer/PATH in Settings → Windows only after placing the folder permanently. Optional sign-in startup is available there as well.

## Uninstall

Use Windows Settings → Apps → Konvert → Uninstall. Setup stops its listener and removes its files, shortcuts, startup registration and owned integration. It does not delete converted files or preferences. A sandbox/different-copy uninstall does not remove another copy's recorded Explorer/PATH integration.

For a portable copy, stop the listener from its tray menu, remove Explorer/PATH and sign-in startup in Settings, then remove the portable folder. Preferences may be removed separately from %LOCALAPPDATA%\\Shardapps\\Konvert if wanted.

## Microsoft Store

Konvert is not published to Microsoft Store yet. Unsigned development MSIX packages are for packaging validation, not ordinary installation. Partner Center identity, Store startup registration, modern packaged Explorer integration, certification and signing remain release work. [Store checklist](STORE.md).
