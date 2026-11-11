#ifndef PayloadDirectory
  #error Build with scripts/Build-Installer.ps1
#endif
#ifndef TargetArchitecture
  #error TargetArchitecture must be x64 or arm64
#endif
#define ProductId "{FB2E8267-E891-4CE0-8F84-441FB22E54E5}"

[Setup]
AppId={{#ProductId}
AppName=Konvert
AppVersion={#AppVersion}
AppPublisher=Shardapps
AppPublisherURL=https://github.com/shardapps
AppSupportURL=https://github.com/shardapps/Konvert-any/issues
DefaultDirName={localappdata}\Programs\Shardapps\Konvert
DefaultGroupName=Shardapps
PrivilegesRequired=lowest
MinVersion=10.0.22000
DisableProgramGroupPage=yes
DirExistsWarning=no
#if TargetArchitecture == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
AppMutex=Local\Shardapps.Konvert.UI
SetupMutex=Local\Shardapps.Konvert.Setup
CloseApplications=yes
RestartApplications=no
ChangesEnvironment=yes
UninstallDisplayIcon={app}\Konvert.App\Konvert.exe
SetupIconFile=Assets\Konvert.ico
WizardSmallImageFile=Assets\Square150x150Logo.png
LicenseFile=..\LICENSE
OutputBaseFilename=Konvert-Setup-{#AppVersion}-{#TargetArchitecture}
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern dynamic
VersionInfoVersion={#FileVersion}
VersionInfoDescription=Konvert by Shardapps Setup
SignedUninstaller=no

[Tasks]
Name: "integration"; Description: "Add Konvert to Explorer and your user PATH"; GroupDescription: "Windows integration:"
Name: "startup"; Description: "Keep Ctrl+K ready when you sign in"; GroupDescription: "Windows integration:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PayloadDirectory}\Konvert.exe"; Flags: dontcopy
Source: "{#PayloadDirectory}\Konvert.dll"; Flags: dontcopy
Source: "{#PayloadDirectory}\Konvert.deps.json"; Flags: dontcopy
Source: "{#PayloadDirectory}\Konvert.runtimeconfig.json"; Flags: dontcopy
Source: "{#PayloadDirectory}\KonvertHost.exe"; Flags: dontcopy
Source: "{#PayloadDirectory}\UglyToad.PdfPig*.dll"; Flags: dontcopy
Source: "LegacyAlpha.sha256"; Flags: dontcopy
Source: "{#PayloadDirectory}\Konvert.exe"; DestDir: "{app}\Konvert.App"; Flags: ignoreversion
Source: "{#PayloadDirectory}\Konvert.dll"; DestDir: "{app}\Konvert.App"; Flags: ignoreversion
Source: "{#PayloadDirectory}\Konvert.deps.json"; DestDir: "{app}\Konvert.App"; Flags: ignoreversion
Source: "{#PayloadDirectory}\Konvert.runtimeconfig.json"; DestDir: "{app}\Konvert.App"; Flags: ignoreversion
Source: "{#PayloadDirectory}\KonvertHost.exe"; DestDir: "{app}\Konvert.App"; Flags: ignoreversion
Source: "{#PayloadDirectory}\KonvertPdf.exe"; DestDir: "{app}\Konvert.App"; Flags: ignoreversion
Source: "{#PayloadDirectory}\UglyToad.PdfPig*.dll"; DestDir: "{app}\Konvert.App"; Flags: ignoreversion
Source: "{#PayloadDirectory}\LICENSE"; DestDir: "{app}\Konvert.App"
Source: "{#PayloadDirectory}\NOTICE"; DestDir: "{app}\Konvert.App"
Source: "{#PayloadDirectory}\PRIVACY.md"; DestDir: "{app}\Konvert.App"
Source: "{#PayloadDirectory}\THIRD_PARTY_NOTICES.md"; DestDir: "{app}\Konvert.App"

[Icons]
Name: "{group}\Konvert"; Filename: "{app}\Konvert.App\Konvert.exe"; WorkingDir: "{app}\Konvert.App"
Name: "{autodesktop}\Konvert"; Filename: "{app}\Konvert.App\Konvert.exe"; WorkingDir: "{app}\Konvert.App"; Tasks: desktopicon

[Run]
Filename: "{app}\Konvert.App\KonvertHost.exe"; Tasks: integration or startup; Flags: nowait runhidden
Filename: "{app}\Konvert.App\Konvert.exe"; Description: "Open Konvert"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\Konvert.App\KonvertHost.exe"; Parameters: "--exit"; Flags: runhidden waituntilterminated; RunOnceId: "StopKonvertHost"
; Replace the legacy log entry. Registry cleanup now works without the .NET runtime.
Filename: "{app}\Konvert.App\KonvertHost.exe"; Parameters: "--exit"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveKonvertIntegration"

[Code]
#include "SetupIntegration.iss"
#include "SetupUpgrade.iss"
