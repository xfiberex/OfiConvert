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
| **Versión publicada** | **2.6.0** (2026-07-21) — **pase de UX/UI** (3 bugs vistos solo mirando la app, pulido en claro/oscuro), sobre el **Tier H** que trajo la 2.5.0. Instalador sin firmar, **con `.sha256`** |
| **En `main`, sin publicar** | — (al día) |
| **Estado** | Funcional; **hoja de ruta COMPLETADA** — Tiers 0 y A–H ✅ |
| **Stack** | C# / .NET 10 · **WinUI 3** (Windows App SDK **1.8.260317003**, unpackaged, `net10.0-windows10.0.22621.0`, mín. 10.0.19041.0) · COM Interop (Office) + LibreOffice CLI · Serilog · **xUnit** + **FlaUI** · Inno Setup 6 |
| **Licencia** | **MIT** ([`LICENSE`](LICENSE)) — pero **lo que redistribuye NO es todo MIT**: ver §4 *Legal* |
| **Pruebas** | **230**: 200 unitarias (199 + 1 de red, omitida salvo `OFICONVERT_NETWORK_TESTS=1`) + **30 de UI** (FlaUI, contra la app real) |
| **Hoja de ruta** | [`ROADMAP.md`](ROADMAP.md) — **cerrada** |
| **Última actualización** | 2026-07-21 |

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
├─ Core/                       LÓGICA PURA Y TESTEADA (sin UI, sin Process, sin HttpClient, sin COM):
│                              OutputPath (salida confinada + nunca sobrescribe), FileSignature (magic
│                              bytes), CsvField (fórmulas neutralizadas), ByteSize, OfficeFormats +
│                              OutputFormatHelper (mapeo de formatos), LegalText (textos embebidos)
├─ Models/                     Datos: FileItem, ConversionOptions/Result/Progress, OutputFormat (el enum;
│                              su mapeo vive en Core/), AppSettings…
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
├─ tests/OfiConvert.Tests/     xUnit (163): Core/, validación de archivos, activación, localización,
│                              legal, updater (servidor HTTP local + release real)
├─ tests/OfiConvert.UiTests/   FlaUI/UIA3 (21): conducen el .exe REAL. Sin elevación y sin Office
├─ tools/                      capture-screenshots.ps1 (regenera docs/screenshots conduciendo la app)
├─ docs/screenshots/           Las capturas del README (regenerables, no se editan a mano)
├─ THIRD-PARTY-NOTICES.txt     Avisos de terceros — VERIFICADOS uno a uno; embebido en el .exe (§4 Legal)
├─ .claude/ · .agents/ · .mcp.json   Infraestructura agéntica (Tier F): CLAUDE.md, skills, codegraph
└─ release.ps1                 Corte de versión en un paso (build + tests + instalador + GitHub Release)
```

**Regla de oro de los hermanos, aquí ya CUMPLIDA (Tier D):** `Core/` concentra la lógica pura y
testeable que antes vivía mezclada en `Services/` y `MainViewModel`. La frontera: `Core/` no conoce la
UI, ni lanza procesos, ni sale a la red, ni habla COM — por eso se puede probar sin arrancar nada.

---

## 3. Estado actual

| | |
|---|---|
| Build | `dotnet build OfiConvert.slnx -c Release`: **0 errores / 0 advertencias** |
| Pruebas unitarias | **199 pasan · 1 se omite (la de red) · 0 fallan** |
| Pruebas de UI | **30 pasan · 0 fallan** (FlaUI, arrancan la app real) |
| Publicado | **v2.6.0** (2.1.0 → 2.6.0 cortadas con `release.ps1`; todas con instalador + `.sha256`) |
| Updater | **Verifica** el instalador antes de ejecutarlo (Authenticode → SHA-256) |
| Instalador | **Probado de punta a punta** (2026-07-14): instalación limpia, desinstalación y actualización in-place sobre una instalación real |
| Pendiente de release | — (al día) |

**Tiers** (detalle en [`ROADMAP.md`](ROADMAP.md)) — **hoja de ruta cerrada**

| Tier | Tema | Estado |
|---|---|---|
| 0 | Docs vivos (`CONTEXT.md` + `ROADMAP.md`) | ✅ |
| **A** | **Higiene: bugs de la auditoría, README real, `LICENSE`, build 0/0** | ✅ |
| **B** | **Pipeline de release (instalador scriptado, `.sha256`)** | ✅ |
| **C** | **Verificar el instalador antes de ejecutarlo** | ✅ |
| **D** | **Pruebas (`Core/` extraído, UI tests FlaUI)** | ✅ |
| **E** | **Cara pública (README, capturas reproducibles, legal in-app)** | ✅ |
| **F** | **Infraestructura agéntica (`.mcp.json`, `CLAUDE.md`, skills, codegraph)** | ✅ |
| **G** | **UI/UX (3 bugs, comandos que se apagan solos, accesibilidad)** | ✅ |
| **H** | **Instalador end-to-end (el `/VERYSILENT` que no era silencioso)** | ✅ |

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

### UI/UX — trampas y convenciones (pase de 2026-07-21, sobre capturas)

- 🔴 **UN `ContentDialog` NO HEREDA EL TEMA DE LA APP.** `ApplyTheme` fija `RequestedTheme` en `Content`
  (el root), pero un diálogo se enraíza en la **capa de popups**, hermana de `Content`, no dentro: se
  queda en el tema del **sistema**. Con la app en Claro sobre un Windows en Oscuro, **los diálogos salían
  negros**. Todo `ContentDialog` debe recibir `RequestedTheme = RootTheme` a mano (`MainWindow` tiene el
  helper). Afecta a los cuatro: legal, cierre, sin-actualizaciones y actualización.
- **El icono de estado del historial iba en DURO** (tilde verde para TODAS las filas): un fallo se veía
  idéntico a un éxito, sin ruta y sin motivo. Ahora el glifo/color salen de `Success` (converters
  `BoolToStatus*`), la decisión de glifo vive en `Core/HistoryStatus` (pura, la cubre `HistoryStatusTests`)
  y las filas fallidas muestran su `ErrorMessage`. Mismo patrón que el bug del contador de reintentos:
  *un `FontIcon` en duro no rompe el build, solo enseña lo que no debe*.
- **Botones destructivos = OUTLINE, no relleno sólido** (`btnCancel`, `btnClearHistory`, `Desregistrar`):
  rojo en texto y borde, fondo transparente. Un relleno rojo sólido **choca con acentos de sistema
  cálidos** (la app respeta el acento de Windows, no lo fija), y junto a un botón de acento —`Registrar`—
  los dos salían rojos e indistinguibles. El outline se lee como destructivo con **cualquier** acento.
- **Las capturas fijan un acento NEUTRO, la app NO.** La app sigue el acento del sistema; para que las
  imágenes del README no salgan con el acento personal de quien las genera, los scripts de captura ponen
  `OFICONVERT_ACCENT` (azul `#0078D4`) y `App.OnLaunched` lo lee **solo para esto** (gated por env var; en
  producción, no hace nada). Sin esto, el repo mostraba capturas en el rojo del equipo del autor.

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
- 🔴 **`/VERYSILENT` NO ES SILENCIOSO POR SÍ SOLO.** Con `PrivilegesRequiredOverridesAllowed=dialog`, Inno
  planta el cuadro «Seleccione el modo de instalación» **aunque se le pase `/VERYSILENT`** y **se bloquea
  esperando un clic**. Por eso el `.iss` lleva `commandline dialog` y **el updater manda siempre
  `/ALLUSERS` o `/CURRENTUSER`** (`Core/InstallScope`), el que corresponda a **cómo está instalada la app
  ahora** — actualizar no puede cambiarle el alcance al usuario. **No quitar ni el `commandline` ni el
  modificador.** Se descubrió probando el instalador de punta a punta; llevaba cuatro versiones escondido
  porque, al ACTUALIZAR, Inno recuerda el modo anterior y no pregunta.
- **La auto-actualización de una instalación *para todos los usuarios* PIDE UAC**, y eso es inevitable
  (escribe en `Program Files`). Lo que sí se arregló: **si el usuario lo rechaza, la app ya no se cierra**.
  Solo la instalación per-user (la opción por defecto) se actualiza de forma verdaderamente silenciosa.

### Pruebas

- **Framework: xUnit** (el estándar de la casa) + **FlaUI/UIA3** para la UI. Dos proyectos en la
  solución: `tests/OfiConvert.Tests` (152) y `tests/OfiConvert.UiTests` (18).
- **`release.ps1` ejecuta TODOS los `.csproj` bajo `tests\`**, descubriéndolos solo. Un proyecto de
  pruebas nuevo entra en el pipeline sin tocar el script.
- **`Core/` es la frontera de lo testeable**: sin UI, sin `Process`, sin `HttpClient`, sin COM. Lo que
  cae ahí se prueba sin arrancar nada, y es donde vive lo que de verdad puede equivocarse en silencio
  (rutas de salida, magic bytes, neutralización de fórmulas CSV).
- **Los UI tests NO convierten nada, y es deliberado.** Convertir exige Office o LibreOffice y lanza
  procesos COM: metería una dependencia del entorno en **cada corte de versión** (`release.ps1` corre las
  pruebas). Lo que verifican es que **el `.exe` real arranca** y que sus controles están donde deben —
  que es justo lo que caza un publish roto (a FormatDiskPro, un publish sin el `.pri` le hacía crashear
  al arrancar con un instalador que se generaba sin quejarse).
- **El Pivot de WinUI descarga el contenido de la pestaña que no está seleccionada:** con Ajustes
  delante, `btnConvert` **no existe** en el árbol de automatización. Todo UI test debe **seleccionar su
  pestaña primero** (`Tabs.Select`) y **ninguno puede fiarse de la que dejó el anterior**.
- **Un `ComboBox` de WinUI no cambia de valor por UIA.** Ni `Select(index)` ni abrir el Popup y hacer
  `SelectionItem.Select()` disparan el `SelectionChanged` de la app: la selección "ocurre" y no pasa
  nada. Hay que conducirlo **por teclado** (foco + `Inicio` + `Abajo`), que además es el camino real de
  quien no usa ratón.
- 🔴 **`OfiConvert.UiTests` DEBE mantener su `ProjectReference` a la app con
  `ReferenceOutputAssembly="false"`.** No es una referencia normal: es una **dependencia de compilación**
  (se compila la app, no se referencia su ensamblado — cargar WinUI dentro del proceso de test es justo lo
  que no se quiere). **Sin ella, `dotnet test` de ese proyecto NO recompila la app** y los tests conducen el
  `.exe` viejo de `bin\`: pasan en verde contra un binario que ya no existe. *Un test que aprueba código que
  no se va a publicar es peor que no tener test.* Obliga además a que **el TFM del proyecto de UI tests
  coincida con el de la app** (`net10.0-windows10.0.22621.0`), o MSBuild rechaza la referencia.
- 🔴 **`SettingsBackup` respalda `%AppData%\OfiConvert` Y SIEMBRA UN ESTADO CONOCIDO** (cola e historial
  vacíos, español) antes de arrancar la app; restaura al acabar. Las dos mitades son necesarias: la app es
  *unpackaged* y escribe donde escribe la instalación real del usuario, así que sin el respaldo se le
  cambia el idioma y se le borra la cola —y **sin sembrar el estado, las pruebas dependen de con qué se
  encuentren**: «el botón Convertir está apagado porque no hay archivos» fallaría en la máquina de alguien
  con una cola pendiente, sin que la app tuviera ningún fallo.
- **Los `ToggleSwitch` se exponen a UI Automation como BOTONES SIN NOMBRE.** Su etiqueta es un `TextBlock`
  aparte y el lector de pantalla no la asocia: hay que darles `AutomationProperties.Name` a mano. Lo mismo
  para todo botón de solo icono. Lo fija `AccessibilityTests`.
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
- ⚠️ **`Progress<T>` despacha cada callback al `SynchronizationContext` y, a falta de uno, al THREAD
  POOL.** Un test que recoge los valores en una `List<T>` y asserta sobre `[^1]` hace dos apuestas
  falsas: que llegan **en orden** y que `List.Add` es seguro desde varios hilos. El test del progreso de
  descarga (Tier C) las hacía, pasaba **por suerte**, y se puso en rojo en cuanto la suite creció y metió
  presión en el thread pool. Ahora usa un `IProgress<double>` **síncrono**: lo que interesa es *qué*
  reporta la descarga, no cómo lo despacha quien la llama.
- **Se comprobó que los tests FALLAN.** En el Tier C, desactivando la verificación (2 de 10 en rojo). En
  el Tier D, quitando una clave de `ja-JP.xaml`: `LocalizationTests` la señala por archivo y nombre. Un
  test que nunca ha fallado no prueba nada.
- **Los dos tests de localización se ganaron el sueldo el día que se escribieron:** encontraron dos bugs
  reales que llevaban versiones en producción (ver el registro, 2026-07-14).

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
- 🔴 **EL IDIOMA ES ESTADO ESTÁTICO EN `LocalizationService`, Y TIENE QUE SERLO. NO CONVERTIRLO EN
  ESTADO DE INSTANCIA.** Hay **dos instancias vivas y no se puede evitar**: el código usa el singleton
  `Instance`, y `MainWindow.xaml` declara `<helpers:LocalizationService x:Key="Loc"/>`, que construye
  **la suya** — es la que escuchan los ~40 bindings de la UI. Cuando el idioma era estado de instancia,
  cambiarlo actuaba sobre el singleton y notificaba al singleton, mientras **los bindings escuchaban al
  otro objeto, que nadie tocaba jamás**: la UI se quedaba **en español en los ocho idiomas**, y ni
  reiniciar lo arreglaba (la instancia del XAML nace en español). Ver el bug en el registro (2026-07-14).
  - **Registrar el singleton como recurso desde código NO es alternativa**: WinUI no resuelve ese
    `{StaticResource}` subiendo a los recursos de la aplicación y **la app muere al arrancar**
    (`STATUS_STOWED_EXCEPTION`). Se probó.
- **`LocalizationService.SupportedLanguages` es la fuente única.** `SettingsService` valida contra ella:
  antes tenía su propia lista `("es" or "en")` y **reseteaba a español los otros seis** al cargar —
  elegir francés funcionaba hasta reiniciar. Al añadir un idioma, tocar **solo** esa lista.
- ⚠️ **El indexer devuelve la propia clave si no la conoce** — la misma trampa que el `L.T` de los
  hermanos: un typo no rompe el build ni nada visible salvo texto raro en la UI. **Ya había caído**: el
  diálogo de cierre pedía claves inexistentes y caía a español a fuego (ver el registro). Ahora lo cazan
  `LocalizationTests` (cada clave en los 8 archivos) y `LocalizationUsageTests` (cada clave **usada** en
  el código o el XAML **existe** en los diccionarios).
  - **No añadir "fallbacks defensivos"** del tipo `Instance[k] is string s && s != k ? s : "texto"`: son
    exactamente lo que ocultó el bug durante versiones. Si falta una clave, que se rompa el test.
- 🔴 **NUNCA escribir texto de UI en duro.** El mismo fallo se ha cometido **cuatro veces** —el diálogo de
  cierre, la barra de actualización, los diálogos de `DialogService` y el botón de comprobar
  actualizaciones—, y las cuatro salían **en español en los ocho idiomas** sin romper nada. Ahora lo
  prohíbe **`HardcodedUiTextTests`**: ningún literal puede asignarse a `Title`/`Message`/`Content`/
  `Text`/`…ButtonText` en el código de UI.
- ⚠️ **El escáner de claves debe cubrir TODAS las formas de pedirlas**: `GetLocalizedString("…")`,
  `LocalizationService.Instance["…"]` **y `loc["…"]`** (variable local, la que usa medio `MainWindow`). Le
  faltaba la tercera, y por eso se le escapó una clave inexistente. *Un escáner que no mira donde de verdad
  se usa el código no prueba nada.*
- **No hay detección del idioma del sistema:** arranca en español salvo ajuste guardado.

### Legal (Tier E) — no simplificar

- 🔴 **LO QUE EL INSTALADOR REDISTRIBUYE NO ES TODO MIT.** Es *self-contained*, así que dentro viajan el
  runtime de .NET y seis bibliotecas más, y **tres tienen licencias distintas de MIT**:
  - **Windows App SDK / WinUI 3 → *Microsoft Software License Terms*** (EULA propietario). El repositorio
    de GitHub sí es MIT, pero **los binarios salen del paquete NuGet**, que trae su propio `license.txt`.
    **El `THIRD-PARTY-NOTICES.txt` de WingetUSoft lo declara como MIT: está mal. No copiarlo.**
  - **Serilog y Serilog.Sinks.File → Apache-2.0.** Su cláusula 4.a **exige entregar una copia de la
    licencia**, no solo nombrarla: por eso el texto íntegro de la Apache 2.0 viaja dentro del `.exe`.
  - **WebView2 → BSD-3-Clause.** Llega como dependencia del App SDK; es fácil no verlo siquiera.
- **Verificar contra el `.nuspec` y el `license.txt` del paquete, NUNCA de memoria.** Es literalmente el
  error que cometió el proyecto hermano.
- **Los textos van EMBEBIDOS en el `.exe`** (`EmbeddedResource` en el `.csproj` → `Core/LegalText`), no
  como archivos sueltos: un archivo se borra, se queda atrás en una actualización o no llega al
  instalador, y la app dejaría de mostrar una atribución **obligatoria** sin que nada fallara.
  `LegalText` es defensivo (devuelve `""`), así que romper el embebido **no rompe nada visible**:
  `LegalTextTests` es lo único que lo convierte en un build en rojo.
- **Office y LibreOffice NO se redistribuyen**: la app los automatiza si están instalados.

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
| Pruebas unitarias | `dotnet test tests\OfiConvert.Tests\OfiConvert.Tests.csproj` |
| **Pruebas de UI** (abren la app; requieren la app compilada) | `dotnet test tests\OfiConvert.UiTests\OfiConvert.UiTests.csproj` |
| Pruebas **con red** (verifica el release real de GitHub) | `$env:OFICONVERT_NETWORK_TESTS = "1"; dotnet test …` |
| **Regenerar las capturas** del README | `.\tools\capture-screenshots.ps1` (acento neutro por defecto) |
| **Galería de revisión** de UI (todos los estados ×claro/oscuro) | `.\tools\capture-ui-states.ps1` |
| Instalador | `.\installer\build-installer.ps1` (`-CertThumbprint <huella>` para firmar) |
| **Publicar versión** | `.\release.ps1 -Version X.Y.Z` (`-DryRun` para simular) |

> Los UI tests conducen el `.exe` de `bin\` (el más reciente con RID `win-x64`); `OFICONVERT_EXE` permite
> apuntarlos a otro (un publish, una instalación real). **Verán la ventana abrirse y cerrarse: es normal.**

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

1. **La conversión en sí (COM/LibreOffice) sigue sin pruebas automatizadas**, y seguirá: exige Office
   instalado y lanza procesos. Lo que el Tier D sí cubre es todo lo que la rodea (validación previa,
   rutas de salida, mapeo de formatos, cola). Se verifica **conduciendo la app a mano**.
3. **Firma de código (OV/EV): descartada por ahora.** SmartScreen seguirá diciendo "editor desconocido", y
   la confianza de las actualizaciones se apoya en el `.sha256`. `release.ps1` deja la firma lista
   (`-CertThumbprint`) para el día que se decida.

> ✅ **Ya resuelto** (2026-07-14): la verificación del Tier C **se ejerció en producción** en el corte
> 2.2.0 → 2.3.0, y el `[NetworkFact]` la comprobó contra el release real publicado. Era el punto que este
> documento marcaba como "lo que hay que vigilar en el próximo corte".

Menores, sin tier asignado:

- **Sin detección del idioma del sistema** en el primer arranque (los hermanos sí la tienen): la app
  abre en español hasta que el usuario elija.
- **`FileValidationService` devuelve sus mensajes de error en español a fuego** ("El archivo está
  vacío.", "…protegido con contraseña."), y llegan a la UI tal cual. Es el último rincón sin traducir:
  `HardcodedUiTextTests` **no** lo caza, porque ahí los literales no se asignan a una propiedad de UI, sino
  que viajan dentro de un `FileValidationResult`.

> ✅ **Resuelto en el Tier H** (2026-07-14): los textos en duro de `DialogService` («Sí», «No», «Aceptar»,
> «Error») y los del flujo de actualización. Los prohíbe ahora `HardcodedUiTextTests`.

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
| **2.6.0** | **Pase de UX/UI sobre capturas** — galería de todos los estados (×claro/oscuro, incluidos convirtiendo/resultados con conversión real) que destapó **3 bugs** vistos solo mirando la app (historial que no distinguía éxito de fallo, diálogos que ignoraban el tema, panel de resultados con tilde verde sobre errores) + pulido (destructivos *outline*, jerarquía de tarjetas, layout, diálogo legal, duración con unidad). **230 pruebas.** |
| **2.5.0** | **Tier H** — el instalador probado de punta a punta: **`/VERYSILENT` no era silencioso** (bloqueaba con un diálogo modal), la app **se cerraba aunque el usuario rechazara el UAC**, y el flujo de actualización estaba **en español a fuego**. **226 pruebas.** |
| **2.4.0** | **Tiers E, F y G** — legal in-app (licencia y avisos **embebidos**, 8 idiomas), `THIRD-PARTY-NOTICES.txt` **verificado paquete a paquete** (no todo es MIT), capturas regenerables, README público, infraestructura agéntica, y **UI/UX: 3 bugs** (contador de reintentos invertido, carpeta de destino que prometía lo que no hacía, historial que se borraba sin preguntar) + **accesibilidad**. **212 pruebas.** |
| **2.3.0** | **Tier D** — `Core/` extraído y **170 pruebas** (152 unitarias + 18 de UI con FlaUI). Las pruebas destaparon **dos bugs de localización** en producción: la UI estaba **en español en los 8 idiomas**, y el diálogo de cierre no se traducía. |
| **2.2.0** | **Tier C** — el updater **verifica** el instalador antes de ejecutarlo (Authenticode → SHA-256). Primeras pruebas del proyecto (11, xUnit). |
| **2.1.0** | **Tier A** — instancia única + menú contextual que funciona, los 8 idiomas persisten, aviso al terminar sin modal, build 0/0, `LICENSE`, README real. **Tier B** — pipeline de release en un paso (`release.ps1`), instalador scriptado y `.sha256`. |
| **2.0.0** | Migración de WPF a **WinUI 3** (Mica, title bar propia). Post-tag, sin release: publish self-contained, tooling MSIX + idiomas en el publish, progreso de descarga en el updater. |
| **1.0.0** | La app WPF completa: conversión por lotes a 5 formatos, 8 idiomas, historial, cola persistente, bandeja, menú contextual y aviso de actualización vía GitHub. |

---

### 2026-07-21 — Pase de UX/UI: mirar la app, no leer el XAML — **v2.6.0**

Se instrumentó primero y se pulió después. La instrumentación: **`tools/capture-ui-states.ps1`**, primo de
`capture-screenshots.ps1` que fotografía **todos** los estados (vacío, con cola, historial poblado, historial
vacío, ajustes arriba y abajo, y el diálogo legal abierto) en **claro y oscuro** — 14 imágenes por corrida,
sembrando cada estado por JSON. Con esa galería delante aparecieron dos cosas que el XAML no delataba.

**🐞 1 — El historial no distinguía un fallo de un éxito.** El `FontIcon` de estado tenía el glifo (tilde) y
el color (verde) **en duro**, sin mirar `Success`. Una conversión fallida se veía **idéntica** a una correcta:
tilde verde, sin ruta y **sin decir el motivo**. Ahora el glifo/color salen de `Success` (converters
`BoolToStatus*`; la decisión de glifo vive en `Core/HistoryStatus`, pura y con test que se comprobó en rojo),
y las filas fallidas muestran su `ErrorMessage` en rojo. *Un `FontIcon` en duro no rompe el build: solo miente.*

**🐞 2 — Los `ContentDialog` ignoraban el tema de la app.** En modo Claro (con el Windows del equipo en
Oscuro), el diálogo de licencia salía **negro**: un diálogo se enraíza en la capa de popups, hermana de
`Content`, así que no hereda el `RequestedTheme` que se fija en el root. Se les pasa el tema a mano
(`RootTheme`). Afecta a los cuatro diálogos. Detalle en §4 *UI/UX*.

**🐞 3 — El panel de resultados encabezaba los errores con un tilde verde.** El mismo patrón que el bug 1,
en otro sitio: `iconResult` tenía el glifo y el color **en duro** (tilde + verde), así que *"Conversión
finalizada con errores"* salía con un check de éxito al lado. Se descubrió capturando los estados que
**exigen una conversión real** (Office): *convirtiendo*, *resultados con éxito* y *con errores*, en claro y
oscuro. Ahora el icono se enlaza a `HasConversionErrors` (converters `ErrorsToResult*`): con errores, un
**aviso ámbar** (no rojo total: parte del lote sí se convirtió). *La conversión sigue fuera del pipeline de
capturas a propósito, pero se puede conducir a mano (COM de la app corre en el escritorio interactivo; COM
desde un PowerShell no-interactivo **cuelga** — los docs de ejemplo se generan como OOXML, sin COM).*

**✨ Pulido** (todo verificado en las dos temas): diálogo legal **ensanchado** (el MIT a 80 columnas ya no se
parte en "copy"/"deal" sueltos); historial con **duración con unidad** ("2,4 s", nueva clave `UnitSeconds` ×8)
y columnas equilibradas (ruta con icono y elipsis); fila de acciones **reorganizada** (origen a la izquierda,
acciones a la derecha; adiós a la zona muerta) y su tarjeta retitulada (`LblSelectFiles`: "Selecciona:" →
"Archivos y formato"); **botones destructivos en *outline*** (rojo en texto y borde, no relleno sólido, que
choca con acentos cálidos del sistema); y **menos monotonía de tarjetas** (footer como barra de resumen con
divisor, cabecera de documentos sin chrome).

**Y un arreglo de higiene de las capturas:** la app respeta el acento de Windows, así que el repo mostraba las
capturas en el **rojo personal** del equipo del autor. Los scripts de captura ahora fijan un acento **neutro**
(`OFICONVERT_ACCENT`, que `App.OnLaunched` lee solo para esto); `docs/screenshots/` regenerado en azul por
defecto. **+4 pruebas** (`HistoryStatusTests`) → **230**.

### 2026-07-14 — Tier H: el instalador, probado de verdad — **v2.5.0**

Era **el único hueco que ninguna prueba cubría**, y este documento lo señalaba desde el principio: *«el
instalador nunca se ha probado end-to-end; FormatDiskPro encontró ahí un fallo con un diálogo modal»*. Se
probó (instalación limpia, desinstalación y actualización in-place sobre la instalación real) y apareció
**el mismo fallo, casi palabra por palabra**.

**🐞 1 — `/VERYSILENT` NO ERA SILENCIOSO.** Con `PrivilegesRequiredOverridesAllowed=dialog`, Inno planta el
cuadro «Seleccione el modo de instalación» **aunque se le pase `/VERYSILENT`**, y se queda **bloqueado
esperando un clic**. La instalación limpia de prueba tardó **76 s en lugar de 9** — los que tardó un humano
en verlo y pulsar. Desatendida, colgaría para siempre; y en la **auto-actualización** la app **ya se ha
cerrado**, así que el usuario vería su programa esfumarse y aparecer un diálogo que no ha pedido.
*Llevaba cuatro versiones escondido porque en una actualización Inno recuerda el modo anterior y no
pregunta.* Arreglo: `PrivilegesRequiredOverridesAllowed=commandline dialog` + el updater manda
`/ALLUSERS` o `/CURRENTUSER` **según cómo esté instalada la app** (`Core/InstallScope`) — una actualización
no puede mover la app de sitio por sorpresa.

**🐞 2 — La app se cerraba aunque el usuario rechazara el UAC.** Instalada *para todos los usuarios*, el
instalador pide elevación. La app lo lanzaba, esperaba 1,5 s y hacía `Application.Current.Exit()` **sin
comprobar nada**: si el usuario decía que no, el programa **desaparecía igual**, seguía en la versión vieja y
no recibía explicación. Ahora se captura `ERROR_CANCELLED` (y el instalador que muere con error) y **la app
sigue viva**, diciendo lo que ha pasado.

**🐞 3 — El mismo bug de localización, por CUARTA vez.** Todo el flujo de actualización estaba en español a
fuego («Descargando… 42%», «Instalar ahora», «Comprobando…»), igual que los diálogos de `DialogService`
(«Sí», «No», «Aceptar», «Error»). Y otra clave inexistente tapada por un fallback defensivo
(`MsgCheckingUpdate`).
**Y lo importante es por qué no se cazó:** `LocalizationUsageTests` buscaba `LocalizationService.Instance["…"]`
y `GetLocalizedString("…")`, pero **no `loc["…"]`**, que es la forma que usa medio `MainWindow`. *Un escáner
que no mira donde de verdad se usa el código no prueba nada.* Se amplía el escáner y se añade
**`HardcodedUiTextTests`**, que prohíbe asignar literales a las propiedades de texto de la UI.

**Lo que sí funcionaba** (verificado sobre la instalación real): actualización in-place sin duplicar la
instalación, cierre y **relanzado automático** de la app, `.pri` y los 8 idiomas en su sitio, datos del
usuario intactos, y desinstalación que **no borra** `%AppData%\OfiConvert`.

### 2026-07-14 — Tier G: UI/UX — tres bugs que solo se veían mirando la app — **v2.4.0**

Revisión de la interfaz **sobre capturas de la app real**, no leyendo el XAML. Y ahí estaba lo que el código
no delataba: **tres bugs en producción desde el principio**.

**🐞 1 — El contador de reintentos estaba invertido.** `CountToVisibilityConverter` **ignoraba su
`ConverterParameter`** y el XAML le pasaba `Invert` dando por hecho que lo respetaba. Resultado: el `↻ 0` se
veía en **todas** las filas (un cero que no dice nada) y el contador **se escondía justo cuando un archivo
había reintentado** — el único momento en que ese número importa. Un converter mal escrito **no rompe el
build**: solo enseña, o esconde, lo que no debe. La regla vive ahora en `Core/VisibilityRules` y se prueba.

**🐞 2 — La carpeta de destino prometía algo que no existía.** El placeholder decía «Misma ubicación que
archivos originales» y esa función **no estaba implementada**: al convertir sin carpeta elegida, la app
**interrumpía con un diálogo** y, si el usuario decía que no, **cancelaba el lote entero**. Se ha
implementado la promesa (cada documento se convierte junto al original) en vez de corregir el texto: es lo
que el usuario espera al leerlo, y hace que la app funcione **sin configurar nada**.

**🐞 3 — «Limpiar historial» borraba hasta 1000 registros sin preguntar.** La única acción irreversible de
la app era la única sin confirmación.

**La causa raíz de casi todo lo demás:** **ninguno de los 15 `[RelayCommand]` tenía `CanExecute`**. Los
botones estaban siempre encendidos y la app compensaba **riñendo al usuario** con diálogos («No hay archivos
seleccionados»). Ahora se apagan solos — y el arreglo **quita** código: tres diálogos y **cinco claves × 8
idiomas** desaparecen.

**Accesibilidad: la app era muda.** `AutomationProperties` no aparecía **ni una vez** en todo el XAML. Los
botones de solo icono no tenían nombre accesible… y tampoco los tres `ToggleSwitch`, que **UI Automation
expone como botones sin nombre** (su etiqueta es un `TextBlock` aparte que el lector no asocia): anunciaban
«botón, activado» sin decir de qué. **Esos tres los encontró el test**, no la revisión visual.

**Dos trampas de las propias pruebas, descubiertas de camino** (ver §4 *Pruebas*):
- **Los UI tests conducían un `.exe` VIEJO**: `OfiConvert.UiTests` no referenciaba la app, así que
  `dotnet test` no la recompilaba y las pruebas pasaban contra un binario que ya no existía.
- **Dependían de los datos reales del usuario**: «el botón se apaga si no hay archivos» habría fallado en la
  máquina de quien tuviera una cola pendiente, sin que la app tuviera fallo alguno.

### 2026-07-14 — Tiers E y F: cara pública, legal e infraestructura agéntica — **v2.4.0 (sin publicar)**

**Lo que hay que recordar de este tier no es lo que se construyó, sino lo que se descubrió al verificar.**

**⚖️ "Verificar cada `.nuspec`, no de memoria" no era una frase bonita.** El plan mandaba portar el
`THIRD-PARTY-NOTICES.txt` de WingetUSoft. **Ese archivo está mal**: declara el **Windows App SDK como
MIT**. El repositorio de GitHub sí es MIT, pero **los binarios que el instalador redistribuye salen del
paquete NuGet**, que trae un `license.txt` con los *Microsoft Software License Terms* — un EULA
propietario. Copiarlo habría propagado un aviso legal falso a un tercer proyecto.

Y no era la única: **Serilog es Apache-2.0** (no MIT), y su cláusula 4.a **exige entregar una copia de la
licencia**, así que el texto íntegro de la Apache 2.0 viaja ahora dentro del `.exe`. **WebView2 es
BSD-3-Clause** y llega como dependencia del App SDK, donde nadie lo había mirado. Todo comprobado leyendo
los `.nuspec` y los `license.txt` de los paquetes realmente redistribuidos. Detalle en §4 *Legal*.

**Lo construido (Tier E).**
- `THIRD-PARTY-NOTICES.txt` (345 líneas, con los textos íntegros de MIT, BSD-3 y Apache-2.0) y `LICENSE`,
  ambos **embebidos en el `.exe`** (`Core/LegalText`) y accesibles desde *Configuración → Acerca de*, con
  sus 4 claves nuevas en los 8 idiomas (126 por archivo).
- `tools/capture-screenshots.ps1` + `docs/screenshots/`: **4 capturas regeneradas conduciendo la app
  real**. Tres cosas que costaron: se **respaldan y restauran** el `settings.json` y el `queue.json` del
  usuario (la app es *unpackaged* y escribe donde escribe la instalación de verdad); se **siembra la cola**
  con documentos de ejemplo (una captura de la cola vacía no enseña el producto); y se captura la
  **pantalla**, no la ventana, porque un WinUI con Mica no se deja capturar por `PrintWindow` — con
  `DWMWA_EXTENDED_FRAME_BOUNDS`, que `GetWindowRect` mete la sombra invisible del marco.
- README con badges, capturas y una sección legal que **no miente sobre las licencias**.
- **9 pruebas nuevas** (184 en total): `LegalTextTests` fija que las tres licencias no-MIT sigan ahí — si
  alguien "simplifica" el archivo a un "todo es MIT", el build cae — y 3 UI tests abren los diálogos
  legales de verdad y leen su contenido. Hacía falta: `LegalText` es defensivo, así que romper el
  `EmbeddedResource` **no rompería nada visible**, solo dejaría de mostrar una atribución obligatoria.

**Lo validado (Tier F).** Estaba **a medias sin que el ROADMAP lo supiera**: las 9 skills de C#/.NET, el
`skills-lock.json` y el índice de codegraph **ya existían** (y bien: el índice se auto-ignora, así que sus
2 MB no entran en el repo). Lo que faltaba era lo que los hace utilizables — **`.mcp.json`** (sin él, el
grafo existía y **no había nada que lo sirviera**), **`.claude/CLAUDE.md`** y **`.claude/settings.json`**.

**Otra que el plan daba por hecha y era falsa:** decía que el `CLAUDE.md` de WingetUSoft manda «leer
`CONTEXT.md` al iniciar sesión y mantenerlo». **No lo dice** — su `CLAUDE.md` es solo el bloque que genera
codegraph. Aquí esa parte se ha escrito de verdad (con los 6 invariantes que no se rompen), en vez de
copiarla de donde no existía.

### 2026-07-14 — Tier D: pruebas de verdad — y los dos bugs que encontraron — **v2.3.0**

De **11 pruebas** (solo el updater) a **170**: 152 unitarias + 18 de UI conduciendo el `.exe` real. Pero lo
que hay que contar de este tier no son las pruebas: es **lo que encontraron el día que se escribieron**.

**🐞 Bug 1 — La interfaz estaba en español en los ocho idiomas.**
`MainWindow.xaml` declaraba `<helpers:LocalizationService x:Key="Loc"/>`. Eso **construye una segunda
instancia**: los ~40 bindings de la UI escuchaban a ese objeto, mientras el cambio de idioma llamaba a
`LoadLanguage` sobre el singleton `LocalizationService.Instance` — **otro objeto, al que la UI no escuchaba
jamás**. Consecuencia: elegir japonés cambiaba los mensajes que pasan por código… y **dejaba todos los
botones y etiquetas en español**, para siempre; **ni reiniciando** (la instancia del XAML nace en español).
El `settings.json` guardaba el idioma correctamente, así que desde fuera parecía que funcionaba, y los 7
idiomas no españoles llevaban así desde que existen.
*Arreglo:* el idioma pasa a ser **estado compartido** por todas las instancias (ver §4 *Localización*).
Registrar el singleton como recurso desde código **no era alternativa**: WinUI no resuelve ese
`{StaticResource}` subiendo a los recursos de la app y **la app muere al arrancar** — se probó, y se cambió
de enfoque.
*Verificado contra la app real:* al cambiar a inglés, un control ya en pantalla pasa de "Sistema" a
"System". Lo fija `LocalizationUiTests`.

**🐞 Bug 2 — El diálogo de cierre, sin traducir.** Pedía `TitleConfirmClose`, `BtnYes` y `BtnNo`, que **no
existían**, y caía a un texto español en duro. Sus traducciones **ya estaban en los 8 idiomas**, con otro
nombre (`MsgCancelConfirm`/`MsgCancelConfirmTitle`) y **sin usarse en ningún sitio**. Es el diálogo que
protege contra los procesos de Office huérfanos — *EL* riesgo de esta app. Se conectan las claves que ya
existían, se añaden `BtnYes`/`BtnNo` (8 idiomas, 122 claves por archivo) y **se quitan los fallbacks
defensivos**, que eran justo lo que lo ocultaba.

**Lo construido.**
- **`Core/`** (la regla de oro de los hermanos): `OutputPath` (salida confinada + nunca sobrescribe),
  `FileSignature` (magic bytes, sin tocar disco), `CsvField` (fórmulas neutralizadas), `ByteSize`,
  `OfficeFormats` + `OutputFormatHelper`. De paso, **`ByteSize` mata una duplicación real**: el ViewModel
  llegaba a TB y el historial se quedaba en GB, así que el mismo archivo se veía distinto en la cola y en
  el historial exportado.
- **`tests/OfiConvert.Tests`** (152): `Core/`, `FileValidationService` contra archivos reales (vacío,
  bloqueado con `FileShare.None`, cifrado, `.docx` renombrado a `.doc`), `ActivationArguments` (el menú
  contextual), y los dos tests de localización.
- **`tests/OfiConvert.UiTests`** (18, FlaUI/UIA3): arrancan el `.exe` real. **Sin elevación** (`asInvoker`)
  y **sin Office** — ninguno convierte nada, a propósito: `release.ps1` corre las pruebas en cada corte y
  un release no puede depender de lo que haya instalado en la máquina.

**Dos trampas de WinUI que costaron el rato** (§4 *Pruebas*): el **Pivot descarga el contenido de la
pestaña que no está delante** (con Ajustes visible, `btnConvert` no existe en el árbol de automatización), y
un **`ComboBox` no cambia de valor por UIA** — hay que conducirlo por teclado o el `SelectionChanged` de la
app no se dispara.

**Un test que mentía, arreglado:** `DownloadInstaller_ReportsProgress` (Tier C) assertaba sobre `reports[^1]`
de una `List<double>` rellenada desde `Progress<T>`, que **despacha al thread pool**: ni el orden ni la
seguridad de `List.Add` estaban garantizados. Pasaba por suerte y se puso en rojo en cuanto la suite creció.
Ahora usa un `IProgress<double>` síncrono.

**Se comprobó que las pruebas fallan:** quitando `BtnNo` de `ja-JP.xaml`, `LocalizationTests` lo señala por
archivo y por clave. Un test que nunca ha fallado no prueba nada.

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
