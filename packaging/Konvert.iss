#ifndef PayloadDirectory
  #error Build with scripts/Build-Installer.ps1
#endif
#ifndef TargetArchitecture
  #error TargetArchitecture must be x64 or arm64
#endif
#ifndef AppVersion
  #define AppVersion "0.2.0-alpha.1"
#endif

[Setup]
AppId={{FB2E8267-E891-4CE0-8F84-441FB22E54E5}
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
UninstallDisplayIcon={app}\Konvert.exe
SetupIconFile=Assets\Konvert.ico
LicenseFile=..\LICENSE
OutputBaseFilename=Konvert-Setup-{#AppVersion}-{#TargetArchitecture}
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
VersionInfoVersion=0.2.0.0
VersionInfoDescription=Konvert by Shardapps Setup
SignedUninstaller=no

[Tasks]
Name: "integration"; Description: "Add Konvert to Explorer and your user PATH"; GroupDescription: "Windows integration:"
Name: "startup"; Description: "Keep Ctrl+K ready when you sign in"; GroupDescription: "Windows integration:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PayloadDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Konvert"; Filename: "{app}\Konvert.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Konvert"; Filename: "{app}\Konvert.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Shardapps.Konvert"; ValueData: """{app}\KonvertHost.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Konvert.exe"; Parameters: "--install-integration"; Tasks: integration; Flags: runhidden waituntilterminated
Filename: "{app}\KonvertHost.exe"; Flags: nowait runhidden
Filename: "{app}\Konvert.exe"; Description: "Open Konvert"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\KonvertHost.exe"; Parameters: "--exit"; Flags: runhidden waituntilterminated; RunOnceId: "StopKonvertHost"
Filename: "{app}\Konvert.exe"; Parameters: "--remove-integration"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveKonvertIntegration"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  HostPath: String;
begin
  Result := '';
  HostPath := ExpandConstant('{app}\KonvertHost.exe');
  if FileExists(HostPath) then
    Exec(HostPath, '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
