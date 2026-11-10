const
  PayloadFolder = 'Konvert.App';
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProductId}_is1';
  ReparseAttribute = $400;
  DirectoryAttribute = $10;

var
  TasksRestored: Boolean;
  CleanLegacy: Boolean;
  IntegrationFailed: Boolean;

function GetFileAttributes(const FileName: String): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';
function SetEnvironmentVariable(const Name, Value: String): Boolean;
  external 'SetEnvironmentVariableW@kernel32.dll stdcall';

function AppDirectory(const Root: String): String;
begin
  Result := AddBackslash(Root) + PayloadFolder;
end;

function IsReparsePoint(const Path: String): Boolean;
var
  Attributes: Cardinal;
begin
  Attributes := GetFileAttributes(Path);
  Result := (Attributes <> $FFFFFFFF) and ((Attributes and ReparseAttribute) <> 0);
end;

function IsPlainFile(const Path: String): Boolean;
var
  Attributes: Cardinal;
begin
  Attributes := GetFileAttributes(Path);
  Result := (Attributes <> $FFFFFFFF) and ((Attributes and (ReparseAttribute or DirectoryAttribute)) = 0);
end;

function IsEmptyFolder(const Dir: String): Boolean;
var
  Entry: TFindRec;
begin
  Result := True;
  if FindFirst(AddBackslash(Dir) + '*', Entry) then
  try
    repeat
      if (Entry.Name <> '.') and (Entry.Name <> '..') then
      begin
        Result := False;
        Exit;
      end;
    until not FindNext(Entry);
  finally
    FindClose(Entry);
  end;
end;

function RecognizesKonvert(const Dir: String): Boolean;
var
  Manifest: AnsiString;
  ManifestPath: String;
  Size: Integer;
begin
  ManifestPath := AddBackslash(Dir) + 'Konvert.deps.json';
  // Identity check for a stranded published copy. The file is read as data and never executed.
  Result := IsPlainFile(ManifestPath) and FileSize(ManifestPath, Size) and (Size <= 2 * 1024 * 1024) and
    LoadStringFromFile(ManifestPath, Manifest) and
    (Pos('"Konvert/', Manifest) > 0) and (Pos('"Konvert.dll"', Manifest) > 0);
end;

function RegisteredFolder(): String;
begin
  Result := '';
  RegQueryStringValue(HKCU, UninstallKey, 'InstallLocation', Result);
end;

function HasNewerCopy(const Dir: String): Boolean;
var
  FoundVersion, SetupVersion: Int64;
begin
  Result := GetPackedVersion(AddBackslash(Dir) + 'Konvert.dll', FoundVersion) and
    StrToVersion('{#FileVersion}', SetupVersion) and (ComparePackedVersion(FoundVersion, SetupVersion) > 0);
end;

function DestinationError(const Root: String): String;
var
  Registered, AppDir: String;
begin
  Result := '';
  Registered := RegisteredFolder();
  AppDir := AppDirectory(Root);
  if (Registered <> '') and DirExists(Registered) and not SamePath(Registered, Root) then
    Result := 'Konvert is already installed in ' + Registered + '. Use that folder to upgrade.'
  else if IsReparsePoint(Root) or IsReparsePoint(AppDir) then
    Result := 'Choose a regular local folder for Konvert. This destination points to another location.'
  else if FileExists(AppDir) or
    FileExists(AddBackslash(AppDir) + 'hostfxr.dll') or
    (DirExists(AppDir) and not IsEmptyFolder(AppDir) and not RecognizesKonvert(AppDir)) then
    Result := 'The Konvert.App subfolder contains a different copy or other files. Move that subfolder to a safe location and retry.'
  else if DirExists(Root) and not IsEmptyFolder(Root) and
    not RecognizesKonvert(Root) and not RecognizesKonvert(AppDir) and
    ((Registered = '') or not SamePath(Registered, Root)) then
    Result := 'This folder contains other files. Choose a new empty folder for Konvert.'
  else if HasNewerCopy(Root) or HasNewerCopy(AppDir) then
    Result := 'A newer version of Konvert is already in this folder. Use the newer setup to repair it.';
end;

function CheckRuntime(): Boolean;
var
  ResultCode: Integer;
  Previous: String;
begin
  ExtractTemporaryFiles('{tmp}\Konvert.*');
  // The packaged executable verifies its actual runtime resolution, including architecture.
  Previous := GetEnv('DOTNET_DISABLE_GUI_ERRORS');
  SetEnvironmentVariable('DOTNET_DISABLE_GUI_ERRORS', '1');
  try
    Result := Exec(ExpandConstant('{tmp}\Konvert.exe'), '--setup-check', ExpandConstant('{tmp}'),
      SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
    if Result then Log('Konvert runtime probe passed.')
    else Log('Konvert runtime probe failed with code ' + IntToStr(ResultCode));
  finally
    SetEnvironmentVariable('DOTNET_DISABLE_GUI_ERRORS', Previous);
  end;
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := CheckRuntime();
  if not Result then
    if SuppressibleMsgBox('Konvert needs the Microsoft .NET Desktop Runtime {#RuntimeVersion} for {#TargetArchitecture}. ' +
      'Install or repair that runtime, then run Konvert Setup again. Open Microsoft''s download page?',
      mbConfirmation, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/{#RuntimeVersion}', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;

function ExplicitTasks(): Boolean;
var
  Index: Integer;
  Arg: String;
begin
  Result := True;
  for Index := 1 to ParamCount do
  begin
    Arg := Uppercase(ParamStr(Index));
    if (Pos('/TASKS=', Arg) = 1) or (Pos('/MERGETASKS=', Arg) = 1) then Exit;
  end;
  Result := False;
end;

procedure RestoreExistingTasks();
var
  Root, AppDir: String;
  Explorer, Startup: Boolean;
begin
  if TasksRestored or ExplicitTasks() then Exit;
  Root := WizardDirValue();
  AppDir := AppDirectory(Root);
  if not (RecognizesKonvert(Root) or RecognizesKonvert(AppDir)) then Exit;
  Explorer := OwnsCommand(ShellKey + '\command', '', Root, 'Konvert.exe') or
    OwnsCommand(ShellKey + '\command', '', AppDir, 'Konvert.exe');
  Startup := OwnsCommand(RunKey, RunName, Root, 'KonvertHost.exe') or
    OwnsCommand(RunKey, RunName, AppDir, 'KonvertHost.exe');
  // An orphan with no existing integration has no stored setup choices to restore.
  if (RegisteredFolder() = '') and not Explorer and not Startup then Exit;
  TasksRestored := True;
  if Explorer then WizardSelectTasks('integration')
  else WizardSelectTasks('!integration');
  if Startup then WizardSelectTasks('startup')
  else WizardSelectTasks('!startup');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectTasks then RestoreExistingTasks();
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ErrorText: String;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and not WizardSilent() then
  begin
    ErrorText := DestinationError(WizardDirValue());
    if ErrorText <> '' then
    begin
      MsgBox(ErrorText, mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function UpdateReadyMemo(Space, NewLine, UserInfo, DirInfo, TypeInfo, ComponentsInfo, GroupInfo, TasksInfo: String): String;
begin
  if RecognizesKonvert(WizardDirValue()) or RecognizesKonvert(AppDirectory(WizardDirValue())) then
    Result := 'Update Konvert in its existing folder. Your preferences and converted files stay in place.' + NewLine + NewLine
  else
    Result := 'Install Konvert for your Windows account.' + NewLine + NewLine;
  Result := Result + DirInfo;
  if TasksInfo <> '' then Result := Result + NewLine + NewLine + TasksInfo;
end;

function StopExistingListener(const Dir: String): Boolean;
var
  Target, Helper: String;
  ResultCode: Integer;
begin
  Target := AddBackslash(Dir) + 'KonvertHost.exe';
  Result := True;
  if not FileExists(Target) then Exit;
  Helper := ExpandConstant('{tmp}\KonvertHost.exe');
  if not FileExists(Helper) then ExtractTemporaryFile('KonvertHost.exe');
  // Use Setup's own helper, even if a legacy host executable in the destination is modified.
  Result := Exec(Helper, '--stop-at "' + Target + '"', ExpandConstant('{tmp}'), SW_HIDE,
    ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Root: String;
begin
  Root := WizardDirValue();
  Result := DestinationError(Root);
  if Result <> '' then Exit;
  if WizardSilent() then RestoreExistingTasks();
  CleanLegacy := RecognizesKonvert(Root);
  if not (StopExistingListener(Root) and StopExistingListener(AppDirectory(Root))) then
    Result := 'Konvert could not stop its shortcut listener. Quit it from the tray and choose Retry.';
end;

procedure CleanKnownLegacyFiles(const Root: String);
var
  Rows: TArrayOfString;
  Index, Separator: Integer;
  Name, FilePath, Parent, Digest: String;
  SafePath: Boolean;
begin
  if IsReparsePoint(Root) then Exit;
  ExtractTemporaryFile('LegacyAlpha.sha256');
  if not LoadStringsFromFile(ExpandConstant('{tmp}\LegacyAlpha.sha256'), Rows) then Exit;
  for Index := 0 to GetArrayLength(Rows) - 1 do
  begin
    Separator := Pos('|', Rows[Index]);
    if Separator = 0 then Continue;
    Name := Copy(Rows[Index], 1, Separator - 1);
    if (Name = '') or (Name[1] = '\') or (Pos('/', Name) > 0) or (Pos('..', Name) > 0) or (Pos(':', Name) > 0) then Continue;
    FilePath := AddBackslash(Root) + Name;
    if not IsPlainFile(FilePath) then Continue;
    SafePath := True;
    Parent := ExtractFileDir(FilePath);
    while not SamePath(Parent, Root) do
    begin
      if IsReparsePoint(Parent) then SafePath := False;
      Parent := ExtractFileDir(Parent);
    end;
    if not SafePath then Continue;
    try
      Digest := Lowercase(GetSHA256OfFile(FilePath));
      if Pos('|' + Digest + '|', Copy(Rows[Index], Separator, MaxInt) + '|') > 0 then
        if not DeleteFile(FilePath) then Log('Could not remove legacy file: ' + Name);
    except
      Log('Skipped legacy file: ' + Name);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Root: String;
begin
  if CurStep <> ssPostInstall then Exit;
  Root := ExpandConstant('{app}');
  IntegrationFailed := not ConfigureIntegration(Root, AppDirectory(Root),
    WizardIsTaskSelected('integration'), WizardIsTaskSelected('startup'));
  if IntegrationFailed then
    SuppressibleMsgBox('Konvert installed, but Windows integration could not be fully applied. ' +
      'Run Setup again to retry the selected options.', mbError, MB_OK, IDOK);
  if CleanLegacy and not IntegrationFailed then CleanKnownLegacyFiles(Root);
end;

function GetCustomSetupExitCode(): Integer;
begin
  Result := 0;
  if IntegrationFailed then Result := 20;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Root: String;
begin
  if CurUninstallStep <> usUninstall then Exit;
  Root := ExpandConstant('{app}');
  if not ConfigureIntegration(Root, AppDirectory(Root), False, False) then
    Log('Could not remove all Konvert Windows integration entries.');
end;
