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

Build outputs go to ignored `artifacts/`. Setup uses complete self-contained payloads with the runtime, listener and notices. The installer builder checks PE architecture for the apphost, listener, hostfxr and CoreCLR before compiling either x64 or ARM64 setup. CI exercises x64 install, repair and removal in an isolated workspace directory; ARM64 is cross-built and requires physical-device validation. Compact ZIPs require .NET 10 Desktop Runtime; standalone ZIPs and MSIX include it. MSIX needs the Windows SDK's MakeAppx. Its packaging command requires an explicit publisher and identity; use Partner Center values for actual Store work. Packages are unsigned; there is no automatic Store deployment.

For a development Ctrl+K listener, build with `./scripts/Build-NativeHost.ps1 -Architecture x64 -Output src/Konvert/bin/Release/net10.0-windows` after the managed build. Use the finished package for installed-flow testing. The listener has `--self-test` and `--exit` commands; closing the conversion UI leaves the separately controlled listener active.

For a workspace on a streamed/cloud filesystem, Windows apphost resource updates may fail with a mapped-section error. To verify managed code locally, use `dotnet build Konvert.sln -c Release -p:UseAppHost=false` then `dotnet tests/Konvert.Tests/bin/Release/net10.0-windows/Konvert.Tests.dll`. Normal CI builds on a Windows runner disk produce the apphost. For interactive local development the managed app can run as `dotnet src/Konvert/bin/Release/net10.0-windows/Konvert.dll`; portable integration needs the published `Konvert.exe`.

## CI provenance

The invoked ci-cd-pipeline-builder skill's stack detector does not recognize `.csproj` files; it reports no language and its generated workflow has no executable .NET stages. We ran detection and baseline generation, then authored `ci.yml` from the real solution and commands. CI verifies managed/native builds, formatting, regression tests, dependency audit, real optional-engine exports, self-contained ZIPs, setup EXEs and unsigned MSIX for x64/ARM64. CodeQL separately scans C# and the C++ listener on PRs/main and weekly. No application package cache is needed while there are no third-party NuGet dependencies; the runner/SDK maintains platform reference caches. SHA-pinned actions receive Dependabot PRs.

Keep all changes on branches and PRs. Merge only when checks for the current head pass. No deployment secrets are required for CI. Store credentials/signing are deliberately absent; future deployment must use a protected environment with human release approval.
