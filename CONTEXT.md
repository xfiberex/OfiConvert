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
| **Versión publicada** | **2.0.0** (2026-04-03) — instalador sin firmar y **sin `.sha256`** |
| **En `main`, sin publicar** | 3 commits posteriores al tag `v2.0.0` (publish self-contained, tooling MSIX + idiomas en el publish, progreso de descarga del updater) |
| **Estado** | Funcional; hoja de ruta **ABIERTA** — deuda de infraestructura frente a los hermanos |
| **Stack** | C# / .NET 10 · **WinUI 3** (Windows App SDK **1.8.260317003**, unpackaged, `net10.0-windows10.0.22621.0`, mín. 10.0.19041.0) · COM Interop (Office) + LibreOffice CLI · Serilog · Inno Setup 6 |
| **Licencia** | MIT según el README — ⚠️ **sin archivo `LICENSE`** en el repo (Tier A) |
| **Pruebas** | **Ninguna** (Tier D) |
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
OfiConvert/                    (proyecto único en la raíz del repo; no hay src/ ni tests/)
├─ Program.cs                  Main propio (DISABLE_XAML_GENERATED_MAIN); crash.log si el arranque revienta
├─ App.xaml(.cs)               UnhandledException → crash.log; inicializa Serilog; guarda args (⚠️ sin usar, §6)
├─ MainWindow.xaml(.cs)        Ventana única: cola, formatos, ajustes, updater (InfoBar), bandeja, Mica
├─ ViewModels/
│  └─ MainViewModel.cs         Orquestación completa: cola, conversión paralela + pausa + reintentos,
│                              ajustes, historial, persistencia de la cola
├─ Services/
│  ├─ OfficeFileConversionService.cs   Motor principal: COM late binding (Word/Excel/PowerPoint)
│  ├─ LibreOfficeConversionService.cs  Motor alternativo: soffice --headless --convert-to
│  ├─ FileValidationService.cs         Magic bytes OLE/ZIP: corrupto, con contraseña, bloqueado, vacío
│  ├─ GitHubUpdateService.cs           Consulta releases y descarga el instalador (⚠️ SIN verificarlo — Tier C)
│  ├─ ConversionHistoryService.cs      history.json (máx. 1000) + export CSV (fórmulas neutralizadas) y TXT
│  ├─ SettingsService.cs               settings.json, validado al cargar (⚠️ bug de idiomas, §6)
│  ├─ QueuePersistenceService.cs       queue.json: la cola sobrevive al cierre (filtra UNC/relativas/inexistentes)
│  ├─ ShellIntegrationService.cs       Menú contextual del Explorador (HKCU, por extensión)
│  ├─ ThumbnailService.cs              Miniaturas del shell para la lista
│  ├─ DialogService.cs                 Pickers de archivo/carpeta y diálogos
│  └─ LoggingService.cs                Serilog → %AppData%\OfiConvert\logs (diario, 30 días, 10 MB)
├─ Models/                     FileItem, ConversionOptions/Result/Progress, OutputFormat(+Helper), AppSettings…
├─ Helpers/LocalizationService.cs   8 idiomas (ES/EN/PT/FR/DE/IT/ZH/JA) — ver §4 Localización
├─ Behaviors/ · Converters/    Drag & drop de archivos · converters de binding
├─ Lang/*.xaml                 Diccionarios de idioma (viajan junto al .exe, parseados en runtime)
├─ Assets/app.ico
└─ installer/OfiConvert.iss    Inno Setup — hoy se compila A MANO (Tier B)
```

**Regla de oro de los hermanos, aquí PENDIENTE:** no existe `Core/`. La lógica pura y testeable
(rutas de salida seguras, formateo de bytes, mapeo de formatos, sanitización CSV, comparación de
versiones) vive mezclada en `Services/` y `MainViewModel`. Extraerla es la primera fase del Tier D.

---

## 3. Estado actual

| | |
|---|---|
| Build | `dotnet build -c Release`: 0 errores / **39 advertencias** MVVMTK0045 (ver §6.6) |
| Pruebas | No existe proyecto de tests |
| Publicado | v2.0.0 (releases v1.0.0 y v2.0.0 en GitHub, con instalador) |
| Updater | Funciona de punta a punta, pero **no verifica** lo que descarga y ejecuta |
| Pendiente de release | 3 commits en `main` posteriores al tag `v2.0.0` |

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
- ⚠️ **El updater NO verifica el instalador que descarga y ejecuta** — el mismo agujero que
  FormatDiskPro (#38) y WingetUSoft cerraron con Authenticode → SHA-256 y marcaron como NO ROMPER.
  Es el **Tier C**, y el motivo de que el pipeline (Tier B) deba subir el `.sha256` como segundo asset.

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
- ⚠️ **La versión vive en DOS sitios:** `<Version>` del `.csproj` **y** `#define MyAppVersion` del
  `.iss`. Hoy se actualizan a mano y pueden divergir; el Tier B lo resuelve como los hermanos (el
  script del instalador lee la versión del `.csproj`).

### Localización

- **8 idiomas** (ES/EN/PT/FR/DE/IT/ZH/JA) en `Lang/*.xaml`, parseados **en runtime** con `XDocument`
  (no son ResourceDictionary compilados) y refrescados por binding al indexer
  (`{Binding [Clave], Source={StaticResource Loc}}`). Si falta el archivo del idioma, cae a `es-ES`.
- ⚠️ **El indexer devuelve la propia clave si no la conoce** — la misma trampa que el `L.T` de los
  hermanos: un typo no rompe el build ni nada visible salvo texto raro en la UI. El test de completitud
  (cada clave en los 8 archivos + cada clave usada en el código existe) es parte del Tier D.
- **No hay detección del idioma del sistema:** arranca en español salvo ajuste guardado. Y el ajuste
  **solo persiste es/en** — bug real, ver §6.1.

### Datos del usuario

- Todo en `%AppData%\OfiConvert\`: `settings.json` (validado al cargar con `Math.Clamp`),
  `history.json` (máx. 1000 entradas), `queue.json` (la cola sobrevive al cierre; al cargar filtra
  rutas no absolutas, UNC e inexistentes) y `logs\` (Serilog diario, 30 días, 10 MB por archivo).
- `crash.log` se escribe junto al `.exe` (`AppContext.BaseDirectory`) — matiz en §6.8.
- Límite de entrada: archivos de hasta **500 MB** (`MaxFileSizeBytes`).

---

## 5. Tareas comunes

| Tarea | Comando |
|-------|---------|
| Compilar | `dotnet build OfiConvert.csproj -c Release` |
| Ejecutar | `dotnet run --project OfiConvert.csproj` |
| Publicar | `dotnet publish OfiConvert.csproj -c Release -r win-x64 --self-contained -o ./publish` |
| Instalador | **Manual:** compilar `installer/OfiConvert.iss` con Inno Setup (ISCC o IDE) → `installer/Output/` |
| Publicar versión | **Manual:** bump en `.csproj` **y** `.iss` → publish → instalador → subir al release de GitHub |

> El README documenta además `-p:PublishSingleFile=true` en el publish; queda por verificar en el
> Tier B (el instalador copia `publish\*` recursivo, así que single-file no es requisito).
> El pipeline en un paso (`release.ps1`) es el **Tier B** de la hoja de ruta.

---

## 6. Pendientes — hallazgos de la auditoría (2026-07-13)

El plan por tiers está en [`ROADMAP.md`](ROADMAP.md). Estos son los hallazgos **confirmados contra el
código**, con su tier asignado:

1. **6 de los 8 idiomas no persisten.** `SettingsService.ValidateSettings` resetea a `"es"` todo
   idioma que no sea `es`/`en`, mientras el combo ofrece 8 y `LocalizationService` los soporta todos:
   elegir francés funciona… hasta reiniciar. *(Tier A)*
2. **El menú contextual del Explorador no hace nada útil.** `ShellIntegrationService` registra
   `"OfiConvert.exe" "%1"`, pero `App` guarda los `args` y **nunca los procesa**: la app abre vacía en
   vez de encolar el archivo. *(Tier A)*
3. **Updater sin verificación** (ver §4 Seguridad). *(Tier C — requiere el `.sha256` del Tier B)*
4. **README desfasado:** describe el stack **WPF** de la v1.0.x (WPF-UI, Behaviors.Wpf), el instalador
   "1.0.0" compilado a mano, y la `<Description>` del `.csproj` dice "a PDF" cuando hay 5 formatos.
   *(Tier A)*
5. **`LICENSE` y `THIRD-PARTY-NOTICES.txt` no existen**, aunque el README declara MIT. *(El archivo
   `LICENSE`, Tier A; avisos de terceros in-app, Tier E)*
6. **39 advertencias MVVMTK0045:** `[ObservableProperty]` sobre campos no es AOT-compatible en
   WinUI 3; migrar a propiedades parciales. Los hermanos exigen build 0/0. *(Tier A)*
7. **"Notificaciones" que son un diálogo modal:** al terminar un lote con `ShowNotifications` activo se
   muestra un `ContentDialog` (las claves se llaman `TrayNotif*`, pero no hay notificación de bandeja).
   Decidir: notificación de bandeja real (H.NotifyIcon la soporta) o renombrado honesto. *(Tier A)*
8. **`crash.log` junto al `.exe`:** con la instalación per-user (la ruta por defecto) funciona; si el
   usuario eligió elevar la instalación a Archivos de programa, la escritura falla y el crash se
   pierde. Menor. *(Tier A, opcional)*

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
| **2.0.0** | Migración de WPF a **WinUI 3** (Mica, title bar propia). Post-tag, sin release: publish self-contained, tooling MSIX + idiomas en el publish, progreso de descarga en el updater. |
| **1.0.0** | La app WPF completa: conversión por lotes a 5 formatos, 8 idiomas, historial, cola persistente, bandeja, menú contextual y aviso de actualización vía GitHub. |

---

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
