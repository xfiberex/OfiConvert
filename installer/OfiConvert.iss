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

; El aviso de "sin motor de conversion" (TJ-12). Vive aqui, y no como literal en [Code], porque el
; instalador habla SEIS idiomas y hasta ahora lo soltaba en espanol en todos.
;
; Ojo con el texto: NO puede decir "hace falta Microsoft Office". La app convierte con Office o con
; LibreOffice, y decirle a quien usa LibreOffice que su instalacion no va a funcionar es mentirle sobre
; el producto. %n es el salto de linea de Inno.
[CustomMessages]
spanish.NoEngineTitle=No se ha detectado ningún motor de conversión
spanish.NoEngineBody=OfiConvert convierte documentos automatizando Microsoft Office (de escritorio) o LibreOffice, y no se ha encontrado ninguno de los dos.%n%nPuede continuar: la aplicación se instalará correctamente, pero no podrá convertir hasta que instale uno de ellos.
english.NoEngineTitle=No conversion engine detected
english.NoEngineBody=OfiConvert converts documents by automating Microsoft Office (desktop) or LibreOffice, and neither one was found.%n%nYou can continue: the application will install correctly, but it will not be able to convert until you install one of them.
portuguese.NoEngineTitle=Nenhum mecanismo de conversão detectado
portuguese.NoEngineBody=O OfiConvert converte documentos automatizando o Microsoft Office (desktop) ou o LibreOffice, e nenhum dos dois foi encontrado.%n%nVocê pode continuar: o aplicativo será instalado corretamente, mas não poderá converter até que você instale um deles.
french.NoEngineTitle=Aucun moteur de conversion détecté
french.NoEngineBody=OfiConvert convertit les documents en automatisant Microsoft Office (bureau) ou LibreOffice, et aucun des deux n'a été trouvé.%n%nVous pouvez continuer : l'application s'installera correctement, mais elle ne pourra pas convertir tant que vous n'aurez pas installé l'un d'eux.
german.NoEngineTitle=Keine Konvertierungs-Engine gefunden
german.NoEngineBody=OfiConvert konvertiert Dokumente, indem es Microsoft Office (Desktop) oder LibreOffice automatisiert. Keines von beiden wurde gefunden.%n%nSie können fortfahren: Die Anwendung wird korrekt installiert, kann aber erst konvertieren, wenn Sie eines davon installieren.
italian.NoEngineTitle=Nessun motore di conversione rilevato
italian.NoEngineBody=OfiConvert converte i documenti automatizzando Microsoft Office (desktop) o LibreOffice, e nessuno dei due è stato trovato.%n%nPuoi continuare: l'applicazione verrà installata correttamente, ma non potrà convertire finché non ne installerai uno.

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

function IsLibreOfficeInstalled: Boolean;
var
  SofficePath: String;
begin
  { El otro motor. Se mira PRIMERO el registro (App Paths de soffice.exe, que LibreOffice registra en
    HKLM) y, si no esta, las rutas de instalacion por defecto en 64 y 32 bits: una instalacion portable o
    en carpeta ajena no aparece en App Paths. Ante la duda se prefiere NO avisar: un aviso de mas sobre
    un equipo que si puede convertir es peor que ninguno. }
  Result := RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\soffice.exe', '', SofficePath);
  if not Result then
    Result := RegQueryStringValue(HKEY_LOCAL_MACHINE,
      'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\soffice.exe', '', SofficePath);
  if not Result then
    Result := FileExists(ExpandConstant('{commonpf}\LibreOffice\program\soffice.exe'));
  if not Result then
    Result := FileExists(ExpandConstant('{commonpf32}\LibreOffice\program\soffice.exe'));
end;

procedure InitializeWizard;
begin
  { WizardSilent() NO es opcional: Inno llama a InitializeWizard tambien en /SILENT y /VERYSILENT, y un
    MsgBox se muestra IGUAL salvo que se pase /SUPPRESSMSGBOXES. La auto-actualizacion lanza el instalador
    con la app ya cerrada, asi que sin esta guarda el usuario SIN Office —justo el que la app dice
    soportar con LibreOffice— veia su programa desaparecer y quedarse un dialogo esperando un clic, o la
    actualizacion colgada. Es el mismo fallo del Tier H ("/VERYSILENT que no era silencioso") en otro
    sitio. El updater manda ademas /SUPPRESSMSGBOXES (Core/InstallScope.SilentInstallArguments). }
  { Se avisa solo si NO hay NINGUNO de los dos motores (TJ-12). Antes se miraba unicamente Microsoft
    Office, asi que a quien usa LibreOffice —una configuracion que la app soporta y anuncia— se le decia
    que su instalacion no iba a funcionar. Y el texto salia en espanol en los seis idiomas: ahora vive
    en la seccion CustomMessages.

    OJO al editar esto: una linea de comentario que EMPIECE por corchete la lee ISCC como etiqueta de
    seccion y aborta con "Invalid section tag", aunque este dentro de un comentario y sangrada. }
  if (not WizardSilent) and (not IsOfficeInstalled) and (not IsLibreOfficeInstalled) then
  begin
    MsgBox(CustomMessage('NoEngineTitle') + #13#10 + #13#10 + CustomMessage('NoEngineBody'),
           mbInformation, MB_OK);
  end;
end;
