# Development

Use Windows 11, Git and the .NET 10 SDK. The native listener also requires Visual Studio C++ desktop tools and the Windows SDK; setup EXEs require Inno Setup 6. `global.json` permits the latest installed .NET 10 feature band. No additional NuGet packages are required by the app or test harness.

```powershell
git clone https://github.com/shardapps/Konvert-any.git
cd Konvert-any
dotnet build Konvert.sln -c Release
dotnet run --project tests/Konvert.Tests -c Release
dotnet format Konvert.sln --verify-no-changes
dotnet run --project src/Konvert
```

Use `dotnet format Konvert.sln` to apply formatting. The executable regression harness intentionally avoids a test framework dependency. Run it explicitly; `dotnet test` is not the verification entry point. Fixtures are generated in a unique Windows temp directory and removed afterward.

## Optional integration tests

Install LibreOffice and FFmpeg separately. Their executables must be on PATH or in the standard LibreOffice Program Files location.

```powershell
dotnet run --project tests/Konvert.Tests -c Release -- --require-ffmpeg --require-office
```

The engine suite converts generated audio/video and decodes results using FFmpeg. LibreOffice tests generated DOCX documents, CSV-to-spreadsheet exports and ODP-to-presentation exports, including PDF and package structure checks. CI requires both engines in a separate integration job. Missing optional engines are explicitly skipped in normal local runs; required flags turn missing engines into failures.

## Packaging

```powershell
./scripts/Build.ps1 -Runtime win-x64
./scripts/Build.ps1 -Runtime win-arm64 -SelfContained
./scripts/Build-Installer.ps1 -Architecture x64
./scripts/Build-Installer.ps1 -Architecture arm64
./scripts/Build-Msix.ps1 -Architecture x64 -IdentityName Shardapps.Konvert.Development -Publisher CN=Shardapps.Development
```

Build outputs go to ignored `artifacts/`. Setup contains the compact app, listener and notices. The installer builder verifies the apphost and listener PE architecture for x64 or ARM64. Windows CI runs fresh install, repair, upgrade from a rebuilt 0.2 standalone installer, recovery from an orphaned legacy folder, and removal on a disposable x64 account. ARM64 is cross-built and requires physical-device validation. Compact installers and ZIPs use .NET 10 Desktop Runtime. Standalone ZIPs and MSIX include it. MSIX needs the Windows SDK's MakeAppx and an explicit publisher and identity. Use Partner Center values for Store work.

For a development Ctrl+K listener, build with `./scripts/Build-NativeHost.ps1 -Architecture x64 -Output src/Konvert/bin/Release/net10.0-windows` after the managed build. Use the finished package for installed-flow testing. The listener has `--self-test` and `--exit` commands; closing the conversion UI leaves the separately controlled listener active.

For a workspace on a streamed/cloud filesystem, Windows apphost resource updates may fail with a mapped-section error. To verify managed code locally, use `dotnet build Konvert.sln -c Release -p:UseAppHost=false` then `dotnet tests/Konvert.Tests/bin/Release/net10.0-windows/Konvert.Tests.dll`. Normal CI builds on a Windows runner disk produce the apphost. For interactive local development the managed app can run as `dotnet src/Konvert/bin/Release/net10.0-windows/Konvert.dll`; portable integration needs the published `Konvert.exe`.

## CI provenance

The CI/CD Pipeline Builder skill's detector has no `.csproj` support, so the Windows workflow uses the solution and checked-in build scripts as its stack inputs. The build, format, regression and engine jobs gate the x64 and ARM64 package matrix. The matrix publishes the app without repeating the already completed regression suite. The x64 installer check runs before artifact upload and retains setup logs on failure. The setup EXEs have separate download artifacts from the portable ZIP and MSIX bundles. CodeQL scans C# and the C++ listener. The app has no third-party NuGet dependencies or package lockfiles, and the workflow uses SHA-pinned actions.

Keep all changes on branches and PRs. Merge only when checks for the current head pass. No deployment secrets are required for CI. Store credentials/signing are deliberately absent; future deployment must use a protected environment with human release approval.
