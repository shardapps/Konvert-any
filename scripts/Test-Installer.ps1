param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][string]$LegacyInstaller,
    [Parameter(Mandatory)][string]$LegacyArchive
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $root ('artifacts/installer-test/' + [Guid]::NewGuid().ToString('N'))
$setup = (Resolve-Path -LiteralPath $Installer).Path
$legacySetup = (Resolve-Path -LiteralPath $LegacyInstaller).Path
$legacyZip = (Resolve-Path -LiteralPath $LegacyArchive).Path
$registeredKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{FB2E8267-E891-4CE0-8F84-441FB22E54E5}_is1'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$commandKey = 'HKCU:\Software\Classes\*\shell\Shardapps.Konvert\command'
$trackingKey = 'HKCU:\Software\Shardapps\Konvert'
$settingsFile = Join-Path $env:LOCALAPPDATA 'Shardapps/Konvert/settings.json'
$noTasks = '!integration,!startup,!desktopicon'
if ((Test-Path -LiteralPath $registeredKey) -or (Test-Path -LiteralPath $commandKey) -or
    (Test-Path -LiteralPath $settingsFile) -or (Get-Process KonvertHost,Konvert -ErrorAction SilentlyContinue) -or
    (Get-ItemPropertyValue -LiteralPath $runKey -Name 'Shardapps.Konvert' -ErrorAction SilentlyContinue) -or
    (Get-ItemPropertyValue -LiteralPath $trackingKey -Name 'OwnedPath' -ErrorAction SilentlyContinue)) {
    throw 'Run installer lifecycle tests on a clean disposable Windows account.'
}
$originalPath = [string][Environment]::GetEnvironmentVariable('PATH', 'User')
New-Item -ItemType Directory -Path $scratch | Out-Null
New-Item -ItemType Directory -Path (Split-Path $settingsFile) -Force | Out-Null
Set-Content -LiteralPath $settingsFile -Value '{"Theme":"ocean","JpegQuality":93}'
$settingsHash = (Get-FileHash -LiteralPath $settingsFile).Hash

function Invoke-Process {
    param([string]$Executable, [string]$Arguments, [int]$Timeout = 60000)
    # Waiting on the setup process alone lets the shortcut listener continue after installation.
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit($Timeout)) {
        $process.Kill()
        throw "$(Split-Path $Executable -Leaf) timed out."
    }
    return $process.ExitCode
}

function Invoke-Setup {
    param(
        [string]$Executable = $setup,
        [string]$Target,
        [string]$Tasks,
        [string]$FailureText
    )
    $log = Join-Path $scratch ([Guid]::NewGuid().ToString('N') + '.log')
    $parameters = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG="' + $log + '"'
    if ($Target) { $parameters += ' /DIR="' + $Target + '"' }
    if ($Tasks) { $parameters += ' /TASKS="' + $Tasks + '"' }
    $exitCode = Invoke-Process $Executable $parameters 120000
    if ($FailureText) {
        if ($exitCode -ne 0 -and (Select-String -LiteralPath $log -SimpleMatch $FailureText -Quiet)) { return }
    } elseif ($exitCode -eq 0) { return }
    Get-Content -LiteralPath $log -Tail 45
    throw "Unexpected setup result $exitCode for '$Target'. Expected: $FailureText"
}

function Remove-TestInstall([string]$Target) {
    $uninstall = Join-Path $Target 'unins000.exe'
    if (Test-Path -LiteralPath $uninstall) {
        $code = Invoke-Process $uninstall '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
        if ($code) { throw "Uninstall failed with code $code." }
    }
}

function Test-CompactInstall([string]$Target) {
    $app = Join-Path $Target 'Konvert.App'
    foreach ($name in @('Konvert.exe','KonvertHost.exe','Konvert.runtimeconfig.json')) {
        if (!(Test-Path -LiteralPath (Join-Path $app $name))) { throw "Installed payload missing $name." }
    }
    if (Test-Path -LiteralPath (Join-Path $app 'hostfxr.dll')) { throw 'Compact payload contains a private runtime.' }
    if (Invoke-Process (Join-Path $app 'KonvertHost.exe') '--self-test') { throw 'Installed listener self-test failed.' }
    $inputFile = Join-Path $Target 'conversion fixture.txt'
    Set-Content -LiteralPath $inputFile -Value 'Installer conversion check'
    $result = [IO.Path]::ChangeExtension($inputFile, 'html')
    if (Invoke-Process (Join-Path $app 'Konvert.exe') ('--convert "' + $inputFile + '" --to html') 30000) {
        throw 'Installed conversion failed.'
    }
    if ((Get-Content -LiteralPath $result -Raw) -notmatch 'Installer conversion check') {
        throw 'Installed conversion produced invalid HTML.'
    }
    return $result
}

function Test-ExistingSettings {
    if ((Get-FileHash -LiteralPath $settingsFile).Hash -ne $settingsHash) {
        throw 'Setup modified existing conversion preferences.'
    }
}

function Test-NoIntegration {
    if ([string][Environment]::GetEnvironmentVariable('PATH', 'User') -ne $originalPath) {
        throw 'A disabled or removed installation left PATH changes.'
    }
    if ((Test-Path -LiteralPath $commandKey) -or
        (Get-ItemPropertyValue -LiteralPath $runKey -Name 'Shardapps.Konvert' -ErrorAction SilentlyContinue)) {
        throw 'A disabled or removed installation left Explorer or startup registration.'
    }
}

$fresh = Join-Path $scratch 'Fresh install'
$registered = Join-Path $scratch 'Registered legacy'
$orphan = Join-Path $scratch 'Orphaned legacy Ω'
$foreign = Join-Path $scratch 'Unrelated files'
try {
    Invoke-Setup -Target $fresh -Tasks $noTasks
    $converted = Test-CompactInstall $fresh
    Invoke-Setup -Target $fresh -Tasks $noTasks
    Remove-TestInstall $fresh
    if (!(Test-Path -LiteralPath $converted)) { throw 'Uninstall removed a converted file.' }
    Test-NoIntegration
    Test-ExistingSettings
    Write-Output 'PASS fresh compact install, conversion, repair and uninstall with files preserved.'

    New-Item -Path $foreign -ItemType Directory | Out-Null
    $document = Join-Path $foreign 'My notes.txt'
    Set-Content -LiteralPath $document -Value 'Keep these notes'
    Set-Content -LiteralPath (Join-Path $foreign 'hostfxr.dll') -Value 'Foreign fixture'
    Invoke-Setup -Target $foreign -Tasks $noTasks -FailureText 'This folder contains other files'
    if ((Get-Content -LiteralPath $document -Raw).Trim() -ne 'Keep these notes' -or
        (Test-Path -LiteralPath (Join-Path $foreign 'Konvert.App'))) { throw 'Setup changed an unrelated destination.' }
    Write-Output 'PASS unrelated folder rejected without altering its files.'

    Invoke-Setup -Executable $legacySetup -Target $registered -Tasks 'integration,startup,!desktopicon'
    $legacyApp = Join-Path $registered 'Konvert.dll'
    $legacyHash = (Get-FileHash -LiteralPath $legacyApp).Hash
    $legacyHost = Get-Process KonvertHost | Where-Object { $_.Path -eq (Join-Path $registered 'KonvertHost.exe') }
    if (!$legacyHost) { throw 'Legacy installer did not start its shortcut listener.' }

    # Point the child apphost at an existing but unusable runtime location.
    $invalidRuntime = Join-Path $scratch 'Invalid runtime'
    New-Item -Path $invalidRuntime -ItemType Directory | Out-Null
    $originalRuntime = $env:DOTNET_ROOT_X64
    try {
        $env:DOTNET_ROOT_X64 = $invalidRuntime
        Invoke-Setup -Target $registered -FailureText 'Konvert runtime probe failed'
    } finally {
        $env:DOTNET_ROOT_X64 = $originalRuntime
    }
    if ((Get-FileHash -LiteralPath $legacyApp).Hash -ne $legacyHash -or $legacyHost.HasExited) {
        throw 'A failed runtime prerequisite changed or stopped the previous install.'
    }
    Write-Output 'PASS unavailable runtime leaves the previous installation working.'

    $retained = Join-Path $registered 'My saved file.txt'
    Set-Content -LiteralPath $retained -Value 'Keep converted files'
    $modified = Join-Path $registered 'PRIVACY.md'
    Set-Content -LiteralPath $modified -Value 'Modified locally'
    # Simulate the user turning off startup after installing 0.2.
    Remove-ItemProperty -LiteralPath $runKey -Name 'Shardapps.Konvert'
    Invoke-Setup
    if (!$legacyHost.HasExited) { throw 'The legacy listener was not stopped.' }
    $null = Test-CompactInstall $registered
    $expectedCommand = '"' + (Join-Path $registered 'Konvert.App/Konvert.exe') + '" --popup "%1"'
    if ((Get-Item -LiteralPath $commandKey).GetValue('') -ne $expectedCommand) {
        throw 'Explorer still points to the legacy executable.'
    }
    if ((Get-ItemPropertyValue -LiteralPath $trackingKey -Name 'OwnedPath') -ne (Join-Path $registered 'Konvert.App')) {
        throw 'PATH did not move to the compact executable.'
    }
    if (Get-ItemPropertyValue -LiteralPath $runKey -Name 'Shardapps.Konvert' -ErrorAction SilentlyContinue) {
        throw 'Setup re-enabled a previously disabled startup option.'
    }
    if ((Test-Path -LiteralPath (Join-Path $registered 'LICENSE')) -or
        (Get-Content -LiteralPath $modified -Raw).Trim() -ne 'Modified locally') {
        throw 'Legacy cleanup failed to distinguish known and modified files.'
    }
    Test-ExistingSettings
    Invoke-Setup -Target $registered -Tasks $noTasks
    Test-NoIntegration
    Remove-TestInstall $registered
    if (!(Test-Path -LiteralPath $retained)) { throw 'Uninstall removed a user file.' }
    Write-Output 'PASS registered 0.2 upgrade, listener migration, preserved startup choice, exact cleanup and task opt-out.'

    Expand-Archive -LiteralPath $legacyZip -DestinationPath $orphan
    $retained = Join-Path $orphan 'My exported document.txt'
    Set-Content -LiteralPath $retained -Value 'Keep this export'
    $collision = Join-Path $orphan 'Konvert.App'
    New-Item -ItemType Directory -Path $collision | Out-Null
    $unrelated = Join-Path $collision 'Unrelated.txt'
    Set-Content -LiteralPath $unrelated -Value 'Existing subfolder'
    Invoke-Setup -Target $orphan -Tasks $noTasks -FailureText 'The Konvert.App subfolder contains a different copy or other files'
    if ((Get-Content -LiteralPath $unrelated -Raw).Trim() -ne 'Existing subfolder') { throw 'Setup changed an unrelated subfolder.' }
    Remove-Item -LiteralPath $unrelated
    # An empty reserved subfolder is safe to reuse.
    Invoke-Setup -Target $orphan -Tasks $noTasks
    $null = Test-CompactInstall $orphan
    if ((Get-Content -LiteralPath $retained -Raw).Trim() -ne 'Keep this export') { throw 'Upgrade changed a user file.' }
    if (Test-Path -LiteralPath (Join-Path $orphan 'LICENSE')) { throw 'Orphaned upgrade did not clean a known file.' }
    Test-NoIntegration
    Test-ExistingSettings
    Remove-TestInstall $orphan
    if (Test-Path -LiteralPath (Join-Path $orphan 'Konvert.App/Konvert.exe')) {
        throw 'Uninstall left the compact executable behind.'
    }
    Write-Output 'PASS unregistered 0.2 folder upgrade, Unicode path and subfolder collision protection.'
} finally {
    foreach ($directory in @($orphan, $registered, $fresh)) { Remove-TestInstall $directory }
    Test-ExistingSettings
    Remove-Item -LiteralPath $settingsFile
}
Test-NoIntegration
