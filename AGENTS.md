# Repository working rules

- Keep SDKs, tool downloads, scratch files and build outputs under ignored `.tools/` or `artifacts/`. Never commit credentials or runtime downloads.
- Use feature branches and PRs. Never push application changes directly to `main`. Merge only after the current PR head passes required CI, CodeQL, engine and package checks.
- No extra resident process, polling loop or unbounded conversion fan-out. Keep heavy work off WPF's dispatcher and measure memory honestly.
- Preserve original files and existing outputs, staged cleanup and subprocess argument lists.
- Update README, `docs/FORMATS.md`, `docs/PROJECT_FACTS.md`, changelog and tests with behavior changes. Separate implemented, optional, preview and planned features.
- Branding uses graphite and icy cyan. Distinguish promotional artwork from actual screenshots.
- Verify with `dotnet build Konvert.sln -c Release`, `dotnet run --project tests/Konvert.Tests -c Release`, and `dotnet format Konvert.sln --verify-no-changes`. Validate engines and MSIX when changing those paths.
- Store publication needs real Partner Center identity and release review. Do not invent Store IDs, contacts, benchmarks or supported conversions.
