# Security policy

Konvert is pre-release. Security fixes target current `main`; no stable releases are supported yet. Keep Windows, .NET Desktop Runtime and optional engines updated.

Report privately using [GitHub vulnerability reporting](https://github.com/shardapps/Konvert-any/security/advisories/new). Include commit/version, Windows/runtime/engine versions, impact and minimal non-sensitive reproduction. Never include live credentials or private files. If reporting is unavailable, contact the repository owner through their GitHub profile before public disclosure.

Maintainers aim to acknowledge reports within seven days, on a best-effort basis. Coordinate remediation and disclosure before posting exploit details publicly.

## Boundaries

Konvert and its engines run with your user permissions. **This app is not a sandbox for hostile files.** Size limits reduce resource exhaustion; DOCX XML prohibits DTD/entity resolution. Staged no-overwrite output protects existing files. Engines use argument lists, capped diagnostic capture, cancellation and timeouts. LibreOffice gets a fresh profile with macros disabled; FFmpeg accepts local file/pipe protocols. These protections do not eliminate parser exploits or local file access.

No input uploads or telemetry are implemented. Conversion is not a complete metadata sanitizer. See [privacy](PRIVACY.md). Keep signing keys and Store credentials in protected environments, never in this repository. CI artifacts are unsigned previews.
