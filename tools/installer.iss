#define AppName "LingNote"
#define AppVersion "0.1.33"
[Setup]
AppId={{E79C5407-988D-4A79-9061-F1B02C792993}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=LingNote
DefaultDirName={localappdata}\Programs\LingNote
DefaultGroupName=LingNote
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\artifacts
OutputBaseFilename=LingNote-0.1.33-Setup
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\LingNote\Assets\app.ico
UninstallDisplayIcon={app}\LingNote.exe
CloseApplications=yes
SetupLogging=yes
[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"
[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："
[Files]
Source: "..\app\*"; DestDir: "{app}"; Excludes: "models\ggml-small.bin,*.download,library-location.json,startup-status.json,settings.json,settings.json.bak,update-settings.json,updates\*,*.db,*.db-wal,*.db-shm,*.pdb,recordings\*,Data\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\assets\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsVC
[Icons]
Name: "{group}\LingNote"; Filename: "{app}\LingNote.exe"
Name: "{autodesktop}\LingNote"; Filename: "{app}\LingNote.exe"; Tasks: desktopicon
[Run]
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /passive /norestart"; StatusMsg: "安装语音识别运行组件…"; Check: NeedsVC; Flags: waituntilterminated
Filename: "{app}\LingNote.exe"; Description: "启动 LingNote"; Flags: nowait postinstall skipifsilent; Check: not IsUpdate
Filename: "{app}\LingNote.exe"; Flags: nowait; Check: IsUpdate
[Code]
function IsUpdate: Boolean;
begin
  Result := ExpandConstant('{param:LINGNOTEUPDATE|0}') = '1';
end;

function NeedsVC: Boolean;
var Installed: Cardinal;
begin
  Result := not RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed);
  if not Result then Result := Installed <> 1;
end;

