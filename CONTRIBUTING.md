# Contributing

Start with a small issue describing the user problem. Bug fixes, accessibility, accurate format documentation and real-world test cases are welcome. Use Windows 11 and .NET 10 SDK; see [development](docs/DEVELOPMENT.md).

## Pull requests

1. Branch from `main`; keep one concern per PR.
2. Explain the problem, resulting behavior, validation and fidelity limits.
3. Add regression coverage for conversion, security, cancellation or output safety changes. Prefer tiny generated fixtures with no private or proprietary content.
4. Update README, format matrix, project facts and changelog when behavior changes. Distinguish current features from plans.
5. Pass CI, engine integration, package validation and CodeQL. Never push application changes directly to `main` or bypass checks.

Run `dotnet build Konvert.sln -c Release`, `dotnet run --project tests/Konvert.Tests -c Release`, and `dotnet format Konvert.sln --verify-no-changes`. The tests are a dependency-free executable suite; `dotnet test` is not its entry point. Maintainers squash merge after reviewing the current PR head and checks.

## Engineering expectations

- Keep heavy work away from WPF's dispatcher. Bound concurrency and retained buffers. No idle polling or background service without a design discussion.
- Preserve originals and existing files. Stage new output, then commit without overwrite.
- Use process argument lists. Validate optional engines, support cancellation, clean temporary files, and document data loss and limits.
- Keep keyboard focus, screen reader labels, readable contrast, DPI scaling and reduced motion in mind.
- Justify new dependencies and check their licenses. Do not accidentally bundle optional engines.
- Publish measured performance with conditions; no unsupported “instant,” “fastest” or memory claims.

Contributions use the existing Apache-2.0 license. You must have the right to submit code and fixtures. Do not upload secrets, private documents or unlicensed media. Follow the [code of conduct](CODE_OF_CONDUCT.md); report vulnerabilities privately using [SECURITY.md](SECURITY.md).
