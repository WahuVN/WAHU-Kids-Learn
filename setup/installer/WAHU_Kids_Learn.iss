; WAHU Kids Learn — Inno Setup V1
; Target: Windows 7 SP1+, x86 child runtime, per-user install.
; Build example:
;   ISCC.exe /DAppVersion=0.1.0 WAHU_Kids_Learn.iss
; Optional bundled .NET 4.8 offline installer:
;   ISCC.exe /DAppVersion=0.1.0 /DNet48Redist="D:\path\ndp48-x86-x64-allos-enu.exe" WAHU_Kids_Learn.iss

#ifndef AppVersion
  #define AppVersion "0.1.0-dev"
#endif

#define AppName "WAHU Kids Learn"
#define AppExeName "WAHUKidsLearn.exe"
#define PublisherName "WAHU"
#define PublishDir "..\..\build\win7_x86\publish"
#define OutputDir "..\..\build\installer"
#define Net48InstallerName "ndp48-x86-x64-allos-enu.exe"

[Setup]
AppId={{87D1C739-A798-4CE4-9CB3-E22EC6E5DE95}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#PublisherName}
DefaultDirName={localappdata}\Programs\WAHU Kids Learn
DefaultGroupName=WAHU Kids Learn
UninstallDisplayIcon={app}\{#AppExeName}
PrivilegesRequired=lowest
MinVersion=6.1sp1
SetupArchitecture=x86
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=WAHU-Kids-Learn-Setup-win7-x86-{#AppVersion}
ChangesAssociations=no
ChangesEnvironment=no
RestartIfNeededByRun=no
RestartApplications=no
CloseApplications=yes
CloseApplicationsFilter=WAHUKidsLearn.exe
SetupLogging=yes

[Languages]
; Compile-safe baseline. Full Vietnamese Inno translation will only be vendored
; after version/license review. App-specific strings below are already Vietnamese.
Name: "english"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
; The program tree is immutable application payload. Remove the generated GameV2
; subtree before every install/upgrade so assets removed by a newer release cannot
; linger indefinitely. Learner data is stored separately under %LOCALAPPDATA%\WAHU Kids Learn.
Type: filesandordirs; Name: "{app}\Assets\Generated\GameV2"
Type: files; Name: "{app}\Assets\Generated\Ready\README_VI.txt"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#ifdef Net48Redist
Source: "{#Net48Redist}"; DestDir: "{tmp}"; DestName: "{#Net48InstallerName}"; Flags: dontcopy noencryption
#endif

[Dirs]
Name: "{localappdata}\WAHU Kids Learn\config"
Name: "{localappdata}\WAHU Kids Learn\data"
Name: "{localappdata}\WAHU Kids Learn\content_user"
Name: "{localappdata}\WAHU Kids Learn\cache"
Name: "{localappdata}\WAHU Kids Learn\logs"
Name: "{localappdata}\WAHU Kids Learn\backups"
Name: "{localappdata}\WAHU Kids Learn\temp"
Name: "{localappdata}\WAHU Kids Learn\recovery"
Name: "{localappdata}\WAHU Kids Learn\updates"

[Icons]
Name: "{autoprograms}\WAHU Kids Learn"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\WAHU Kids Learn"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng ngoài màn hình"; GroupDescription: "Biểu tượng:"; Flags: unchecked
Name: "startup"; Description: "Khởi động WAHU Kids Learn cùng Windows"; GroupDescription: "Khởi động:"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WAHU Kids Learn"; ValueData: """{app}\{#AppExeName}"" --startup"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Mở WAHU Kids Learn"; Flags: nowait postinstall skipifsilent; Check: IsNet48OrLater

[Code]
const
  Net48ReleaseMin = 528040;

function MaxCardinal(A, B: Cardinal): Cardinal;
begin
  if A > B then
    Result := A
  else
    Result := B;
end;

function ReadNet48Release(): Cardinal;
var
  ReleaseValue: Cardinal;
  Found: Boolean;
begin
  Result := 0;

  Found := RegQueryDWordValue(
    HKLM32,
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
    'Release',
    ReleaseValue);
  if Found then
    Result := MaxCardinal(Result, ReleaseValue);

  if IsWin64 then
  begin
    Found := RegQueryDWordValue(
      HKLM64,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
      'Release',
      ReleaseValue);
    if Found then
      Result := MaxCardinal(Result, ReleaseValue);
  end;
end;

function IsNet48OrLater(): Boolean;
begin
  Result := ReadNet48Release() >= Net48ReleaseMin;
end;

function InstallBundledNet48(var NeedsRestart: Boolean): String;
#ifdef Net48Redist
var
  ResultCode: Integer;
  InstallerPath: String;
#endif
begin
  Result := '';

#ifdef Net48Redist
  ExtractTemporaryFile('{#Net48InstallerName}');
  InstallerPath := ExpandConstant('{tmp}\{#Net48InstallerName}');

  if not ShellExec(
    'runas',
    InstallerPath,
    '/q /norestart',
    '',
    SW_SHOW,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    Result := 'Không thể khởi chạy bộ cài .NET Framework 4.8 với quyền quản trị.';
    Exit;
  end;

  if ResultCode = 3010 then
    NeedsRestart := True
  else if ResultCode <> 0 then
  begin
    Result := Format('.NET Framework 4.8 cài không thành công. Mã lỗi: %d.', [ResultCode]);
    Exit;
  end;

  if (not NeedsRestart) and (not IsNet48OrLater()) then
    Result := 'Đã chạy bộ cài .NET Framework 4.8 nhưng hệ thống vẫn chưa nhận diện được phiên bản yêu cầu.';
#else
  Result :=
    'Máy chưa có .NET Framework 4.8. ' +
    'Hãy cài .NET Framework 4.8 Offline Installer của Microsoft rồi chạy lại bộ cài WAHU Kids Learn. ' +
    'Bộ cài này không tự tải thành phần từ Internet.';
#endif
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  { Production TODO/gate:
    Legacy Win7 SHA-2/signature readiness is defined in setup/18 and must be
    target-tested without treating absence of one exact KB id as the sole
    permanent failure signal. This installer intentionally performs no network
    remediation. Production signing is configured outside this secrets-free .iss. }
  Result := '';

  if IsNet48OrLater() then
    Exit;

  Result := InstallBundledNet48(NeedsRestart);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { Learner data lives outside the application install directory;
      installer intentionally never deletes it. }
  end;
end;
