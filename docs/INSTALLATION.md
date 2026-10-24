# Installation and removal

The first alpha targets Windows 11. Choose x64 for Intel/AMD Windows or ARM64 for Windows on Arm. ARM64 is cross-built; physical hardware validation remains pending.

## Portable

Download a successful CI ZIP (GitHub sign-in needed for workflow artifacts) or a published release, when available. Extract the entire ZIP. Compact builds require the matching **.NET 10 Desktop Runtime**, not only the base .NET Runtime. Standalone builds include the runtime and are larger. Open `Konvert.exe`.

To add Explorer/PATH integration, place the extracted folder permanently first. Select **Settings → Enable Explorer + PATH**. This registers only the current user's `Software\Classes\*\shell\Shardapps.Konvert` command and adds the app directory to user PATH if absent. No administrator rights or default file association change are needed. Windows 11 shows the portable entry under **Show more options**. Open a new terminal to pick up PATH. Only one selected file is supported.

For removal, select **Settings → Remove integration**, close the app, then delete its portable folder. This removes Konvert's command and only the PATH entry Konvert recorded adding. Your converted files remain. Preferences may be deleted manually from `%LOCALAPPDATA%\Shardapps\Konvert` if wanted. If you move the app, remove integration before moving and re-enable afterward.

## Optional engines

Download [LibreOffice](https://www.libreoffice.org/download/download-libreoffice/) for office exports and [FFmpeg](https://ffmpeg.org/download.html) for audio/video. Use trusted upstream-linked builds. Konvert does not install or bundle these engines. Select exact `soffice.exe` / `ffmpeg.exe` paths in Settings, or restart after adding them to PATH. [Formats and fidelity](FORMATS.md).

## MSIX and Microsoft Store

The repository includes unsigned development MSIX packaging. A development-identity MSIX is not a Store release and is not the recommended user install. No Konvert Store listing exists yet. Do not disable Windows protections to install an unsigned preview; use the portable ZIP for normal testing.

Packaged installs use the manifest's `konvert.exe` execution alias and Windows-managed installation lifecycle. Manual registry/PATH integration is disabled for packaged processes. A modern packaged Explorer command still requires an IExplorerCommand extension; it is planned and intentionally not misrepresented as working. [Store checklist](STORE.md).
