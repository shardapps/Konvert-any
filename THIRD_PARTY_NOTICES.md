# Third-party notices

Konvert code is Apache-2.0. There are currently no third-party application NuGet dependencies.

.NET and WPF use upstream licenses including MIT and third-party notices. Compact builds require a separately installed Desktop Runtime. Self-contained builds include runtime license/notice files, which must be preserved. See [dotnet/runtime](https://github.com/dotnet/runtime) and [dotnet/wpf](https://github.com/dotnet/wpf).

For self-contained packages, `scripts/Copy-RuntimeNotices.ps1` queries the exact resolved runtime packs and copies their license/notice files and version markers into `licenses/`. Packaging fails if the runtime packs or their licenses cannot be found.

LibreOffice and FFmpeg are optional, user-installed engines, **not bundled** in portable/MSIX payloads. Their licenses apply independently. LibreOffice is primarily MPL-2.0; FFmpeg licensing varies with enabled components and may include LGPL/GPL terms. Check the actual distribution before redistributing an engine. Apache-2.0 does not relicense these products.

The README banner is AI-generated brand artwork. [Prompt and provenance](docs/assets/README.md). Simple K icons come from `scripts/New-BrandAssets.ps1`. Marketing artwork is excluded from application payloads.
