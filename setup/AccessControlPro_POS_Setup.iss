; ═══════════════════════════════════════════════════════════════
; AccessControlPro POS Terminal - Installer (Inno Setup)
; ═══════════════════════════════════════════════════════════════
; Prerequisites: .NET 8.0 Desktop Runtime (x86), SQL Server
; Build first: dotnet publish -c Release -r win-x86 --self-contained false
; ═══════════════════════════════════════════════════════════════

#define MyAppName "HM-GymManagement POS"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "HM-Tech IT Solution"
#define MyAppURL "https://hm-tech.com"
#define MyAppExeName "AccessControlPro.POS.exe"
#define PublishDir "..\src\AccessControlPro.POS\bin\Release\net8.0-windows\win-x86\publish"

[Setup]
AppId={{C3D4E5F6-A7B8-9012-CDEF-123456789012}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\HM-GymManagement\POS
DefaultGroupName=HM-GymManagement
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=AccessControlPro_POS_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile=..\src\AccessControlPro.POS\app.ico
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x86compatible
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\AccessControlPro.POS\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\pos_crash_log.txt"
Type: files; Name: "{app}\.setup_complete"

[Code]
function IsDotNet8Installed(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
            and (ResultCode = 0);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNet8Installed() then
  begin
    if MsgBox('.NET 8.0 Desktop Runtime is required but was not detected.' + #13#10 +
              'Please install it from https://dotnet.microsoft.com/download/dotnet/8.0' + #13#10 + #13#10 +
              'Continue anyway?', mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;
