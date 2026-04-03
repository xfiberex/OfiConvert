; OfiConvert - InnoSetup Installer Script
; Requiere Inno Setup 6.x o superior

#define MyAppName "OfiConvert"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Ricky Angel Jimenez Bueno"
#define MyAppExeName "OfiConvert.exe"
#define MyAppDescription "Convertidor de archivos Office a múltiples formatos"
#define MyAppIcon "..\Assets\app.ico"

[Setup]
AppId={{B2E8F4A1-3C7D-4E9F-A1B2-6D8E0F3C5A7B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/xfiberex/OfiConvert
AppSupportURL=https://github.com/xfiberex/OfiConvert/issues
AppUpdatesURL=https://github.com/xfiberex/OfiConvert/releases
AppCopyright=Copyright (c) 2026 {#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=OfiConvert_Setup_{#MyAppVersion}
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
CloseApplicationsFilter=*{#MyAppExeName}*
UsePreviousAppDir=yes
UsePreviousGroup=yes
MinVersion=10.0
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppDescription}
VersionInfoProductName={#MyAppName}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\*.xbf"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\publish\*.pri"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\publish\Lang\*"; DestDir: "{app}\Lang"; Flags: ignoreversion
Source: "..\publish\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Flags: nowait shellexec; Check: IsAutoUpdate
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsAutoUpdate: Boolean;
begin
  Result := ExpandConstant('{param:autoinstall|0}') = '1';
end;

function IsOfficeInstalled: Boolean;
var
  WordPath: String;
begin
  Result := RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Winword.exe',
    '', WordPath);
  if not Result then
    Result := RegQueryStringValue(HKEY_LOCAL_MACHINE,
      'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\Winword.exe',
      '', WordPath);
end;

procedure InitializeWizard;
begin
  if not IsOfficeInstalled then
  begin
    MsgBox('ADVERTENCIA: No se detect' + #243 + ' Microsoft Office instalado en este equipo.' + #13#10 + #13#10 +
           'OfiConvert requiere Microsoft Office (Word, Excel y/o PowerPoint) ' +
           'instalado para funcionar correctamente.' + #13#10 + #13#10 +
           'Puede continuar con la instalaci' + #243 + 'n, pero la aplicaci' + #243 + 'n no funcionar' + #225 + ' ' +
           'hasta que instale Microsoft Office.', mbInformation, MB_OK);
  end;
end;
