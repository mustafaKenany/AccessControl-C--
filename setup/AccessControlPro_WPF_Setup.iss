; ═══════════════════════════════════════════════════════════════
; AccessControlPro - Main Application Installer (Inno Setup)
; ═══════════════════════════════════════════════════════════════
; Prerequisites: .NET 8.0 Desktop Runtime (x86), SQL Server
; Build first: dotnet publish -c Release -r win-x86 --self-contained false
; ═══════════════════════════════════════════════════════════════

#define MyAppName "HM-GymManagement"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "HM-Tech IT Solution"
#define MyAppURL "https://hm-tech.com"
#define MyAppExeName "AccessControlPro.WPF.exe"
#define PublishDir "..\src\AccessControlPro.WPF\bin\Release\net8.0-windows\win-x86\publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName=HM-GymManagement
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=AccessControlPro_Setup_v{#MyAppVersion}
SetupIconFile=..\src\AccessControlPro.WPF\Resources\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x86compatible
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Additional options:"

[Files]
; Main application files
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Native SDK DLLs
Source: "..\src\AccessControlPro.SDK\NativeDlls\*"; DestDir: "{app}"; Flags: ignoreversion

; Default appsettings.json (only if not exists - don't overwrite user config on upgrade)
Source: "..\src\AccessControlPro.WPF\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "..\src\AccessControlPro.WPF\SubscriptionPlans.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Auto-start with Windows (optional task)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; Launch app after install
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up generated files
Type: files; Name: "{app}\crash_log.txt"
Type: files; Name: "{app}\.setup_complete"
Type: filesandordirs; Name: "{app}\Logs"

[Code]
// Check if .NET 8 Desktop Runtime is installed
function IsDotNet8Installed(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
            and (ResultCode = 0);
  // More thorough check could parse output for Microsoft.WindowsDesktop.App 8.x
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNet8Installed() then
  begin
    if MsgBox('.NET 8.0 Desktop Runtime is required but was not detected.' + #13#10 +
              'Please install it from https://dotnet.microsoft.com/download/dotnet/8.0' + #13#10 + #13#10 +
              'Continue anyway?', mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;
