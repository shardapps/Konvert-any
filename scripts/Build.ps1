param(
    [ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64',
    [switch]$SelfContained,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    & $Dotnet build Konvert.sln -c Release
    if ($LASTEXITCODE) { throw 'Build failed.' }
    & $Dotnet run --project tests/Konvert.Tests -c Release --no-build
    if ($LASTEXITCODE) { throw 'Tests failed.' }
    $kind = if ($SelfContained) { 'standalone' } else { 'compact' }
    $output = Join-Path $root "artifacts/$Runtime-$kind"
    & $Dotnet publish src/Konvert -c Release -r $Runtime --self-contained $SelfContained.IsPresent -o $output -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE) { throw 'Publish failed.' }
    Copy-Item LICENSE,NOTICE,PRIVACY.md,THIRD_PARTY_NOTICES.md -Destination $output
    Compress-Archive -Path "$output/*" -DestinationPath "$output.zip" -Force
    Get-FileHash "$output.zip" -Algorithm SHA256 | Format-List
} finally { Pop-Location }
