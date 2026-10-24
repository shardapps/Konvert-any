param([Parameter(Mandatory)][string]$Executable, [string]$Arguments, [string]$Output = 'artifacts/idle-memory.json')
$ErrorActionPreference = 'Stop'
$path = (Resolve-Path -LiteralPath $Executable).Path
$timer = [Diagnostics.Stopwatch]::StartNew()
$start = @{ FilePath = $path; PassThru = $true; WindowStyle = 'Hidden' }
if ($Arguments) { $start.ArgumentList = $Arguments }
$process = Start-Process @start
try {
    if (!$process.WaitForInputIdle(15000)) { throw 'Window did not become ready within 15 seconds.' }
    $ready = $timer.Elapsed.TotalMilliseconds
    $samples = @()
    1..5 | ForEach-Object {
        Start-Sleep -Seconds 1
        $process.Refresh()
        $samples += [ordered]@{ workingSetMiB = [math]::Round($process.WorkingSet64 / 1MB, 2); privateMiB = [math]::Round($process.PrivateMemorySize64 / 1MB, 2); cpuMilliseconds = $process.TotalProcessorTime.TotalMilliseconds }
    }
    $record = [ordered]@{ capturedUtc = [DateTime]::UtcNow.ToString('o'); runtime = 'net10.0-windows'; architecture = $env:PROCESSOR_ARCHITECTURE; readyMilliseconds = [math]::Round($ready); samples = $samples; limitations = 'Single machine, warm desktop, unloaded app, shared runtime; GPU/compositor and external engines excluded.' }
    $parent = Split-Path $Output -Parent
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Output
    $record | ConvertTo-Json -Depth 5
} finally {
    $process.Refresh()
    if (!$process.HasExited) { $null = $process.CloseMainWindow(); if (!$process.WaitForExit(5000)) { $process.Kill() } }
    $process.Dispose()
}
