param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$target=[IO.Path]::GetFullPath((Join-Path $root 'artifacts/installer-test/Konvert'))
$workspace=[IO.Path]::GetFullPath($root).TrimEnd('\')+'\'
if (!$target.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase)) { throw 'Installer test target is outside the workspace.' }
$setup=(Resolve-Path -LiteralPath $Installer).Path
function Install-TestCopy {
    # Wait only for setup, not its intentionally persistent listener descendant.
    $process=Start-Process -FilePath $setup -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR="'+$target+'" /TASKS="!startup,!integration,!desktopicon"') -PassThru
    if (!$process.WaitForExit(180000)) { throw 'Setup did not finish within three minutes.' }
    if ($process.ExitCode) { throw "Setup failed: $($process.ExitCode)" }
    foreach ($name in @('Konvert.exe','KonvertHost.exe','Konvert.runtimeconfig.json','unins000.exe')) {
        if (!(Test-Path -LiteralPath (Join-Path $target $name))) { throw "Installed payload missing $name" }
    }
    if (Test-Path -LiteralPath (Join-Path $target 'hostfxr.dll')) { throw 'Compact setup includes a private runtime.' }
}
if (Test-Path -LiteralPath $target) { throw 'Use a clean installer test directory; refusing to overwrite an unknown previous install.' }
$originalPath=[Environment]::GetEnvironmentVariable('PATH','User')
$originalStartup=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Shardapps.Konvert' -ErrorAction SilentlyContinue).'Shardapps.Konvert'
try {
    Install-TestCopy
    Install-TestCopy # Upgrade/repair the same per-user installation.
    $test=Start-Process (Join-Path $target 'KonvertHost.exe') -ArgumentList '--self-test' -Wait -PassThru
    if ($test.ExitCode) { throw 'Installed native listener self-test failed.' }
    $inputFile = Join-Path $target 'installer-fixture.txt'
    Set-Content -LiteralPath $inputFile -Value 'Installer conversion check'
    $conversion = Start-Process (Join-Path $target 'Konvert.exe') -ArgumentList ('--convert "' + $inputFile + '" --to html') -WindowStyle Hidden -PassThru
    if (!$conversion.WaitForExit(30000) -or $conversion.ExitCode) { throw 'Installed conversion failed.' }
    if ((Get-Content -LiteralPath ([IO.Path]::ChangeExtension($inputFile,'html')) -Raw) -notmatch 'Installer conversion check') { throw 'Installed conversion produced invalid HTML.' }
    Remove-Item -LiteralPath $inputFile,([IO.Path]::ChangeExtension($inputFile,'html'))
    Write-Output 'PASS compact install, repair, native listener and installed text conversion'
} finally {
    $uninstall=Join-Path $target 'unins000.exe'
    if (Test-Path -LiteralPath $uninstall) {
        $process=Start-Process $uninstall -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -PassThru
        if (!$process.WaitForExit(60000)) { throw 'Uninstall did not finish within one minute.' }
        if ($process.ExitCode) { throw 'Uninstall failed.' }
    }
}
if (Test-Path -LiteralPath (Join-Path $target 'Konvert.exe')) { throw 'Uninstall left the application executable.' }
if ([Environment]::GetEnvironmentVariable('PATH','User') -ne $originalPath) { throw 'Sandbox install changed the user PATH.' }
$startup=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Shardapps.Konvert' -ErrorAction SilentlyContinue).'Shardapps.Konvert'
if ($startup -ne $originalStartup) { throw 'Sandbox install changed existing startup settings.' }
Write-Output 'PASS uninstall and preservation of user settings'
