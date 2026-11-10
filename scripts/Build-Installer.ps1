param(
    [ValidateSet('x64','arm64')][string]$Architecture='x64',
    [string]$PayloadDirectory,
    [string]$Iscc,
    [string]$Dotnet='dotnet'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if (!$PayloadDirectory) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -Runtime "win-$Architecture" -Dotnet $Dotnet
    $PayloadDirectory=Join-Path $root "artifacts/win-$Architecture-compact"
}
$PayloadDirectory=(Resolve-Path -LiteralPath $PayloadDirectory).Path
foreach ($required in @('Konvert.exe','Konvert.dll','KonvertHost.exe','Konvert.runtimeconfig.json','Konvert.deps.json','LICENSE')) {
    if (!(Test-Path -LiteralPath (Join-Path $PayloadDirectory $required))) { throw "Installer payload missing $required." }
}
$runtime = (Get-Content (Join-Path $PayloadDirectory 'Konvert.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions.frameworks |
    Where-Object name -eq 'Microsoft.WindowsDesktop.App' | Select-Object -First 1
if (!$runtime -or (Test-Path -LiteralPath (Join-Path $PayloadDirectory 'hostfxr.dll'))) { throw 'Setup requires a framework-dependent Windows Desktop payload.' }
$runtimeVersion = ([version]$runtime.version).ToString(2)
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$fileVersion = ([version]($version.Split('-')[0])).ToString(3) + '.0'
$expectedMachine=if($Architecture -eq 'arm64'){0xAA64}else{0x8664}
foreach ($name in @('Konvert.exe','KonvertHost.exe')) {
    $stream=[IO.File]::OpenRead((Join-Path $PayloadDirectory $name))
    $reader=[IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw "Invalid PE file: $name" }
        $stream.Position=0x3C; $header=$reader.ReadInt32(); $stream.Position=$header
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne $expectedMachine) { throw "Wrong $Architecture payload architecture: $name" }
    } finally { $reader.Dispose(); $stream.Dispose() }
}
if (!$Iscc) {
    $candidates=@((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'),(Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'))
    $Iscc=$candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$Iscc) { throw 'Install Inno Setup 6 or pass -Iscc.' }
$output=Join-Path $root 'artifacts/installers'
New-Item -ItemType Directory -Force -Path $output | Out-Null
& $Iscc "/DPayloadDirectory=$PayloadDirectory" "/DTargetArchitecture=$Architecture" "/DAppVersion=$version" "/DFileVersion=$fileVersion" "/DRuntimeVersion=$runtimeVersion" "/O$output" (Join-Path $root 'packaging/Konvert.iss')
if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
Write-Output "Installer: $output/Konvert-Setup-$version-$Architecture.exe"
