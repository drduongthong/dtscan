; ═══════════════════════════════════════════════
;  DTScan — Inno Setup Script
;  Build Release trước khi chạy:
;    dotnet publish -c Release -r win-x64 --self-contained
; ═══════════════════════════════════════════════

#define MyAppName      "DTScan"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "DTScan"
#define MyAppExeName   "DTScan.exe"
#define MyAppURL       ""

; Đường dẫn tới output của dotnet publish
#define PublishDir     "..\bin\publish"

[Setup]
AppId={{D7E5C3A1-8F42-4B9E-A6D0-1C3E5F7A9B2D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=DTScan_Setup_{#MyAppVersion}
SetupIconFile=..\Assets\DTScan.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "Create a &Quick Launch icon"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Toàn bộ file publish (self-contained)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Icon
Source: "..\Assets\DTScan.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Xóa data local khi gỡ cài đặt (tùy chọn — bỏ comment nếu muốn)
; Type: filesandordirs; Name: "{localappdata}\DTScan"

[Code]
// Kiểm tra phiên bản cũ đang chạy
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if CheckForMutexes('{#MyAppName}_Mutex') then
  begin
    if MsgBox('{#MyAppName} is currently running.' + #13#10 +
              'Please close it before continuing.' + #13#10#13#10 +
              'Click OK after closing the application.',
              mbInformation, MB_OKCANCEL) = IDCANCEL then
    begin
      Result := False;
    end;
  end;
end;
