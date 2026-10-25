# Repository working rules

- Keep SDKs, tool downloads, scratch files and build outputs under ignored `.tools/` or `artifacts/`. Never commit credentials or runtime downloads.
- Use feature branches and PRs. Never push application changes directly to `main`. Merge only after the current PR head passes required CI, CodeQL, engine and package checks.
- Ctrl+K uses the user-requested native KonvertHost listener. Keep the managed UI transient, the listener event-driven, and all heavy work off WPF's dispatcher. No polling loop or unbounded conversion fan-out. Measure native idle memory separately from the visible UI.
- Preserve original files and existing outputs, staged cleanup and subprocess argument lists.
- Update README, `docs/FORMATS.md`, `docs/PROJECT_FACTS.md`, changelog and tests with behavior changes. Separate implemented, optional, preview and planned features.
- Default appearance is translucent Glass with six additional themes (Pearl, Graphite, Midnight, Ocean, Lavender, Forest). Use original vector icons and themed controls. Distinguish promotional artwork from actual screenshots.
- Verify with `dotnet build Konvert.sln -c Release`, `dotnet run --project tests/Konvert.Tests -c Release`, and `dotnet format Konvert.sln --verify-no-changes`. Validate engines and MSIX when changing those paths.
- Store publication needs real Partner Center identity and release review. Do not invent Store IDs, contacts, benchmarks or supported conversions.
