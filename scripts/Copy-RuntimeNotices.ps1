param(
    [Parameter(Mandatory)][string]$Destination,
    [Parameter(Mandatory)][ValidateSet('win-x64','win-arm64')][string]$Runtime,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../src/Konvert/Konvert.csproj'
# Query the exact runtime packs resolved by publish, rather than copying SDK-wide or latest-version notices.
$json = & $Dotnet msbuild $project -t:ResolveFrameworkReferences "-p:RuntimeIdentifier=$Runtime" -p:SelfContained=true -getItem:ResolvedRuntimePack -verbosity:quiet
if ($LASTEXITCODE) { throw 'Could not resolve runtime pack notices.' }
$packs = ($json | Out-String | ConvertFrom-Json).Items.ResolvedRuntimePack
if (@($packs).Count -lt 2) { throw 'Expected .NET and Windows Desktop runtime packs.' }
foreach ($pack in $packs) {
    $directory = $pack.PackageDirectory
    if (!$directory -or !(Test-Path -LiteralPath $directory)) { throw 'Resolved runtime pack directory is missing.' }
    $files = @(Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Name -match '^(LICENSE.*|THIRD.?PARTY.*NOTICES.*|NOTICE.*)$' })
    if (!($files | Where-Object { $_.Name -match '^LICENSE' })) { throw "Runtime license missing in $directory" }
    $name = if ($pack.NuGetPackageId) { $pack.NuGetPackageId } else { $pack.Identity }
    $target = Join-Path $Destination "licenses/$name"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach ($file in $files) { Copy-Item -LiteralPath $file.FullName -Destination $target -Force }
    Set-Content -LiteralPath (Join-Path $target 'package-version.txt') -Value (Split-Path $directory.TrimEnd([IO.Path]::DirectorySeparatorChar) -Leaf)
}
