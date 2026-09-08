; WAHU Kids Learn — Portable All-in-One bootstrapper
; Target: Windows 7 SP1+, x86 app runtime.
; This package extracts the portable app beside the bootstrapper and bundles
; the official Microsoft .NET Framework 4.8 offline redistributable.

#ifndef AppVersion
  #define AppVersion "0.1.0-dev"
#endif
#ifndef Net48Redist
  #error Net48Redist must point to the official NDP48-x86-x64-AllOS-ENU.exe
#endif
#ifndef Net48Sha256
  #error Net48Sha256 must be the pinned SHA-256 of Net48Redist
#endif

#define AppName "WAHU Kids Learn"
#define AppExeName "WAHUKidsLearn.exe"
#define PublisherName "WAHU"
#define PortablePublishDir "..\..\build\win7_x86\portable"
#define OutputDir "..\..\build\allinone"
#define Net48InstallerName "ndp48-x86-x64-allos-enu.exe"

[Setup]
AppId={{A2C21D50-62A6-4F67-A449-87E77C03E74D}
AppName={#AppName} Portable All-in-One
AppVersion={#AppVersion}
AppPublisher={#PublisherName}
DefaultDirName={src}\WAHU-Kids-Learn-Portable
PrivilegesRequired=lowest
MinVersion=6.1sp1
SetupArchitecture=x86
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=no
Uninstallable=no
CreateUninstallRegKey=no
OutputDir={#OutputDir}
OutputBaseFilename=WAHU-Kids-Learn-Portable-AllInOne-win7-x86-{#AppVersion}
ChangesAssociations=no
ChangesEnvironment=no
RestartIfNeededByRun=yes
RestartApplications=no
CloseApplications=yes
CloseApplicationsFilter=WAHUKidsLearn.exe
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PortablePublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Net48Redist}"; DestDir: "{tmp}"; DestName: "{#Net48InstallerName}"; Flags: dontcopy noencryption

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--portable"; Description: "Mở WAHU Kids Learn"; Flags: nowait postinstall skipifsilent; Check: CanLaunchAppNow

[Code]
const
  Net48ReleaseMin = 528040;
  Net48ExpectedSha256 = '{#Net48Sha256}';

var
  Net48NeedsRestart: Boolean;

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

function CanLaunchAppNow(): Boolean;
begin
  Result := IsNet48OrLater() and (not Net48NeedsRestart);
end;

function InstallBundledNet48(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  InstallerPath: String;
  InstallerHash: String;
  InstallLogPath: String;
  Params: String;
begin
  Result := '';
  ExtractTemporaryFile('{#Net48InstallerName}');
  InstallerPath := ExpandConstant('{tmp}\{#Net48InstallerName}');
  InstallerHash := GetSHA256OfFile(InstallerPath);

  if CompareText(InstallerHash, Net48ExpectedSha256) <> 0 then
  begin
    Result :=
      'Gói .NET Framework 4.8 đi kèm không vượt qua kiểm tra SHA-256. ' +
      'WAHU sẽ không chạy file prerequisite này.';
    Exit;
  end;

  InstallLogPath := ExpandConstant('{tmp}\WAHU-Net48-Install.log');
  Params := '/install /quiet /norestart /log "' + InstallLogPath + '"';

  if not ShellExec(
    'runas',
    InstallerPath,
    Params,
    '',
    SW_SHOW,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    Result :=
      'Không thể khởi chạy bộ cài .NET Framework 4.8 với quyền quản trị. ' +
      'Hãy chọn Yes khi Windows hỏi quyền rồi chạy lại file All-in-One.';
    Exit;
  end;

  if ResultCode = 3010 then
  begin
    Net48NeedsRestart := True;
    NeedsRestart := True;
    Exit;
  end;

  if ResultCode <> 0 then
  begin
    Result := Format('.NET Framework 4.8 cài không thành công. Mã lỗi: %d. ' +
      'Máy phải là Windows 7 SP1 hoặc mới hơn. Nếu Windows 7 quá cũ, hãy cập nhật nền hệ thống rồi thử lại.', [ResultCode]);
    Exit;
  end;

  if not IsNet48OrLater() then
    Result :=
      'Bộ cài .NET Framework 4.8 đã chạy nhưng Windows chưa nhận diện runtime. ' +
      'Hãy khởi động lại máy rồi chạy lại file All-in-One.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  Net48NeedsRestart := False;
  if IsNet48OrLater() then
    Exit;

  Result := InstallBundledNet48(NeedsRestart);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AppCommand: String;
begin
  if (CurStep = ssPostInstall) and Net48NeedsRestart then
  begin
    AppCommand := '"' + ExpandConstant('{app}\{#AppExeName}') + '" --portable';
    RegWriteStringValue(
      HKCU,
      'Software\Microsoft\Windows\CurrentVersion\RunOnce',
      'WAHU Kids Learn Portable',
      AppCommand);
  end;
end;
