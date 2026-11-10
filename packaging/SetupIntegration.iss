const
  ShellKey = 'Software\Classes\*\shell\Shardapps.Konvert';
  TrackingKey = 'Software\Shardapps\Konvert';
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunName = 'Shardapps.Konvert';

function SamePath(const Left, Right: String): Boolean;
begin
  Result := CompareText(RemoveBackslashUnlessRoot(Trim(Left)),
    RemoveBackslashUnlessRoot(Trim(Right))) = 0;
end;

function CommandTargets(const Command, Executable: String): Boolean;
var
  Quoted: String;
begin
  Quoted := '"' + Executable + '"';
  Result := SamePath(Command, Executable) or (CompareText(Command, Quoted) = 0) or
    (CompareText(Copy(Command, 1, Length(Quoted) + 1), Quoted + ' ') = 0);
end;

function OwnsCommand(const Key, ValueName, Dir, FileName: String): Boolean;
var
  Command: String;
begin
  Result := RegQueryStringValue(HKCU, Key, ValueName, Command) and
    CommandTargets(Command, AddBackslash(Dir) + FileName);
end;

function PathWithout(const Value, Directory: String; var Found: Boolean): String;
var
  Offset, EndOfSegment: Integer;
  Segment: String;
  First: Boolean;
begin
  Result := '';
  Found := False;
  First := True;
  Offset := 1;
  repeat
    EndOfSegment := Offset;
    while (EndOfSegment <= Length(Value)) and (Value[EndOfSegment] <> ';') do
      EndOfSegment := EndOfSegment + 1;
    Segment := Copy(Value, Offset, EndOfSegment - Offset);
    if not Found and SamePath(RemoveQuotes(Trim(Segment)), Directory) then
      Found := True
    else
    begin
      if not First then Result := Result + ';';
      Result := Result + Segment;
      First := False;
    end;
    Offset := EndOfSegment + 1;
  until EndOfSegment > Length(Value);
end;

function RemoveOwnedPath(const Root, AppDir: String; Replace: Boolean): Boolean;
var
  Owned, Current, Updated: String;
  Found: Boolean;
begin
  Result := True;
  if not RegQueryStringValue(HKCU, TrackingKey, 'OwnedPath', Owned) then Exit;
  if not (Replace or SamePath(Owned, Root) or SamePath(Owned, AppDir)) then Exit;
  if RegQueryStringValue(HKCU, 'Environment', 'Path', Current) then
  begin
    Updated := PathWithout(Current, Owned, Found);
    if Found and not RegWriteExpandStringValue(HKCU, 'Environment', 'Path', Updated) then
    begin
      Result := False;
      Exit;
    end;
  end;
  Result := RegDeleteValue(HKCU, TrackingKey, 'OwnedPath');
end;

function AddOwnedPath(const AppDir: String): Boolean;
var
  Current, Discarded, Updated: String;
  Found: Boolean;
begin
  Current := '';
  RegQueryStringValue(HKCU, 'Environment', 'Path', Current);
  Discarded := PathWithout(Current, AppDir, Found);
  Result := True;
  if Found then Exit;
  Updated := AppDir;
  if Current <> '' then Updated := Updated + ';' + Current;
  if not RegWriteStringValue(HKCU, TrackingKey, 'OwnedPath', AppDir) then
  begin
    Result := False;
    Exit;
  end;
  Result := RegWriteExpandStringValue(HKCU, 'Environment', 'Path', Updated);
  if not Result then RegDeleteValue(HKCU, TrackingKey, 'OwnedPath');
end;

function ConfigureIntegration(const Root, AppDir: String; Explorer, Startup: Boolean): Boolean;
var
  AppExe, HostExe: String;
begin
  Result := RemoveOwnedPath(Root, AppDir, Explorer);
  if not Result then Exit;
  AppExe := AddBackslash(AppDir) + 'Konvert.exe';
  HostExe := AddBackslash(AppDir) + 'KonvertHost.exe';
  if Explorer then
  begin
    if not RegWriteStringValue(HKCU, ShellKey, '', 'Konvert') then Result := False;
    if not RegWriteStringValue(HKCU, ShellKey, 'Icon', AppExe) then Result := False;
    if not RegWriteStringValue(HKCU, ShellKey, 'MultiSelectModel', 'Single') then Result := False;
    if not RegWriteStringValue(HKCU, ShellKey + '\command', '', '"' + AppExe + '" --popup "%1"') then Result := False;
    if not AddOwnedPath(AppDir) then Result := False;
  end
  else if OwnsCommand(ShellKey + '\command', '', Root, 'Konvert.exe') or
    OwnsCommand(ShellKey + '\command', '', AppDir, 'Konvert.exe') then
  begin
    if not RegDeleteKeyIncludingSubkeys(HKCU, ShellKey) then Result := False;
  end;
  if Startup then
  begin
    if not RegWriteStringValue(HKCU, RunKey, RunName, '"' + HostExe + '"') then Result := False;
  end
  else if OwnsCommand(RunKey, RunName, Root, 'KonvertHost.exe') or
    OwnsCommand(RunKey, RunName, AppDir, 'KonvertHost.exe') then
  begin
    if not RegDeleteValue(HKCU, RunKey, RunName) then Result := False;
  end;
end;
