; OfiConvert - InnoSetup Installer Script
; Requiere Inno Setup 6.x o superior
;
; NO se compila a mano: lo hace installer\build-installer.ps1, que pasa la versión leída del .csproj
; (/DMyAppVersion) y la carpeta del publish (/DPublishDir). Los valores de abajo son solo el respaldo
; para cuando alguien abre este archivo en el IDE de Inno Setup — y ENVEJECEN. La fuente de la versión
; es <Version> en OfiConvert.csproj.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-local"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

#define MyAppName "OfiConvert"
#define MyAppPublisher "Ricky Angel Jimenez Bueno"
#define MyAppExeName "OfiConvert.exe"
#define MyAppDescription "Convertidor por lotes de documentos Office a PDF, HTML, CSV, PNG y JPG"
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
; "commandline" ADEMAS de "dialog", y no es opcional:
;
; Con solo "dialog", Inno muestra el cuadro "Seleccione el modo de instalacion" (solo para mi / para todos
; los usuarios) INCLUSO CON /VERYSILENT, y se queda ahi bloqueado esperando un clic. En una instalacion
; desatendida eso cuelga el proceso para siempre; y en la AUTO-ACTUALIZACION, la app ya se ha cerrado, asi
; que el usuario ve su programa desaparecer y aparecer un dialogo que no ha pedido. Se descubrio probando
; el instalador de punta a punta (2026-07-14).
;
; Con "commandline", /ALLUSERS y /CURRENTUSER pasan a estar permitidos: el updater manda el que conserva
; el alcance con el que el usuario instalo la app (ver Core/InstallScope.cs) y el dialogo no aparece.
PrivilegesRequiredOverridesAllowed=commandline dialog
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
; Self-contained publish: copia todo el contenido recursivamente (incluye runtime .NET 10).
; PublishDir lo inyecta build-installer.ps1 apuntando a %TEMP% — ver la nota de MAX_PATH allí.
; OJO: sin 'skipifsourcedoesntexist' a propósito. Antes lo llevaba, y eso convertía un publish
; ausente o vacío en un instalador que se generaba "bien"… sin la aplicación dentro.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

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
  { WizardSilent() NO es opcional: Inno llama a InitializeWizard tambien en /SILENT y /VERYSILENT, y un
    MsgBox se muestra IGUAL salvo que se pase /SUPPRESSMSGBOXES. La auto-actualizacion lanza el instalador
    con la app ya cerrada, asi que sin esta guarda el usuario SIN Office —justo el que la app dice
    soportar con LibreOffice— veia su programa desaparecer y quedarse un dialogo esperando un clic, o la
    actualizacion colgada. Es el mismo fallo del Tier H ("/VERYSILENT que no era silencioso") en otro
    sitio. El updater manda ademas /SUPPRESSMSGBOXES (Core/InstallScope.SilentInstallArguments). }
  if (not WizardSilent) and (not IsOfficeInstalled) then
  begin
    MsgBox('ADVERTENCIA: No se detect' + #243 + ' Microsoft Office instalado en este equipo.' + #13#10 + #13#10 +
           'OfiConvert requiere Microsoft Office (Word, Excel y/o PowerPoint) ' +
           'instalado para funcionar correctamente.' + #13#10 + #13#10 +
           'Puede continuar con la instalaci' + #243 + 'n, pero la aplicaci' + #243 + 'n no funcionar' + #225 + ' ' +
           'hasta que instale Microsoft Office.', mbInformation, MB_OK);
  end;
end;
