# Privacy

Konvert processes files locally. It has no accounts, conversion server, telemetry, advertising SDK, automatic update checker or analytics. It does not intentionally send input files, file names or results to Shardapps.

Preferences are JSON in `%LOCALAPPDATA%\Shardapps\Konvert\settings.json`: output folder, engine paths, JPEG quality, DPI and reduced motion. History is not persisted. Selected paths live in memory while the app is open. Outputs stay in your chosen folder or beside the source.

Staging files live in the destination folder and are removed after success, cancellation or handled failure. LibreOffice uses a unique workspace under Windows temporary storage, which Konvert attempts to remove. Crashes, power loss, forced termination or locked files can leave temporary files. Review temporary storage when handling sensitive material.

Optional engines run with your user permissions. LibreOffice exports use a fresh profile with macro security at its highest level. Engines, Windows, network-backed folders and documents with external references may have their own behavior. Konvert is not a sandbox or network isolation boundary. Use trusted engines and keep them updated.

Explorer/PATH integration is per user and removable in Settings. Version 0.2 adds the requested native shortcut listener, `KonvertHost.exe`. It uses foreground-window events and registers Ctrl+K only while Explorer or the desktop is active. It queries the selected file only when you invoke the shortcut; it does not record keystrokes, monitor the clipboard, scan folders or poll for selection changes. It has a tray icon with pause/quit controls. Closing the managed popup releases the conversion UI; the small native listener remains until quit. Startup at sign-in is controlled by the installer task and Windows settings page. No Windows service is installed. Removing portable files does not delete settings or converted outputs. [Removal instructions](docs/INSTALLATION.md).
