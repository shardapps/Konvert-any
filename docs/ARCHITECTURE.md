# Architecture

Konvert 0.2 separates the persistent shortcut from the transient conversion UI.

```mermaid
flowchart LR
    A[Explorer / desktop foreground event] --> B[Native C++ listener]
    B -->|Ctrl+K: query selected file| C[Windows Shell COM]
    C --> D[Transient WPF radial popup]
    E[Explorer command / file arguments / chooser] --> D
    D --> F[Format registry and converter availability]
    F --> G[One heavy conversion per process]
    G --> H[STA native worker / async engine process]
    H --> I[Staged output]
    I --> J[Atomic no-overwrite move]
```

## Native listener

KonvertHost.exe is C++20/Win32, statically linked with the C runtime. It registers Ctrl+K only for Explorer/desktop foreground windows. SetWinEventHook delivers foreground events without polling; RegisterHotKey receives the shortcut without a low-level keyboard recorder. Windows Shell COM provides one selected filesystem item and its view position. Empty Explorer selections never fall back to unrelated desktop selections.

The listener sleeps in GetMessage when idle. It exposes tray open/settings/pause/quit controls, uses a named mutex to avoid duplicates, bounds concurrent popup launching, and waits for process exit when asked to stop during setup. It records no clipboard contents, keyboard stream or selection history.

## UI and themes

The managed application is .NET 10/WPF with software rendering. A file argument opens the frameless, per-pixel transparent radial popup; no file opens home/onboarding. Popup placement is monitor/work-area aware and clamped. The managed process exits after its windows close, while the separately controlled native listener remains.

ThemeManager supplies seven color systems and system high contrast fallback. App.xaml contains original templates for buttons, sliders, switches, text boxes and scrollbars. IconControl renders original 24-unit vector paths. Settings previews themes and commits only on Save; cancellation restores the original theme. No screen-capture/refraction shader or permanent animation timer is used.

## Conversion safety

FormatRegistry distinguishes supported routes from routes requiring engine setup. ConversionService executes only available routes. A semaphore bounds one heavy job per managed process. Native work uses a short-lived STA worker; engines use argument lists, concurrent bounded diagnostic draining, deadlines and process-tree cancellation.

Output is staged beside the destination, then moved without overwrite with numbered collision suffixes. The original is never written. Office exports use an isolated profile with macro security at its highest level. Text streams in bounded chunks; CSV/TSV rows are yielded; image dimensions and table input have explicit limits. [Format fidelity](FORMATS.md).

## Adding a format

Add an accurate route, engine/capability detection, cancellation/cleanup handling, real output tests and fidelity documentation. Update README, project facts and changelog. Preserve per-process concurrency bounds and avoid idle work.
