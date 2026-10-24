param(
    [Parameter(Mandatory)][string]$IdentityName,
    [Parameter(Mandatory)][string]$Publisher,
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version = '0.1.0.0',
    [ValidateSet('x64','arm64')][string]$Architecture = 'x64',
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$stage = Join-Path $root "artifacts/msix-stage-$Architecture"
New-Item -ItemType Directory -Force -Path $stage | Out-Null
& $Dotnet publish (Join-Path $root 'src/Konvert') -c Release -r "win-$Architecture" --self-contained true -o $stage -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE) { throw 'Publish failed.' }
Copy-Item (Join-Path $root 'packaging/Assets') $stage -Recurse -Force
foreach ($name in @('LICENSE','NOTICE','PRIVACY.md','THIRD_PARTY_NOTICES.md')) { Copy-Item (Join-Path $root $name) $stage }
$document = [xml](Get-Content (Join-Path $root 'packaging/AppxManifest.xml') -Raw)
$document.Package.Identity.Name = $IdentityName
$document.Package.Identity.Publisher = $Publisher
$document.Package.Identity.Version = $Version
$document.Package.Identity.ProcessorArchitecture = $Architecture
$document.Save((Join-Path $stage 'AppxManifest.xml'))
$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
$makeAppx = Get-ChildItem -LiteralPath $sdkRoot -Filter MakeAppx.exe -Recurse | Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
if (!$makeAppx) { throw 'Install the Windows SDK with MakeAppx.exe.' }
$package = Join-Path $root "artifacts/Konvert-$Version-$Architecture.msix"
& $makeAppx.FullName pack /d $stage /p $package /o
if ($LASTEXITCODE) { throw 'MSIX validation/packaging failed.' }
Write-Output "Unsigned MSIX: $package. Use Partner Center identity for submission; never ship the development identity."
