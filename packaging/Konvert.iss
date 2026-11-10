#ifndef PayloadDirectory
  #error Build with scripts/Build-Installer.ps1
#endif
#ifndef TargetArchitecture
  #error TargetArchitecture must be x64 or arm64
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
VersionInfoVersion={#FileVersion}
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
Filename: "{app}\KonvertHost.exe"; Tasks: integration or startup; Flags: nowait runhidden
Filename: "{app}\Konvert.exe"; Description: "Open Konvert"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\KonvertHost.exe"; Parameters: "--exit"; Flags: runhidden waituntilterminated; RunOnceId: "StopKonvertHost"
Filename: "{app}\Konvert.exe"; Parameters: "--remove-integration"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveKonvertIntegration"

[Code]
function HasDesktopRuntime(): Boolean;
var
  DotnetRoot: String;
  Versions: TFindRec;
begin
  DotnetRoot := GetEnv('DOTNET_ROOT_' + Uppercase('{#TargetArchitecture}'));
  if DotnetRoot = '' then
    DotnetRoot := GetEnv('DOTNET_ROOT');
  if (DotnetRoot = '') and
    (not RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\{#TargetArchitecture}', 'InstallLocation', DotnetRoot)) then
  begin
    DotnetRoot := ExpandConstant('{commonpf64}\dotnet');
#if TargetArchitecture == "x64"
    if IsArm64 then
      DotnetRoot := DotnetRoot + '\x64';
#endif
  end;
  DotnetRoot := AddBackslash(DotnetRoot);
  Result := False;
  if FindFirst(DotnetRoot + 'shared\Microsoft.WindowsDesktop.App\{#RuntimeVersion}.*', Versions) then
  try
    repeat
      if (Pos('-', Versions.Name) = 0) and
        FileExists(DotnetRoot + 'shared\Microsoft.WindowsDesktop.App\' + Versions.Name + '\PresentationFramework.dll') and
        FileExists(DotnetRoot + 'shared\Microsoft.NETCore.App\' + Versions.Name + '\coreclr.dll') then
        Result := True;
    until Result or not FindNext(Versions);
  finally
    FindClose(Versions);
  end;
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := HasDesktopRuntime();
  if not Result then
  begin
    if SuppressibleMsgBox('Konvert needs Microsoft .NET Desktop Runtime {#RuntimeVersion} ({#TargetArchitecture}). Open the Microsoft download page, install the Desktop Runtime, then run Konvert Setup again?',
      mbConfirmation, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/{#RuntimeVersion}', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  HostPath: String;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\hostfxr.dll')) then
  begin
    Result := 'Uninstall the previous standalone Konvert alpha from Installed apps, then run this compact setup again. Your conversion preferences are stored separately.';
    Exit;
  end;
  HostPath := ExpandConstant('{app}\KonvertHost.exe');
  if FileExists(HostPath) then
    Exec(HostPath, '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
