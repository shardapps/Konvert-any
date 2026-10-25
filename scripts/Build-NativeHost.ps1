param([ValidateSet('x64','arm64')][string]$Architecture='x64', [string]$Output)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if (!$Output) { $Output=Join-Path $root "artifacts/native-$Architecture" }
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation= & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$installation) { throw 'Install Visual Studio C++ desktop tools and the Windows SDK.' }
$vcvars=Join-Path $installation 'VC/Auxiliary/Build/vcvarsall.bat'
$target=if($Architecture -eq 'arm64'){'amd64_arm64'}else{'amd64'}
$batch=Join-Path $Output 'compile.cmd'
$source=Join-Path $root 'native'
@"
@call "$vcvars" $target >nul
@if errorlevel 1 exit /b 1
@cd /d "$source"
@rc /nologo /fo "$Output\KonvertHost.res" KonvertHost.rc
@if errorlevel 1 exit /b 1
@cl /nologo /std:c++20 /EHsc /W4 /WX /O1 /MT /DUNICODE /D_UNICODE /Fo"$Output\KonvertHost.obj" KonvertHost.cpp "$Output\KonvertHost.res" /Fe"$Output\KonvertHost.exe" /link /SUBSYSTEM:WINDOWS /DYNAMICBASE /NXCOMPAT /GUARD:CF ole32.lib oleaut32.lib shell32.lib user32.lib advapi32.lib uuid.lib
"@ | Set-Content -LiteralPath $batch
& $env:ComSpec /d /c $batch
if ($LASTEXITCODE) { throw 'Native shortcut listener build failed.' }
Write-Output "Native $Architecture listener: $Output/KonvertHost.exe"
