#define AppVersion "0.2.3"
#define WinFspProduct "{C79D9B29-3AF0-45B3-9DB9-226F3C2D2204}"

[Setup]
AppId={{5506E725-88B2-4797-8D9F-1722B924FA8F}
AppName=Webdav Connetor
AppVersion={#AppVersion}
AppPublisher=D.M.Kang
DefaultDirName={localappdata}\Programs\UnionDrive
DisableDirPage=yes
DefaultGroupName=Webdav Connetor
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
OutputDir=..\dist
OutputBaseFilename=Webdav-Connetor-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\UnionDrive.App\Assets\app.ico
LicenseFile=..\LICENSE
InfoBeforeFile=INSTALL-NOTES.txt
UninstallDisplayIcon={app}\UnionDrive.exe
CloseApplications=no
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면 바로 가기 만들기"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "vendor\winfsp-2.1.25156.msi"; Flags: dontcopy

[Icons]
Name: "{group}\Webdav Connetor"; Filename: "{app}\UnionDrive.exe"
Name: "{group}\Webdav Connetor 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Webdav Connetor"; Filename: "{app}\UnionDrive.exe"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{userprograms}\UnionDrive\UnionDrive.lnk"
Type: files; Name: "{userprograms}\UnionDrive\UnionDrive 제거.lnk"
Type: files; Name: "{autodesktop}\UnionDrive.lnk"

[Registry]
Root: HKCU; Subkey: "Software\UnionDrive\Installation"; ValueType: string; ValueName: "InstallDir"; ValueData: "{app}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\UnionDrive.exe"; Description: "Webdav Connetor 실행"; Flags: nowait postinstall skipifsilent

[Code]
var
  InstalledWinFsp, CompletedInstall, RestartNeeded: Boolean;

function MsiQueryProductState(Product: String): Integer;
  external 'MsiQueryProductStateW@msi.dll stdcall';

function WinFspPresent: Boolean;
var
  Dir: String;
begin
  Result := RegQueryStringValue(HKLM32, 'Software\WinFsp', 'InstallDir', Dir);
  if not Result then Result := RegQueryStringValue(HKLM64, 'Software\WinFsp', 'InstallDir', Dir);
  if Result then Result := FileExists(AddBackslash(Dir) + 'bin\winfsp-x64.dll');
end;

function RunMsi(Parameters: String): Boolean;
var
  Code: Integer;
begin
  Result := ShellExec('runas', ExpandConstant('{sys}\msiexec.exe'), Parameters,
    '', SW_HIDE, ewWaitUntilTerminated, Code);
  if Result then begin
    Log(Format('WinFsp MSI result: %d', [Code]));
    Result := (Code = 0) or (Code = 3010) or (Code = 1641);
    if (Code = 3010) or (Code = 1641) then RestartNeeded := True;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\UnionDrive.exe')) then begin
    if not Exec(ExpandConstant('{app}\UnionDrive.exe'), '--prepare-update', '', SW_HIDE,
      ewWaitUntilTerminated, Code) then Code := -1;
    if Code <> 0 then begin
      Result := '기존 드라이브 작업 또는 미전송 파일이 있어 설치를 보류했습니다. 파일 전송을 완료하고 Webdav Connetor를 종료한 후 다시 설치하세요.';
      Exit;
    end;
  end;
  if not WinFspPresent then begin
    ExtractTemporaryFile('winfsp-2.1.25156.msi');
    if not RunMsi('/i "' + ExpandConstant('{tmp}\winfsp-2.1.25156.msi') + '" /qn /norestart ADDLOCAL=F.Main,F.User') then begin
      Result := 'WinFsp 설치에 실패했습니다. Windows 관리자 권한 요청을 허용하고 다시 설치하세요.';
      Exit;
    end;
    InstalledWinFsp := True;
    if not WinFspPresent then begin
      Result := 'WinFsp 설치를 확인하지 못했습니다. Windows를 다시 시작한 후 설치를 다시 실행하세요.';
      NeedsRestart := True;
    end;
  end;
end;

procedure CurStepChanged(Step: TSetupStep);
begin
  if Step = ssPostInstall then begin
    CompletedInstall := True;
    if InstalledWinFsp then begin
      RegWriteDWordValue(HKCU, 'Software\UnionDrive\Installation', 'WinFspOwned', 1);
      RegWriteStringValue(HKCU, 'Software\UnionDrive\Installation', 'WinFspProduct', '{#WinFspProduct}');
    end;
  end;
end;

procedure DeinitializeSetup;
begin
  if InstalledWinFsp and not CompletedInstall then
    RunMsi('/x {#WinFspProduct} /qn /norestart');
end;

function NeedRestart: Boolean;
begin
  Result := RestartNeeded;
end;

function CleanupForUninstall: Boolean;
var
  Code: Integer;
  Owned: Cardinal;
  Product: String;
begin
  Result := False;
  if not Exec(ExpandConstant('{app}\UnionDrive.exe'), '--prepare-update', '', SW_HIDE,
    ewWaitUntilTerminated, Code) then Code := -1;
  if Code <> 0 then begin
    MsgBox('실행 중인 드라이브 또는 미전송 파일이 있어 제거를 보류했습니다. 파일을 닫고 전송 완료 후 다시 시도하세요.', mbError, MB_OK);
    Exit;
  end;
  if RegQueryDWordValue(HKCU, 'Software\UnionDrive\Installation', 'WinFspOwned', Owned) and (Owned = 1) and
    RegQueryStringValue(HKCU, 'Software\UnionDrive\Installation', 'WinFspProduct', Product) and
    (Product = '{#WinFspProduct}') and (MsiQueryProductState(Product) = 5) then begin
    if not RunMsi('/x ' + Product + ' /qn /norestart') then begin
      MsgBox('함께 설치한 WinFsp 제거에 실패했습니다. Windows 관리자 권한 요청을 허용하고 다시 제거하세요.', mbError, MB_OK);
      Exit;
    end;
  end;
  if not Exec(ExpandConstant('{app}\UnionDrive.exe'), '--uninstall-cleanup', '', SW_HIDE,
    ewWaitUntilTerminated, Code) then Code := -1;
  if Code <> 0 then begin
    MsgBox('Webdav Connetor 사용자 데이터 정리에 실패했습니다. 데이터는 보존되었으며 제거를 중지했습니다.', mbError, MB_OK);
    Exit;
  end;
  Result := True;
end;

procedure CurUninstallStepChanged(Step: TUninstallStep);
begin
  { Cleanup runs only after the user has confirmed uninstall. }
  if Step = usUninstall then
    if not CleanupForUninstall then RaiseException('제거를 완료하지 못했습니다. 파일 전송 및 권한 상태를 확인한 후 다시 시도하세요.');
end;

function UninstallNeedRestart: Boolean;
begin
  Result := RestartNeeded;
end;

