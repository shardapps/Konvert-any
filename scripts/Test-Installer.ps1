param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$target=[IO.Path]::GetFullPath((Join-Path $root 'artifacts/installer-test/Konvert'))
$workspace=[IO.Path]::GetFullPath($root).TrimEnd('\')+'\'
if (!$target.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase)) { throw 'Installer test target is outside the workspace.' }
$setup=(Resolve-Path -LiteralPath $Installer).Path
function Install-TestCopy {
    $process=Start-Process -FilePath $setup -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR="'+$target+'" /TASKS="!startup,!integration,!desktopicon"') -Wait -PassThru
    if ($process.ExitCode) { throw "Setup failed: $($process.ExitCode)" }
    foreach ($name in @('Konvert.exe','KonvertHost.exe','hostfxr.dll','unins000.exe','licenses')) {
        if (!(Test-Path -LiteralPath (Join-Path $target $name))) { throw "Installed payload missing $name" }
    }
}
if (Test-Path -LiteralPath $target) { throw 'Use a clean installer test directory; refusing to overwrite an unknown previous install.' }
$originalPath=[Environment]::GetEnvironmentVariable('PATH','User')
$originalStartup=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Shardapps.Konvert' -ErrorAction SilentlyContinue).'Shardapps.Konvert'
try {
    Install-TestCopy
    Install-TestCopy # Upgrade/repair the same per-user installation.
    $test=Start-Process (Join-Path $target 'KonvertHost.exe') -ArgumentList '--self-test' -Wait -PassThru
    if ($test.ExitCode) { throw 'Installed native listener self-test failed.' }
    Write-Output 'PASS per-user install, complete runtime, repair/upgrade and native listener'
} finally {
    $uninstall=Join-Path $target 'unins000.exe'
    if (Test-Path -LiteralPath $uninstall) {
        $process=Start-Process $uninstall -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
        if ($process.ExitCode) { throw 'Uninstall failed.' }
    }
}
if (Test-Path -LiteralPath (Join-Path $target 'Konvert.exe')) { throw 'Uninstall left the application executable.' }
if ([Environment]::GetEnvironmentVariable('PATH','User') -ne $originalPath) { throw 'Sandbox install changed the user PATH.' }
$startup=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Shardapps.Konvert' -ErrorAction SilentlyContinue).'Shardapps.Konvert'
if ($startup -ne $originalStartup) { throw 'Sandbox install changed existing startup settings.' }
Write-Output 'PASS uninstall and preservation of user settings'
