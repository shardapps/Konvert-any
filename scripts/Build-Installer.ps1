param(
    [ValidateSet('x64','arm64')][string]$Architecture='x64',
    [string]$PayloadDirectory,
    [string]$Iscc,
    [string]$Dotnet='dotnet'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if (!$PayloadDirectory) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -Runtime "win-$Architecture" -SelfContained -Dotnet $Dotnet
    $PayloadDirectory=Join-Path $root "artifacts/win-$Architecture-standalone"
}
$PayloadDirectory=(Resolve-Path -LiteralPath $PayloadDirectory).Path
foreach ($required in @('Konvert.exe','Konvert.dll','KonvertHost.exe','hostfxr.dll','licenses')) {
    if (!(Test-Path -LiteralPath (Join-Path $PayloadDirectory $required))) { throw "Installer payload missing $required. Use a complete self-contained build." }
}
if (!$Iscc) {
    $candidates=@((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'),(Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'))
    $Iscc=$candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$Iscc) { throw 'Install Inno Setup 6 or pass -Iscc.' }
$output=Join-Path $root 'artifacts/installers'
New-Item -ItemType Directory -Force -Path $output | Out-Null
& $Iscc "/DPayloadDirectory=$PayloadDirectory" "/DTargetArchitecture=$Architecture" '/DAppVersion=0.2.0-alpha.1' "/O$output" (Join-Path $root 'packaging/Konvert.iss')
if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
Write-Output "Installer: $output/Konvert-Setup-0.2.0-alpha.1-$Architecture.exe"
