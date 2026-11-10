# Installation and removal

Konvert targets Windows 11. Choose x64 for Intel/AMD or ARM64 for Windows on Arm. The compact setup checks for the matching [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) and offers to open Microsoft's download page if needed. Physical ARM64 testing remains pending. GitHub previews are unsigned.

## Setup EXE

Run the matching setup from Releases or CI. The default per-user directory is **%LOCALAPPDATA%\\Programs\\Shardapps\\Konvert**, following Screen's Shardapps folder convention. Setup creates a Start menu shortcut, registers uninstall in Windows Settings → Apps, and offers Explorer/PATH integration, startup at sign-in and a desktop shortcut.

With Explorer integration or sign-in startup enabled, setup starts the native listener. Select one file in Explorer or on the desktop and press Ctrl+K. Pick a format to save next to the source. Open the chevron for more formats and output settings. Escape collapses the drawer or closes the circle. An outside click dismisses an idle popup.

Windows 11 shows the installer/portable Explorer command under **Show more options**. A modern packaged Explorer extension remains future work. Open a new terminal after PATH changes. The installer prepends its own directory, preserving unrelated PATH entries and removing only a previous entry Konvert recorded owning.

Uninstall 0.2 before moving from the standalone setup to the compact setup. The uninstall keeps your Konvert preferences and converted files. To repair or update a compact installation, close visible Konvert windows and run the matching setup again.

## Optional engines

Native images, text, tables and DOCX text work without additional engines. For DOCX-to-PDF and other office exports, install [LibreOffice](https://www.libreoffice.org/download/download-libreoffice/). For audio/video, use a trusted [FFmpeg build](https://ffmpeg.org/download.html).

Settings → Converters shows availability, download links and executable path pickers. Missing-engine popup options open that page. Set an explicit path to soffice.exe or ffmpeg.exe, save, then reopen the popup. Program Files and absolute PATH directories are also detected. The engines are not included in setup and have separate licenses.

## Portable ZIP

Extract the entire ZIP and run Konvert.exe. The compact ZIP uses the shared Desktop Runtime. For a self-contained build, use `scripts/Build.ps1 -SelfContained` and keep its license directory with the extracted files. Opening the UI starts the listener. Register Explorer/PATH in Settings → Windows after placing the folder permanently.

## Uninstall

Use Windows Settings → Apps → Konvert → Uninstall. Setup stops its listener and removes its files, shortcuts, startup registration and owned integration. It does not delete converted files or preferences. A sandbox/different-copy uninstall does not remove another copy's recorded Explorer/PATH integration.

For a portable copy, stop the listener from its tray menu, remove Explorer/PATH and sign-in startup in Settings, then remove the portable folder. Preferences may be removed separately from %LOCALAPPDATA%\\Shardapps\\Konvert if wanted.

## Microsoft Store

Konvert is not published to Microsoft Store yet. Unsigned development MSIX packages are for packaging validation, not ordinary installation. Partner Center identity, Store startup registration, modern packaged Explorer integration, certification and signing remain release work. [Store checklist](STORE.md).
