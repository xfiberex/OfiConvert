# Contexto del proyecto — OfiConvert

> **Qué es este archivo.** El contexto **vivo** del proyecto: qué es, cómo está montado, qué se decidió
> y por qué, y qué pasó en cada versión. Sirve para retomar el trabajo sin releer el código (y sin
> repetir errores ya pagados por este proyecto o por sus hermanos). **Mantenerlo con cada cambio
> relevante:** actualizar §3 _Estado actual_ y añadir una entrada al _Registro de cambios_ (fecha
> absoluta). Commitearlo junto al cambio.
>
> Reparto con [`ROADMAP.md`](ROADMAP.md): allí, **qué se va a hacer** (tiers pendientes); aquí,
> **qué hay hecho, cómo y por qué**.
>
> **Proyectos hermanos:** [FormatDiskPro](https://github.com/xfiberex/FormatDiskPro) y
> [WingetUSoft](https://github.com/xfiberex/WingetUSoft) (mismo autor, mismo stack, ambos TERMINADOS).
> Gran parte de la hoja de ruta consiste en **portar su infraestructura ya probada**; sus `CONTEXT.md`
> documentan los tropiezos que aquí no hay que repetir.

| | |
|---|---|
| **Repositorio** | https://github.com/xfiberex/OfiConvert |
| **Versión publicada** | **2.2.0** (2026-07-13) — Tier C. Instalador sin firmar, **con `.sha256`** |
| **En `main`, sin publicar** | *(nada)* |
| **Estado** | Funcional; hoja de ruta **ABIERTA** — Tiers A, B y C ✅, quedan D–F |
| **Stack** | C# / .NET 10 · **WinUI 3** (Windows App SDK **1.8.260317003**, unpackaged, `net10.0-windows10.0.22621.0`, mín. 10.0.19041.0) · COM Interop (Office) + LibreOffice CLI · Serilog · **xUnit** · Inno Setup 6 |
| **Licencia** | **MIT** ([`LICENSE`](LICENSE)) |
| **Pruebas** | **11** (10 + 1 de red, que se omite salvo `OFICONVERT_NETWORK_TESTS=1`) — solo cubren el updater; el resto es el Tier D |
| **Hoja de ruta** | [`ROADMAP.md`](ROADMAP.md) — abierta, plan por tiers |
| **Última actualización** | 2026-07-13 |

---

## 1. Qué es

Conversor de escritorio **por lotes** de documentos de Microsoft Office (Word, Excel, PowerPoint) a
**PDF, HTML, CSV, PNG y JPG**. Motor principal: **automatización COM de Office**; motor alternativo:
**LibreOffice** en modo headless. Cola con pausa/reanudación/cancelación, conversiones **paralelas**
con límite configurable, **reintentos** con espera exponencial, validación previa de archivos (corruptos,
con contraseña, bloqueados), historial exportable (CSV/TXT), minimización a bandeja, menú contextual del
Explorador, **8 idiomas**, tema claro/oscuro/sistema y aviso de actualización vía GitHub Releases.

Corre **sin elevación** (`asInvoker`) y su ventana es **redimensionable**. No gestiona discos ni
paquetes (eso es territorio de FormatDiskPro y WingetUSoft).

---

## 2. Arquitectura

```
OfiConvert/                    (la app vive en la RAÍZ del repo; no hay src/ — ojo con los globs, §4 Pruebas)
├─ Program.cs                  Main propio (DISABLE_XAML_GENERATED_MAIN) + INSTANCIA ÚNICA (redirección)
├─ App.xaml(.cs)               Activaciones (propias y redirigidas) → encola archivos; Serilog; crash.log
├─ MainWindow.xaml(.cs)        Ventana única: cola, formatos, ajustes, updater (InfoBar), bandeja, Mica
├─ ViewModels/
│  └─ MainViewModel.cs         Orquestación completa: cola, conversión paralela + pausa + reintentos,
│                              ajustes, historial, persistencia de la cola
├─ Services/
│  ├─ OfficeFileConversionService.cs   Motor principal: COM late binding (Word/Excel/PowerPoint)
│  ├─ LibreOfficeConversionService.cs  Motor alternativo: soffice --headless --convert-to
│  ├─ FileValidationService.cs         Magic bytes OLE/ZIP: corrupto, con contraseña, bloqueado, vacío
│  ├─ GitHubUpdateService.cs           Releases + descarga + VERIFICACIÓN (Authenticode → SHA-256)
│  ├─ ConversionHistoryService.cs      history.json (máx. 1000) + export CSV (fórmulas neutralizadas) y TXT
│  ├─ SettingsService.cs               settings.json, validado al cargar
│  ├─ QueuePersistenceService.cs       queue.json: la cola sobrevive al cierre (filtra UNC/relativas/inexistentes)
│  ├─ ShellIntegrationService.cs       Menú contextual del Explorador (HKCU, por extensión)
│  ├─ ThumbnailService.cs              Miniaturas del shell para la lista
│  ├─ DialogService.cs                 Pickers de archivo/carpeta y diálogos
│  └─ LoggingService.cs                Serilog → %AppData%\OfiConvert\logs (diario, 30 días, 10 MB)
├─ Models/                     FileItem, ConversionOptions/Result/Progress, OutputFormat(+Helper),
│                              OfficeFormats (extensiones admitidas: fuente única), AppSettings…
├─ Helpers/
│  ├─ LocalizationService.cs   8 idiomas (ES/EN/PT/FR/DE/IT/ZH/JA) — ver §4 Localización
│  ├─ AppPaths.cs              Rutas de %AppData%\OfiConvert (fuente única) + volcado del crash.log
│  ├─ ActivationArguments.cs   Línea de comandos de una activación → archivos Office (lógica PURA)
│  └─ Notifier.cs              Aviso al terminar: sonido + parpadeo en la barra de tareas (Win32)
├─ Behaviors/ · Converters/    Drag & drop de archivos · converters de binding
├─ Lang/*.xaml                 Diccionarios de idioma (viajan junto al .exe, parseados en runtime)
├─ Assets/app.ico
├─ installer/
│  ├─ OfiConvert.iss           Inno Setup (la versión se la inyecta el script; no editarla a mano)
│  └─ build-installer.ps1      Publish self-contained → instalador → .sha256 (+ firma opcional)
├─ tests/OfiConvert.Tests/     xUnit: verificación del updater (servidor HTTP local + release real)
└─ release.ps1                 Corte de versión en un paso (build + tests + instalador + GitHub Release)
```

**Regla de oro de los hermanos, aquí PENDIENTE:** no existe `Core/`. La lógica pura y testeable
(rutas de salida seguras, formateo de bytes, mapeo de formatos, sanitización CSV, comparación de
versiones) vive mezclada en `Services/` y `MainViewModel`. Extraerla es la primera fase del Tier D.

---

## 3. Estado actual

| | |
|---|---|
| Build | `dotnet build OfiConvert.slnx -c Release`: **0 errores / 0 advertencias** |
| Pruebas | **10 pasan · 1 se omite · 0 fallan** (`dotnet test`). Solo cubren el updater |
| Publicado | **v2.2.0** (la 2.1.0 fue el primer corte con `release.ps1`; ambas con instalador + `.sha256`) |
| Updater | **Verifica** el instalador antes de ejecutarlo (Authenticode → SHA-256) |
| Pendiente de release | *(nada)* |

**Tiers** (detalle en [`ROADMAP.md`](ROADMAP.md))

| Tier | Tema | Estado |
|---|---|---|
| 0 | Docs vivos (`CONTEXT.md` + `ROADMAP.md`) | ✅ |
| **A** | **Higiene: bugs de la auditoría, README real, `LICENSE`, build 0/0** | ✅ |
| **B** | **Pipeline de release (instalador scriptado, `.sha256`)** | ✅ |
| **C** | **Verificar el instalador antes de ejecutarlo** | ✅ |
| D | Pruebas (extraer `Core/`, cobertura real, UI tests FlaUI) | ⬜ |
| E | Cara pública (README de usuario, capturas, legal in-app) | ⬜ |
| F | Infraestructura agéntica | ⬜ |

---

## 4. Decisiones y convenciones clave

### Producto

- **`asInvoker` (sin elevación):** nada de lo que hace requiere administrador. Coincide con
  WingetUSoft — los futuros UI tests **no** necesitarán terminal elevada.
- **Motor de conversión dual.** Office COM es el principal; **LibreOffice** (`soffice --headless
  --convert-to`) entra en dos casos: si Office no está instalado, o como último recurso cuando Office
  agota los reintentos. La app funciona sin Office si hay LibreOffice; sin ninguno de los dos, error
  claro antes de empezar.
- **Ventana única redimensionable** (tamaño inicial 1050×800 px), title bar extendida y backdrop Mica.
- **Bandeja del sistema** (H.NotifyIcon.WinUI): con la opción activa, cerrar minimiza a la bandeja.
- **Cierre protegido:** con una conversión en curso, cerrar pide confirmación y cancela primero —
  los procesos de Office huérfanos son EL riesgo de esta app.
- **INSTANCIA ÚNICA (desde el Tier A).** La app se registra con `AppInstance.FindOrRegisterForKey`; una
  segunda invocación (el menú contextual del Explorador con la app ya abierta) **redirige su activación
  a la primera y se cierra sin abrir ventana**. Es lo correcto para una app con **cola persistente**:
  seleccionar 5 archivos en el Explorador los encola todos en la MISMA ventana, y no hay 5 procesos
  peleándose por el mismo `queue.json`. Detalles de implementación en §4 *Activación*.
- **Aviso al terminar = sonido + parpadeo de la barra de tareas**, y **solo si la ventana no está en
  primer plano** (`Helpers/Notifier`). Si está delante, el panel de resultados ya lo dice y no se
  interrumpe a nadie. **No se usa un toast de Windows**, por la misma razón que los hermanos lo
  descartaron: en una app *unpackaged* exige registrar un servidor COM del `AppNotificationManager`,
  mucha fontanería para un beneficio marginal. Antes esto era un `ContentDialog` **modal**, pese a que
  sus claves de localización se llamaban `TrayNotif*`.

### Conversión COM (no romper)

- **Late binding puro** (`Type.GetTypeFromProgID` + `InvokeMember`), sin PIAs ni ensamblados de
  interop: funciona con cualquier Office de escritorio instalado (detección por registro: ClickToRun,
  16.0/15.0/14.0; fallback al ProgID). A cambio no hay tipado — los números mágicos van comentados
  (`wdExportFormatPDF = 17`, `xlCSV = 6`, `ppSaveAsPDF = 32`…).
- **Documentos SIEMPRE en solo lectura y con macros forzosamente deshabilitadas**
  (`AutomationSecurity = 3`, msoAutomationSecurityForceDisable): es la protección anti-macro que
  promete el README, aplicada en Word, Excel y PowerPoint.
- **Limpieza COM estricta:** `Close`/`Quit` + `Marshal.FinalReleaseComObject` + doble `GC.Collect()`.
  Sin eso quedan `WINWORD.EXE`/`EXCEL.EXE` zombis tras cada lote.
- **PowerPoint no acepta `Visible = false`** (lanza excepción): se abre con `WithWindow:=False` y se
  ocultan sus ventanas después (`HidePowerPointWindows`). Por eso sus llamadas van en `try/catch`
  individuales.
- Excel→CSV exporta **una hoja** (la activa, o la indicada en `ConversionOptions.SheetNames`);
  PPT→PNG/JPG exporta **todas las diapositivas** a una subcarpeta con el nombre del archivo.

### Activación (menú contextual del Explorador) — trampas ya pagadas

- **`RedirectActivationToAsync` NO se puede esperar desde el hilo STA de `Main`:** la redirección
  necesita bombear mensajes COM y el `await` se bloquea contra sí mismo (la app se queda colgada sin
  abrir nada). Se despacha a un hilo del pool y se espera con un semáforo — es el patrón del sample
  oficial del Windows App SDK, y por eso ese código parece más raro de lo que debería.
- **En una app unpackaged, los argumentos de la activación llegan como UNA cadena que incluye la ruta
  del propio `.exe`** como primer token (en la empaquetada, no). Por eso `ActivationArguments` **no**
  descarta el primer token a ciegas: tokeniza respetando comillas (las rutas del Explorador vienen
  entrecomilladas y casi siempre llevan espacios) y filtra por **extensión admitida + el archivo
  existe** — el `.exe` se cae solo con ese filtro.
- El evento `AppInstance.Activated` **llega en un hilo del pool**, no en el de la UI: hay que volver al
  `DispatcherQueue` antes de tocar el ViewModel.

### Seguridad

- **Salida confinada a la carpeta elegida:** `GetSafeOutputPath` normaliza con `GetFullPath` y
  **rechaza** rutas que escapen de la carpeta destino; si el archivo existe, renombra `archivo (1).pdf`
  en vez de sobrescribir.
- **CSV del historial con fórmulas neutralizadas** (`=`/`+`/`-`/`@`/TAB/CR → prefijo `'`) además del
  escape de comillas — el mismo criterio que los hermanos añadieron como tier de seguridad; aquí nació
  implementado (`ConversionHistoryService.SanitizeCsvField`).
- **LibreOffice:** las rutas con `"` se rechazan antes de interpolarlas en los argumentos del proceso.
- **Validación previa a convertir** (`FileValidationService`): magic bytes (OLE vs ZIP), detección de
  contraseña (OpenXML cifrado se presenta como OLE, o como ZIP sin `[Content_Types].xml`), archivo
  bloqueado o vacío. Falla ANTES de lanzar Office.
- **VERIFICACIÓN DEL INSTALADOR (desde el Tier C) — NO ROMPER.** `GitHubUpdateService` **no ejecuta
  nada que no haya verificado**: firma Authenticode válida → OK; si no la hay, **SHA-256** contra el
  asset `*.exe.sha256` del release. Sin ninguna de las dos, **borra el archivo y aborta**.
  Consecuencias operativas:
  - **Todo release debe subir su `.sha256`** (o ir firmado) o los clientes **rechazarán** la
    actualización. `build-installer.ps1` lo genera y `release.ps1` lo sube y aborta si falta.
  - **La descarga vive en su propio método** (`DownloadToFileAsync`) **a propósito**: su `FileStream` es
    `FileShare.None` y debe cerrarse **antes** de verificar. Si se fusiona con la verificación, esta no
    podrá ni abrir el archivo («lo está usando otro proceso» — el proceso es la propia app) y la
    actualización fallará **siempre**. Le pasó a WingetUSoft: dejó su auto-actualización muerta durante
    dos versiones. Hay tests que lo cazan.
  - **Alcance honesto:** el `.exe` y su hash salen del mismo release → detecta corrupción y manipulación
    **en tránsito**, no un compromiso de la cuenta de GitHub. La firma sigue siendo el objetivo.

### Build y publicación

- **Self-contained** (`SelfContained` + `WindowsAppSDKSelfContained`): el usuario final no instala
  runtimes (decidido 2026-04-03; desviación deliberada de WingetUSoft, que publica framework-dependent
  con descarga de runtimes en su instalador).
- **`Microsoft.WindowsAppSDK` con versión EXACTA (`1.8.260317003`)** — lección de FormatDiskPro: con
  comodín (`1.8.*`), el conjunto de archivos publicados cambia solo con la fecha y rompe el build de
  forma diferida. Subir de versión debe ser deliberado y probado.
- **Workarounds del publish WinUI 3 unpackaged — NO QUITAR** (targets del `.csproj`):
  - `CopyXamlResourcesToPublish` copia el `.pri` propio y los `.xbf`: sin el `.pri`, la app publicada
    **crashea al iniciar** (FormatDiskPro lo pagó con su 1.2.0).
  - `CopyLangFilesToPublish` copia `Lang/*.xaml`: el tooling de WinUI 3 **filtra**
    `CopyToPublishDirectory`, y sin este target la app publicada arrancaría sin idiomas.
- El `.csproj` declara `win-x64` y `win-arm64`, pero solo se publica x64
  (`ArchitecturesAllowed=x64compatible` en el `.iss`).
- **Instalador (Inno Setup):** `AppId={{B2E8F4A1-3C7D-4E9F-A1B2-6D8E0F3C5A7B}` — **no cambiar nunca**
  (permite la actualización in-place). `PrivilegesRequired=lowest` → instalación **per-user** en
  `%LocalAppData%\Programs` por defecto (el diálogo permite elevar) — **distinto de los hermanos**, que
  van con `admin`. Avisa si no detecta Office, pero deja instalar (LibreOffice puede cubrir).
  `CloseApplications=yes`; el flujo silencioso del updater pasa `/VERYSILENT /NORESTART /autoinstall=1`
  y el `[Run]` con `Check: IsAutoUpdate` relanza la app.
- **La versión tiene UNA fuente: el `.csproj`** — y hay que subir **las TRES etiquetas** (`<Version>`,
  `<AssemblyVersion>`, `<FileVersion>`), cosa que `release.ps1` hace de golpe. **El updater compara el
  tag del release contra `Assembly.GetName().Version`, que sale de `<AssemblyVersion>`:** si esa se
  queda atrás, la app publicada se cree más vieja de lo que es y se ofrece a sí misma, en bucle, una
  actualización que ya tiene. (WingetUSoft lo cazó justo antes de su primer corte.)
  `build-installer.ps1` inyecta la versión en el `.iss` con `/DMyAppVersion`; el `#define` que queda en
  el archivo es **solo un respaldo para abrirlo en el IDE de Inno Setup**, y envejece.
- **`build-installer.ps1` verifica el publish antes de empaquetar:** que estén `OfiConvert.exe`, el
  **`.pri`** y los **8 idiomas**. Si un cambio de SDK rompiera alguno de los dos targets del `.csproj`,
  el instalador se generaría "bien" y la app **crashearía al iniciar** en el equipo del usuario (sin el
  `.pri`, WinUI no resuelve el XAML) o abriría sin traducciones. Mejor romper el corte que el equipo
  del usuario.
- **`[Files]` del `.iss` ya NO lleva `skipifsourcedoesntexist`:** con esa bandera, un publish ausente o
  vacío producía un instalador que se compilaba sin quejarse… **y no llevaba la aplicación dentro**.

### Pruebas

- **Framework: xUnit** (el estándar de la casa). `tests/OfiConvert.Tests`, en la solución.
- **El `.csproj` de la app vive en la RAÍZ**, así que su glob por defecto (`**/*.cs`) **se tragaba los
  archivos de `tests/`** y el build reventaba con errores absurdos (`Fact` no encontrado *dentro de la
  app*). Por eso el `.csproj` lleva `<Compile Remove="tests\**" />`. Si algún día se añade otra carpeta
  de proyectos, hay que excluirla igual.
- `GitHubUpdateService` es `internal`; `AssemblyInfo.cs` abre los internals **solo** a `OfiConvert.Tests`
  (`InternalsVisibleTo`). No es API pública, pero es el código que decide si se ejecuta un `.exe` bajado
  de internet: tiene que ser comprobable.
- **Las pruebas del updater ejercen la DESCARGA COMPLETA** contra un servidor HTTP local
  (`LocalHttpServer`), no solo el cálculo del hash. Es deliberado: en WingetUSoft las pruebas cubrían el
  hash pero **nunca la descarga**, y por eso se les coló el bug del archivo que se bloqueaba a sí mismo.
- **`LocalHttpServer` va sobre `TcpListener`, no `HttpListener`:** este último exige en Windows reservar
  la URL o correr **como administrador**, y convertiría unas pruebas normales en pruebas que solo pasan
  en terminal elevada.
- **`[NetworkFact]` — omitir ≠ fallar.** El test que verifica el **release real publicado** descarga
  ~58 MB de GitHub y se **omite** salvo `OFICONVERT_NETWORK_TESTS=1`. *Un test omitido dice «no hay red /
  no se ha pedido»; uno fallido dice «la app está rota».* Confundirlos es lo que hizo que FormatDiskPro
  no pudiera meter sus UI tests en el pipeline durante meses.
- **Se comprobó que los tests FALLAN** al desactivar la verificación (2 de 10 en rojo). Un test que
  nunca ha fallado no prueba nada.

### Trampas de PowerShell 5.1 (las tres las pagaron los hermanos; aquí entraron ya resueltas)

- **Los `.ps1` van con BOM UTF-8.** Sin él, PS 5.1 los lee con la página de códigos ANSI y los acentos
  o un `—` **rompen el tokenizer** ("Falta el paréntesis de cierre"), con un error que no señala la
  causa.
- **El `.csproj` NO se lee con `Get-Content -Raw`.** Sin BOM, PS 5.1 lo lee en ANSI: los bytes UTF-8 de
  `é` se vuelven `Ã©` y, al reescribirlo, la corrupción **queda grabada**. Como el bump ocurre en
  **cada** release, el daño se acumula capa sobre capa — a FormatDiskPro le destrozó el nombre del
  autor en `<Authors>`/`<Copyright>`, y de ahí en las **propiedades del `.exe` publicado**, a lo largo
  de 14 versiones sin que nadie lo viera. `release.ps1` usa `[System.IO.File]::ReadAllText` (detecta el
  BOM) y reescribe **conservándolo**. Cualquier script que toque el `.csproj` debe hacer lo mismo.
- **git + salida capturada = trampa.** git escribe por stderr en su operación **normal** (el resumen
  del `push`, los avisos de CRLF), sin que nada falle. Si la salida del script **se captura**
  (`| Tee-Object`, `2>&1 |`, un wrapper), PS 5.1 convierte cada línea de stderr de un exe nativo en un
  `NativeCommandError` y, con `$ErrorActionPreference = "Stop"`, **aborta aunque git devuelva 0**. En
  un `push` eso deja el release **a medias**: rama subida, sin tag ni GitHub Release. Por eso los git
  que mutan estado van por **`Invoke-Git`**, que baja la preferencia mientras corre git y decide por
  `$LASTEXITCODE`.

### MVVM

- **`[ObservableProperty]` va sobre PROPIEDADES PARCIALES, nunca sobre campos** (`public partial string
  X { get; set; }`). Sobre un campo, el código generado **no es AOT-compatible en WinUI 3**
  (MVVMTK0045: CsWinRT no puede producir el marshalling WinRT). Consecuencias:
  - **`CommunityToolkit.Mvvm` debe ser ≥ 8.4.2.** La **8.4.0 ignora las propiedades parciales en
    silencio** —sin error ni diagnóstico propio— y el build muere con 33 × `CS9248` ("la propiedad
    parcial debe tener una parte de implementación"), que apunta al síntoma y no a la causa. No bajar
    de 8.4.2.
  - **Una propiedad parcial no admite inicializador:** los valores por defecto van al **constructor**
    (`MainViewModel`, `FileItem`, `ConversionOptions`).
- **`LoadSettings` corre con `_isLoadingSettings = true`.** Al pasar los defaults de campo al
  constructor, cada asignación dispara su `OnXChanged` → `SaveSettings`, y eso **escribía en disco el
  estado a medio cargar**: el guardado disparado por `SelectedTheme` llevaba todavía el
  `DefaultOutputFormat` y el `LastOutputFolder` por defecto, **pisando los del usuario**. El guardado
  se ignora mientras se carga (y de paso desaparecen 7 escrituras redundantes en cada arranque).

### Localización

- **8 idiomas** (ES/EN/PT/FR/DE/IT/ZH/JA) en `Lang/*.xaml`, parseados **en runtime** con `XDocument`
  (no son ResourceDictionary compilados) y refrescados por binding al indexer
  (`{Binding [Clave], Source={StaticResource Loc}}`). Si falta el archivo del idioma, cae a `es-ES`.
- **`LocalizationService.SupportedLanguages` es la fuente única.** `SettingsService` valida contra ella:
  antes tenía su propia lista `("es" or "en")` y **reseteaba a español los otros seis** al cargar —
  elegir francés funcionaba hasta reiniciar. Al añadir un idioma, tocar **solo** esa lista.
- ⚠️ **El indexer devuelve la propia clave si no la conoce** — la misma trampa que el `L.T` de los
  hermanos: un typo no rompe el build ni nada visible salvo texto raro en la UI. El test de completitud
  (cada clave en los 8 archivos + cada clave usada en el código existe) es parte del Tier D.
- **No hay detección del idioma del sistema:** arranca en español salvo ajuste guardado.

### Datos del usuario

- Todo en `%AppData%\OfiConvert\` — **las rutas salen de `Helpers/AppPaths`, fuente única**:
  `settings.json` (validado al cargar con `Math.Clamp`), `history.json` (máx. 1000 entradas),
  `queue.json` (la cola sobrevive al cierre; al cargar filtra rutas no absolutas, UNC e inexistentes),
  `crash.log` y `logs\` (Serilog diario, 30 días, 10 MB por archivo).
- **El lote de conversión se FIJA al empezar** (`var batch = SelectedFiles.ToList()`). La cola sigue
  viva mientras corre: se pueden soltar archivos nuevos, o llegar por el menú contextual, y **no entran
  en el lote en curso ni se tocan al acabar**. Antes se iteraba y se limpiaba `SelectedFiles`
  directamente, así que un archivo añadido a mitad de un lote **se borraba sin convertir**.
- Límite de entrada: archivos de hasta **500 MB** (`MaxFileSizeBytes`).

---

## 5. Tareas comunes

| Tarea | Comando |
|-------|---------|
| Compilar | `dotnet build OfiConvert.slnx -c Release` |
| Ejecutar | `dotnet run --project OfiConvert.csproj` |
| Pruebas | `dotnet test tests\OfiConvert.Tests\OfiConvert.Tests.csproj` |
| Pruebas **con red** (verifica el release real de GitHub) | `$env:OFICONVERT_NETWORK_TESTS = "1"; dotnet test …` |
| Instalador | `.\installer\build-installer.ps1` (`-CertThumbprint <huella>` para firmar) |
| **Publicar versión** | `.\release.ps1 -Version X.Y.Z` (`-DryRun` para simular) |

`release.ps1` hace: validar → compilar (+ pruebas, cuando existan) → bump de `<Version>`,
`<AssemblyVersion>` y `<FileVersion>` → instalador (+ `.sha256`) → commit + tag `vX.Y.Z` → push →
`gh release create` con **los dos assets**. Flags: `-DryRun`, `-SkipTests`, `-AllowDirty`, `-NotesFile`
y los de firma.

> **`-DryRun` sí compila el instalador** (con el bump aplicado, para que lo que se prueba sea lo que
> se publicaría) y **revierte el `.csproj` al salir**. Lo único que no toca es git y GitHub.
>
> **Solo hace `git add -u`**, así que los archivos **nuevos** hay que `git add`earlos antes o el
> release saldría sin ellos.

---

## 6. Pendientes

El plan por tiers está en [`ROADMAP.md`](ROADMAP.md).

1. **Cobertura de pruebas casi nula:** las 11 que hay cubren **solo el updater**. La conversión, la
   validación de archivos, las rutas de salida seguras y la localización no tienen ninguna *(Tier D)*.
2. **`THIRD-PARTY-NOTICES.txt` y los textos legales in-app** siguen sin existir *(Tier E)*.
3. **El instalador nunca se ha probado end-to-end** (instalación limpia + actualización in-place con el
   flujo silencioso real). FormatDiskPro encontró ahí un fallo con un diálogo modal abierto.
4. ⚠️ **La verificación del Tier C aún no se ha ejercido en producción.** Solo actúa al actualizar
   **desde** una versión ≥ 2.2.0, y los clientes en 2.1.0 llegarán a la 2.2.0 con el código viejo, que
   no verificaba nada. **El primer uso real será 2.2.0 → 2.3.0.** No es trabajo pendiente: es lo que hay
   que vigilar en el próximo corte.

Menores, sin tier asignado:

- **Sin detección del idioma del sistema** en el primer arranque (los hermanos sí la tienen): la app
  abre en español hasta que el usuario elija.
- **`DialogService` tiene textos en duro** (`"Sí"`, `"No"`, `"Aceptar"`, `"Error"`) que no pasan por
  `LocalizationService`.

---

## 7. Cómo mantener este documento

1. Tras un cambio relevante: entrada nueva en el **Registro de cambios** (fecha absoluta) + actualizar §3.
2. Si cambia una convención o decisión, reflejarlo en §4 (es la sección que evita repetir errores).
3. Marcar el ítem como ✅ en [`ROADMAP.md`](ROADMAP.md) cuando esté **verificado** (build + tests +
   prueba real), no cuando esté escrito.
4. Commitear este archivo **junto** con el cambio, para que el contexto viaje con el código.

---

# Registro de cambios

### Índice de versiones

| Versión | Qué trajo |
|---|---|
| **2.2.0** | **Tier C** — el updater **verifica** el instalador antes de ejecutarlo (Authenticode → SHA-256). Primeras pruebas del proyecto (11, xUnit). |
| **2.1.0** | **Tier A** — instancia única + menú contextual que funciona, los 8 idiomas persisten, aviso al terminar sin modal, build 0/0, `LICENSE`, README real. **Tier B** — pipeline de release en un paso (`release.ps1`), instalador scriptado y `.sha256`. |
| **2.0.0** | Migración de WPF a **WinUI 3** (Mica, title bar propia). Post-tag, sin release: publish self-contained, tooling MSIX + idiomas en el publish, progreso de descarga en el updater. |
| **1.0.0** | La app WPF completa: conversión por lotes a 5 formatos, 8 idiomas, historial, cola persistente, bandeja, menú contextual y aviso de actualización vía GitHub. |

---

### 2026-07-13 — Tier C: el updater ya no ejecuta lo que no ha verificado — **v2.2.0**

Era el agujero más serio del proyecto: `GitHubUpdateService` descargaba un `.exe` de internet y **lo
ejecutaba sin comprobar absolutamente nada**. Port del `GitHubUpdateService` de WingetUSoft, con sus
tropiezos ya conocidos. Detalle y consecuencias operativas en §4 *Seguridad*.

**Lo construido.** `VerifyInstallerAsync` (Authenticode → SHA-256 → borrar y abortar),
`VerifyAuthenticodeSignature` (WinVerifyTrust), `ComputeSha256Async`, y `GitHubReleaseInfo` gana
`ChecksumUrl` (el asset `.sha256` que ya publicaba el Tier B). Dos claves nuevas de localización **en los
8 idiomas** (120 claves por archivo). `MainWindow` pasa el `ChecksumUrl` y muestra el motivo del rechazo
en su InfoBar, además de registrarlo en el log.

**Las primeras pruebas del proyecto** (`tests/OfiConvert.Tests`, xUnit): **11**, y ejercen la
**descarga completa** contra un servidor HTTP local, no solo el cálculo del hash — que es exactamente el
punto ciego por el que a WingetUSoft se le coló el bug del archivo que se bloqueaba a sí mismo. Ver §4
*Pruebas*.

**Se comprobó que los tests fallan de verdad:** desactivando la verificación, 2 de 10 se ponen en rojo
(el del instalador manipulado y el del release sin `.sha256`). Un test que nunca ha fallado no prueba
nada.

**El test que cierra la costura entre PowerShell y C#** (`[NetworkFact]`, se omite salvo
`OFICONVERT_NETWORK_TESTS=1`): descarga el **release real de GitHub** y lo verifica con el código real de
la app. El formato del `.sha256` lo escribe un script de PowerShell y lo lee C#: dos mitades que pueden
divergir en silencio, y bastaría un cambio de formato para que **toda** actualización pasara a
rechazarse sin que nadie se enterase hasta el siguiente corte. *Verificado contra la v2.1.0 publicada:
descarga los 58 MB y los valida.*

**Trampa de MSBuild que costó el rato:** el `.csproj` de la app vive en la **raíz**, así que su glob
`**/*.cs` **se tragó los archivos de `tests/`** y el build reventó con errores absurdos (`Fact` no
encontrado *dentro de la app*, y el compilador de XAML abortando sin decir por qué). Se arregla con
`<Compile Remove="tests\**" />`.

**Corrección honesta de camino:** al portar `NormalizeVersion` escribí que sin él la app «se ofrecería a
sí misma la actualización en bucle». **Es falso** — sin normalizar, un tag de 3 tramos compara como
*menor* que el AssemblyVersion de 4, así que el error caía del lado seguro («no hay actualización»). Se
mantiene porque la comparación no decía lo que parecía decir, pero no arreglaba ningún bug vivo.

### 2026-07-13 — Tier B: pipeline de release en un paso — **v2.1.0**

El corte era artesanal: bump de versión **a mano en dos archivos**, compilar el `.iss` desde el IDE de
Inno Setup y subir el instalador al release a mano. Ahora: `.\release.ps1 -Version X.Y.Z`.

**Lo nuevo:** `installer/build-installer.ps1` (publish self-contained → instalador → **`.sha256`**, con
firma opcional) y `release.ps1` (validar → compilar y probar → bump de las tres etiquetas → instalador →
commit + tag → push → GitHub Release con **los dos assets**).

Portado de los hermanos **con sus trampas ya resueltas**, no reescrito: BOM UTF-8 en los `.ps1`, lectura
del `.csproj` conservando el BOM, `Invoke-Git` para el stderr normal de git, y **publish a `%TEMP%` por
MAX_PATH** (aquí aplica igual: el publish self-contained del Windows App SDK trae nombres de hasta 76
caracteres, e ISCC aborta con un error que no dice de qué archivo habla). Ver §4.

**Tres guardas que no estaban en el plan**, todas contra el mismo tipo de fallo —*el corte "sale bien" y
lo que se rompe es el equipo del usuario*—:
- **El publish se verifica antes de empaquetar**: `OfiConvert.exe` + el **`.pri`** + los **8 idiomas**.
  Los dos targets del `.csproj` que copian esos archivos existen porque el tooling de WinUI 3 unpackaged
  no lo hace solo; si un cambio de SDK los rompiera, el instalador se generaría igual y la app
  **crashearía al iniciar** (sin el `.pri`, WinUI no resuelve el XAML — el bug que tumbó la 1.2.0 de
  FormatDiskPro).
- **Fuera `skipifsourcedoesntexist` del `[Files]` del `.iss`**: con esa bandera, un publish ausente o
  vacío producía un instalador que compilaba sin quejarse **y no llevaba la aplicación dentro**.
- **Las tres etiquetas de versión suben juntas.** El updater compara contra `<AssemblyVersion>`: dejarla
  atrás haría que la app se ofreciese a sí misma, en bucle, una actualización que ya tiene.

**Verificado** con `.\release.ps1 -Version 2.1.0 -DryRun`: compila el instalador **real** (58,1 MB), lo
verifica, genera el `.sha256` y **revierte el `.csproj`** al salir sin dejar rastro ni corrupción de
codificación. Lo único que no se ha ejercido todavía es el tramo de git/GitHub, que solo corre en un
corte real.

### 2026-07-13 — Tier A: higiene y bugs reales — **v2.1.0**

Los 8 hallazgos de la auditoría, cerrados salvo el del updater (que necesita el pipeline del Tier B).
Build: 39 advertencias → **0/0**. **Verificado conduciendo la app real**, no solo compilando.

**1 — Los 6 idiomas que no persistían.** `SettingsService` tenía su propia lista de idiomas válidos
(`"es" or "en"`) y **reseteaba a español los otros seis al CARGAR**: elegir francés funcionaba hasta
reiniciar. Peor: el reset disparaba un guardado, así que el arranque **pisaba en disco** la elección del
usuario sin que este tocara nada. Ahora la fuente única es `LocalizationService.SupportedLanguages`.
*Verificado:* sembrando `Language: "fr"` en el `settings.json` real, arrancando y cerrando la app — el
`fr` **sobrevive** al arranque y al cierre.

**2 — El menú contextual del Explorador no hacía nada.** `ShellIntegrationService` registraba
`"OfiConvert.exe" "%1"` y `App` guardaba los `args`… **sin usarlos nunca**: la app abría vacía. Ahora hay
**instancia única con redirección** (`AppInstance`), que es lo correcto para una app con cola persistente
—seleccionar 5 archivos en el Explorador los encola en la MISMA ventana, en vez de abrir 5 procesos
peleándose por el mismo `queue.json`—. Las dos trampas de plataforma que costó (el `await` que se
autobloquea en el hilo STA, y la ruta del `.exe` colada en los argumentos de una app unpackaged) están
en §4 *Activación*. *Verificado de punta a punta:* con la app abierta, lanzar el `.exe` con la ruta de un
`.docx` (con espacios) → la segunda instancia **sale con código 0 sin abrir ventana**, sigue habiendo
**un solo proceso**, y el `queue.json` de la instancia viva **contiene el archivo**.

**3 — Aviso al terminar: se acabó el modal.** Con `ShowNotifications` activo, terminar un lote abría un
`ContentDialog` **modal** — aunque sus claves se llamaban `TrayNotif*`. Ahora `Helpers/Notifier` hace
sonido + parpadeo de la barra de tareas, y **solo si la ventana no está en primer plano**. El toast se
descarta por lo mismo que en los hermanos (servidor COM en app unpackaged). Ver §4 *Producto*.

**4 — Build 0/0 (39 × MVVMTK0045).** `[ObservableProperty]` sobre campos genera código **no
AOT-compatible en WinUI 3**; migrado a propiedades parciales en `MainViewModel`, `FileItem` y
`ConversionOptions`. **La migración no compilaba con `CommunityToolkit.Mvvm` 8.4.0: esa versión ignora
las propiedades parciales EN SILENCIO** (sin error ni diagnóstico), y el build muere con 33 × `CS9248`,
que apunta al síntoma y no a la causa. Subido a **8.4.2**. Ver §4 *MVVM*.

**5 — Bug latente encontrado al migrar: los defaults pisaban los ajustes del usuario.** Al pasar los
valores por defecto al constructor (una propiedad parcial no admite inicializador), cada asignación
dispara `OnXChanged` → `SaveSettings`, que **escribía en disco el estado a medio cargar**: el guardado
disparado por `SelectedTheme` llevaba aún el `DefaultOutputFormat` y el `LastOutputFolder` por defecto.
Resuelto con el guardia `_isLoadingSettings` (y desaparecen 7 escrituras redundantes por arranque).

**6 — Bug latente, este ya presente en producción: los archivos añadidos a mitad de un lote se
borraban.** `PerformConversionAsync` iteraba y limpiaba `SelectedFiles` **directamente**, y `AddFiles`
no está protegido durante la conversión: soltar un archivo mientras corría un lote que terminaba sin
errores lo **eliminaba de la cola sin convertirlo**. Afectaba al *drag & drop* de siempre, no solo al
menú contextual nuevo. El lote ahora se **fija al empezar** y al acabar solo se retiran sus archivos.

**Además:** `LICENSE` (MIT) — el README lo prometía y el archivo no existía · README reescrito (describía
el stack **WPF** de la v1.0, con paquetes y comandos que ya no eran ciertos) · `<Description>` del
`.csproj` ("a PDF" → los 5 formatos) · `crash.log` se muda a `%AppData%` · rutas de datos y extensiones
admitidas unificadas en `Helpers/AppPaths` y `Models/OfficeFormats` (estaban duplicadas en 4 y 3 sitios).

### 2026-07-13 — Auditoría de infraestructura y nacimiento de los docs vivos

Comparación sistemática con **FormatDiskPro** y **WingetUSoft** (ambos TERMINADOS) para decidir qué
infraestructura portar. Se crean **este `CONTEXT.md`** y [`ROADMAP.md`](ROADMAP.md) con el plan por
tiers (0 y A–F).

- Verificado contra el código y el build real: 0 errores / 39 advertencias MVVMTK0045; releases
  v1.0.0 y v2.0.0 publicados en GitHub; 3 commits en `main` posteriores al tag sin release.
- **8 hallazgos** documentados en §6. Los tres de mayor impacto: el updater ejecuta el instalador
  **sin verificarlo**, 6 de los 8 idiomas **no persisten** al reiniciar, y el menú contextual del
  Explorador registra `"%1"` que la app **ignora**.
- Ya cubierto sin deberlo a los hermanos: la exportación CSV **neutraliza fórmulas** desde el origen
  (ellos lo añadieron a posteriori como tier de seguridad).

### 2026-04-03 — v2.0.0: migración de WPF a WinUI 3

`MainWindow` se reescribe en WinUI 3 (Windows App SDK 1.8, unpackaged): backdrop **Mica**, title bar
extendida, mismos servicios por debajo. Equivale al salto WinForms→WinUI que FormatDiskPro dio en su
1.2.0 — incluidos los workarounds de publish que aquel proyecto descubrió (el `.pri` que `dotnet
publish` no copia).

**Después del tag, sin release (3 commits):** publish self-contained con instalador simplificado
(copia `publish\*` recursivo); `EnableMsixTooling` y copia de `Lang/*.xaml` al publish (el tooling
WinUI los filtraba); progreso e indicación de errores en la descarga del updater.

### 2026-04-02 — v1.0.0: primer release publicado

Aviso de actualización multilingüe con consulta a GitHub Releases (`releases/latest`, comparación de
`Version` contra el `AssemblyVersion` en ejecución). Instalador Inno Setup compilado y subido a mano.

### 2026-04-01 — El grueso de la app en tres commits

Núcleo de conversión (COM + fallback LibreOffice), historial, mejoras de UI y temas, y salto de 4 a
**8 idiomas** (se añaden IT/JA/PT/ZH). El README se actualizó a los formatos nuevos — y quedó
congelado en el stack WPF de entonces (ver §6.4).

### 2026-03-30 — first commit

App WPF: WPF-UI 4.1.0 (Fluent), CommunityToolkit.Mvvm, Microsoft.Xaml.Behaviors.Wpf, patrón MVVM.
