# Contexto del proyecto — OfiConvert

> **Qué es este archivo.** El contexto **vivo** del proyecto: qué es, cómo está montado, qué se decidió
> y por qué, y qué pasó en cada versión. Sirve para retomar el trabajo sin releer el código (y sin
> repetir errores ya pagados por este proyecto o por sus hermanos). **Mantenerlo con cada cambio
> relevante:** actualizar §3 _Estado actual_ y añadir una entrada al _Registro de cambios_ (fecha
> absoluta). Commitearlo junto al cambio.
>
> Reparto de los tres documentos vivos, que **no se solapan**: [`ROADMAP.md`](ROADMAP.md) responde a
> **qué falta** (tiers pendientes); [`CHANGELOG.md`](CHANGELOG.md), a **qué cambió** en cada versión,
> contado para quien usa el programa; y este, a **qué hay hecho, cómo y POR QUÉ**. Ante la duda entre
> los dos últimos: el *qué* va al changelog, el *porqué* aquí.
>
> **Proyectos hermanos:** [FormatDiskPro](https://github.com/xfiberex/FormatDiskPro) y
> [WingetUSoft](https://github.com/xfiberex/WingetUSoft) (mismo autor, mismo stack, ambos TERMINADOS).
> Gran parte de la hoja de ruta consiste en **portar su infraestructura ya probada**; sus `CONTEXT.md`
> documentan los tropiezos que aquí no hay que repetir.

| | |
|---|---|
| **Repositorio** | https://github.com/xfiberex/OfiConvert |
| **Versión publicada** | **2.7.0** (2026-09-01) — **21 de las 39 fichas del [Tier J](ROADMAP.md)**: las 7 Altas y 14 Medias. Deja de cerrarle al usuario su PowerPoint sin guardar, de borrar archivos ajenos por la ruta de LibreOffice y de hablar en español en los ocho idiomas. Instalador sin firmar, **con `.sha256`** |
| **En `main`, sin publicar** | La **verificación de punta a punta de TJ-25** contra LibreOffice 26.8.0.3 (2026-09-01) |
| **Estado** | Funcional; Tiers 0 y A–I ✅. **Hoja de ruta REABIERTA**: [Tier J](ROADMAP.md) (re-auditoría del 2026-08-29) — **39 tareas, 21 cerradas** (las **7 Altas**, completas); quedan 6 Medias y 12 Bajas |
| **Stack** | C# / .NET 10 · **WinUI 3** (Windows App SDK **1.8.260317003**, unpackaged, `net10.0-windows10.0.22621.0`, mín. 10.0.19041.0) · COM Interop (Office) + LibreOffice CLI · Serilog · **xUnit** + **FlaUI** · Inno Setup 6 |
| **Licencia** | **MIT** ([`LICENSE`](LICENSE)) — pero **lo que redistribuye NO es todo MIT**: ver §4 *Legal* |
| **Pruebas** | **307**: 276 unitarias (269 pasan + 7 omitidas: 1 de red con `OFICONVERT_NETWORK_TESTS=1` y 6 que conducen Office con `OFICONVERT_OFFICE_TESTS=1`) + **31 de UI** (FlaUI, contra la app real) |
| **Hoja de ruta** | [`ROADMAP.md`](ROADMAP.md) — **Tier J abierto** (2026-08-29) |
| **Cambios por versión** | [`CHANGELOG.md`](CHANGELOG.md) — creado el 2026-08-29; **el _qué_ va allí, el _porqué_ aquí** |
| **Última actualización** | 2026-08-29 |

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
│                             capture-ui-states.ps1 (galería de revisión) · capture-dropdown.ps1 (popups opacos)
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
| Pruebas unitarias | **284 pasan · 9 se omiten (1 de red + 6 que conducen Office + 2 que ejecutan LibreOffice) · 0 fallan** (total 293); con `OFICONVERT_OFFICE_TESTS=1` y `OFICONVERT_LIBREOFFICE_TESTS=1`, **293 pasan** |
| Pruebas de UI | **34 pasan · 0 fallan** (FlaUI, arrancan la app real **en la configuración compilada**) |
| Publicado | **v2.7.0** (2.1.0 → 2.7.0 cortadas con `release.ps1`; todas con instalador + `.sha256`) |
| Updater | **Verifica** el instalador antes de ejecutarlo (Authenticode → SHA-256) |
| Instalador | **Probado de punta a punta** (2026-07-14): instalación limpia, desinstalación y actualización in-place sobre una instalación real. ⚠️ **Solo en un equipo CON Office**: ver `TJ-04` |
| Pendiente de release | La verificación de TJ-25, el guardián de puertas de entorno, y **TJ-15, TJ-09, TJ-14 y TJ-16** |
| **Abierto** | **[Tier J](ROADMAP.md)** — re-auditoría externa del 2026-08-29: **39 tareas** (TJ-39 nació dentro del tier), **25 cerradas — las 7 Altas, completas**; quedan **2 Medias** (TJ-22, TJ-26) **y 12 Bajas**. Lo cerrado se publicó en la **v2.7.0** |

**Tiers** (detalle en [`ROADMAP.md`](ROADMAP.md)) — **A–I cerrados; J abierto**

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
| **I** | **Pase de UX/UI sobre capturas (3 bugs vistos solo mirando la app)** | ✅ |
| **J** | **Re-auditoría externa: el motor, el pipeline y los guardianes** | 🔶 **abierto (2026-08-29)** |

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

- 🔴 **LOS POPUPS DE WINUI SON ACRÍLICOS POR DEFECTO Y SOBRE MICA SE VEN BORROSOS.** `generic.xaml` alias
  `ComboBoxDropDownBackground` → `AcrylicInAppFillColorDefaultBrush`, así que el desplegable de un
  `ComboBox` (y los `Flyout`/`MenuFlyout`) transparenta el contenido de la ventana **a través del menú** y,
  encima, pinta la **textura de ruido** del acrílico. Con el backdrop Mica se apilan dos capas translúcidas:
  panel moteado y texto sin contraste. Se arregla en `App.xaml`, forzando opacos
  `ComboBoxDropDownBackground`, `FlyoutPresenterBackground`, `MenuFlyoutPresenterBackground` y los dos
  `Acrylic*FillColorDefaultBrush`, en `ThemeDictionaries` (Light/Dark/HighContrast). A nivel de App, no
  control por control: si no, el siguiente `ComboBox` que se añada vuelve a salir borroso.
- 🔴 **UN `ResourceDictionary.ThemeDictionaries` EN LA RAÍZ DE `Application.Resources` NO SE HONRA** si esa
  raíz ya tiene `MergedDictionaries`. Compila, no da ni una advertencia, y los overrides **no se aplican**:
  el popup sigue acrílico exactamente igual que antes. Tiene que ir como un `ResourceDictionary` **dentro
  de `MergedDictionaries`, después de `XamlControlsResources`**. Ya se pagó: el arreglo del acrílico se dio
  por bueno una vez sin efecto ninguno, y solo se destapó **ampliando la captura** y viendo que el moteado
  del acrílico seguía ahí. *Corolario: un override de tema que "no se ve" casi nunca es el color mal
  elegido; es que la clave no se está resolviendo.*
- **Cómo se comprueba esto sin ojo clínico:** `.\tools\capture-dropdown.ps1`. Abre los cuatro `ComboBox`
  y **cuenta los colores** del fondo del popup. Acrílico = decenas de valores vecinos (`#2B`–`#30`, el
  ruido): 38–68% de "cuota de ruido". Sólido = **un** valor, el que se puso: 0%. Así se distingue "está
  opaco" de "parece opaco" sin discutir sobre una captura. Ojo con el `.exe` que mide: coge el más reciente
  de `bin\`, que puede ser un Debug viejo de un `dotnet run` — compila antes.

### Conversión por LibreOffice (no romper)

- **A LibreOffice NUNCA se le da la carpeta del usuario como `--outdir`.** No acepta un nombre de
  salida, solo una carpeta, y dentro escribe con el nombre del original **pisando lo que haya**: con un
  `informe.pdf` ya presente, lo sobrescribía y el `File.Move` posterior se llevaba el nuevo a
  `informe (1).pdf`, así que **el archivo anterior desaparecía**. Se convierte en una carpeta temporal
  **exclusiva de esa conversión** (`Core/LibreOfficeOutput`) y de ahí se mueve al destino, comprobando
  otra vez que sigue libre. Exclusiva y no compartida: dos documentos homónimos en paralelo producirían
  ambos `informe.pdf`. (TJ-03, 2026-08-31.)
- **Todo proceso externo se lanza por `Services/ProcessRunner`**, que empieza a leer `stdout` **y**
  `stderr` **antes** de esperar al proceso. Redirigidos y sin leer, el búfer de la tubería (~4 KB) se
  llena, el hijo se bloquea escribiendo y la espera no vuelve nunca: una conversión congelada para
  siempre, ocupando una plaza del semáforo, sin error y sin registro. **No vale leer solo el flujo que
  se usa:** el que se queda sin leer es justo el que llena la tubería. (TJ-02, 2026-08-31.)
- **Código 0 no significa que haya salida.** LibreOffice termina «bien» sin generar nada cuando su
  filtro no soporta ese formato para ese documento; antes se daba la conversión por buena y el historial
  apuntaba a un archivo inexistente. Se comprueba el archivo, no el código.

### Conversión COM (no romper)

- **PowerPoint es una instancia COM ÚNICA.** `Type.GetTypeFromProgID("PowerPoint.Application")` +
  `Activator.CreateInstance` **no crea un proceso**: devuelve el que ya corre. Medido aquí (Office 16
  ClickToRun): dos activaciones → **1** `POWERPNT.EXE`; Word y Excel → **2**. De ahí dos reglas:
  1. Las conversiones de PowerPoint van **serializadas** (`Services/SerialGate`). Word y Excel no.
  2. **Solo se cierra la instancia que ha abierto la app** (`PowerPointSession`, que mira `POWERPNT.EXE`
     *antes* de activar). Si es del usuario, se le devuelven `DisplayAlerts` y `AutomationSecurity` como
     estaban y **no se llama a `Quit()`**: la app le cerraba su PowerPoint con `ppAlertsNone` puesto, o
     sea, **sin preguntar por lo no guardado**. Ante cualquier duda, se asume que es del usuario.
- **Sobre una instancia PRESTADA, `Marshal.ReleaseComObject` — nunca `FinalReleaseComObject`.** El RCW de
  una aplicación COM es **compartido dentro del proceso**: «Final» no suelta *nuestra* referencia sino
  **todas**, PowerPoint se queda sin clientes de automatización y **cierra las presentaciones abiertas
  por esa vía**. El proceso sigue vivo y el usuario pierde su trabajo igual. Lo cazó
  `PowerPointSharedInstanceTests`, no la revisión del código.

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
  - 🔴 **Y la limpieza tiene que cubrir el camino que se tuerce.** `CreateOfficeApp` llamaba a
    `configure(app)` **fuera de todo `try`**: si esa configuración lanzaba, el método propagaba sin haber
    devuelto el objeto, el `finally` del llamante recibía `null` y quedaba **un proceso vivo por cada
    intento**. La ventana peligrosa es exactamente la que va entre «ya existe el proceso» y «el llamante
    tiene la referencia», y es responsabilidad de quien abre. (TJ-20.)
- 🔴 **A PowerPoint NO SE LE TOCA `Visible`. Ni a true ni a false.** *Medido el 2026-08-31 (Office 16
  ClickToRun):* recién activado por COM está en `Visible = msoFalse` y **sin ventana principal**
  (`MainWindowHandle = 0`), y abrir con `WithWindow:=False` lo deja igual. **Es headless de fábrica.**
  - `Visible = msoFalse` **lanza** («*Hiding the application window is not allowed*»). Cierto — pero de
    ahí **no** se sigue que haya que ponerlo a `msoTrue`, que es la conclusión que se sacó y la que hacía
    aparecer la ventana encima del usuario durante todo el lote. La respuesta es **no tocarlo**.
  - `HidePowerPointWindows` se borró: recorría `presentation.Windows` poniendo `Visible = -1` —que es
    **mostrar**, lo contrario de su nombre— y nunca se ejecutaba, porque con `WithWindow:=False`
    `Windows.Count` es **0**. Código muerto que además mentía. (TJ-21.)
  - ⚠️ **`HidePowerPointWindows` NO oculta nada: pone `Visible = -1`, que es msoTrue.** Hoy da igual
    porque con `WithWindow:=False` la colección viene vacía y el bucle no se ejecuta. Ver `TJ-21`.
- 🔴 **POWERPOINT ES UNA INSTANCIA COM ÚNICA Y COMPARTIDA. WORD Y EXCEL, NO.** *Medido el 2026-08-29 en
  esta máquina (Office 16 ClickToRun): dos `Activator.CreateInstance` seguidos sobre
  `PowerPoint.Application` dejan **un** `POWERPNT.EXE`; sobre `Word.Application` y `Excel.Application`,
  **dos** procesos cada uno.* Dos consecuencias, y las dos muerden:
  - Con `MaxParallelConversions > 1`, **N conversiones de `.pptx` conducen el MISMO PowerPoint**, y la
    primera que termina llama a `Quit()` — matando las presentaciones que las demás están exportando.
  - Si el usuario tiene PowerPoint abierto, la app **se engancha a su sesión** y la cierra al acabar,
    con `DisplayAlerts = ppAlertsNone` puesto: **sin preguntar por lo no guardado**.

    Por eso las conversiones de PowerPoint hay que **serializarlas**, y hay que **detectar si la
    instancia era preexistente** para no llamar a `Quit()` en ese caso. Tarea `TJ-01`. *No es una
    peculiaridad de esta máquina: PowerPoint solo admite una instancia de automatización por sesión.*
- Excel→CSV exporta **una hoja** (la activa, o la indicada en `ConversionOptions.SheetNames`);
  PPT→PNG/JPG exporta **todas las diapositivas** a una subcarpeta con el nombre del archivo.

- **Todo lo que se pinte en la UI se construye en el HILO DE UI.** Un `BitmapImage` creado en un hilo
  del pool no vale, y el `catch` que se lo tragaba dejó la lista **sin una sola miniatura** durante
  meses sin que nada lo dijera (TJ-14). Corolario: los `catch` mudos en el camino de la interfaz son
  cómplices — registran, o no se ponen.
- **`AppWindow.Resize` habla en píxeles FÍSICOS.** Sin escalar por `GetDpiForWindow`, la ventana nace más
  pequeña de lo pensado en cuanto Windows no está al 100 % (al 150 %, un tercio). El tamaño y el mínimo
  se calculan en `Core/WindowSizing`, y el mínimo se fija con
  `OverlappedPresenter.PreferredMinimumWidth/Height`: los desplegables tienen ancho fijo y las etiquetas
  alemanas son las más largas de los ocho idiomas (TJ-16).

### Salir de la app (no romper)

- **Toda salida pasa por el cierre ordenado**: cancelar el lote, guardar ajustes, `Dispose` del
  ViewModel y soltar el icono de bandeja (`ReleaseResources`). Existe porque *el* riesgo declarado de
  este programa es dejar **procesos de Office huérfanos**.
- **`Application.Current.Exit()` NO pasa por `OnAppWindowClosing`.** La instalación de una actualización
  terminaba ahí y se saltaba las cuatro cosas (TJ-15). Hoy hay un único camino, `ShutdownForUpdateAsync`,
  y `ShutdownPathsTests` falla si aparece otro `Exit()` suelto.
- **Un botón deshabilitado es una promesa de la interfaz, no una garantía**: el manejador comprueba
  `CanClose()` por su cuenta. Entre pulsar «instalar» y salir pasan los minutos de la descarga con la
  ventana viva, tiempo de sobra para que el usuario ponga un lote en marcha.

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
- **El corte prueba EL BINARIO QUE PUBLICA** (TJ-05, 2026-08-31). `release.ps1` corre
  `dotnet test -c Release`: sin el `-c`, MSBuild reconstruye la app en **Debug** por el
  `ProjectReference` y los UI tests conducen ese `.exe`, no el Release que empaqueta el instalador.
  `AppFixture` ya no elige «el `.exe` más reciente» —heurística que en la máquina del desarrollador
  apunta a Debug la mitad de las veces—: exige el de `bin\{configuración}\`, lo deja por escrito y
  `DrivenBinaryTests` falla si no cuadra. Es la misma familia que el bug del Tier G (conducir un `.exe`
  viejo): aquel pedía que fuera **fresco**; este, que sea **el que se publica**.
- **Los modificadores del instalador silencioso se arman en `Core/InstallScope`, no a mano** (TJ-04,
  2026-08-31): `/VERYSILENT /NORESTART /SUPPRESSMSGBOXES {alcance} /autoinstall=1`. **`/VERYSILENT` no
  silencia los `MsgBox` del script de Inno** — y el `.iss` planta uno cuando no detecta Office. Doble
  guarda: el aviso vive dentro de `if (not WizardSilent)` y el updater manda `/SUPPRESSMSGBOXES`.
- **Las notas del release salen de `CHANGELOG.md`, no de una plantilla** (TJ-07, 2026-08-31).
  `release.ps1` extrae la sección `## [X.Y.Z]` con `Get-ChangelogSection` y **aborta si no está**, antes
  de compilar nada — así el error cuesta segundos y no cinco minutos de build. Consecuencia práctica:
  **la sección se escribe ANTES del corte**, que es cuando se sabe qué cambió; después, ya no lo sabe
  nadie (las nueve primeras versiones se publicaron con el mismo texto genérico, y reconstruir su
  changelog a posteriori solo dio una aproximación). `-NotesFile` sigue mandando sobre todo esto.
  `ChangelogTests` lleva el mismo contrato a `dotnet test`, para que el fallo no espere al corte.
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
- **UI Automation NO asocia una etiqueta a un control por estar al lado.** Un `TextBlock` hermano no es
  el nombre de nadie: sin `AutomationProperties.Name`, el lector de pantalla anuncia el tipo y el valor
  («botón, activado», «cuadro combinado, PDF», «cuadro de número, 2») y jamás **de qué** son. Vale para
  los `ToggleSwitch` —que además UIA expone como botones—, para todo botón de solo icono y para los
  `ComboBox` y `NumberBox` (TJ-09). El `Name` se ata a la **misma clave** que su etiqueta, así se traduce
  con ella. Lo fija `AccessibilityTests`, que desde TJ-09 mira `Button`, `ComboBox`, `Spinner` y `Edit`.
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

- **Un servicio NUNCA devuelve texto para el usuario: devuelve una clave** (`Core/UserMessage`, clave +
  argumentos). La traducción ocurre en **un solo sitio**, `LocalizationService.Translate`, que es el
  borde con la UI. Motivo: los servicios corren en hilos de fondo y no saben —ni deben saber— en qué
  idioma está la app; devolviendo `string` no hay forma de acertar, y así se colaron **18 mensajes** en
  español que salían igual en los ocho idiomas, con varias de sus traducciones ya escritas y sin usar
  (TJ-06). El historial guarda el texto **ya traducido**: es lo que se exporta a CSV/TXT y lo que se lee
  meses después.
- **Cada forma nueva de pedir una clave se añade al escáner EN EL MISMO CAMBIO que la crea.**
  `LocalizationUsageTests` ha ido por detrás del código tres veces (`loc["…"]`, `T("…")` y ahora
  `UserMessage`/`Failed`), y cada retraso costó claves sin vigilar. Van siete formas.
- **`HardcodedUiTextTests` descubre los archivos, no los lista** (TJ-17), y vigila **dos** patrones: el
  literal asignado a la UI y el literal pasado **como argumento** a `ShowError`/`Failed`/`UserMessage`.
  Como argumento solo se admite una clave: una frase con espacios es el delito.

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
| **Comprobar que los desplegables son opacos** (no acrílicos) | `.\tools\capture-dropdown.ps1` — mide, no solo fotografía; sale con código 1 si alguno sigue acrílico |
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
> ✅ **Resuelto en el Tier J** (2026-08-31): `FileValidationService` —y los otros tres sitios— devolvían
> sus mensajes en español a fuego, y `HardcodedUiTextTests` no los cazaba porque no se asignaban a una
> propiedad de UI. Hoy los servicios devuelven **claves** (`Core/UserMessage`) y el guardián mira también
> los literales que viajan como argumento (TJ-06, TJ-17).

> ✅ **Resuelto en el Tier H** (2026-07-14): los textos en duro de `DialogService` («Sí», «No», «Aceptar»,
> «Error») y los del flujo de actualización. Los prohíbe ahora `HardcodedUiTextTests`.

---

## 7. Cómo mantener este documento

1. Tras un cambio relevante: entrada nueva en el **Registro de cambios** (fecha absoluta) + actualizar §3.
2. Si cambia una convención o decisión, reflejarlo en §4 (es la sección que evita repetir errores).
3. Marcar el ítem como ✅ en [`ROADMAP.md`](ROADMAP.md) cuando esté **verificado** (build + tests +
   prueba real), no cuando esté escrito.
4. Commitear este archivo **junto** con el cambio, para que el contexto viaje con el código.
5. Lo que le pasa **al usuario** va a [`CHANGELOG.md`](CHANGELOG.md), bajo `## [Sin publicar]`, y se
   escribe **antes** del corte: `release.ps1` sacará de ahí las notas del release (`TJ-07`).
6. Una decisión revertida **no se borra**: se marca como superada y se explica qué la cambió.

---

# Registro de cambios

### Índice de versiones

| Versión | Qué trajo |
|---|---|
| **2.7.0** | **Tier J (21 de 39 fichas)** — re-auditoría externa. Deja de **cerrar el PowerPoint del usuario** con trabajo sin guardar, de **borrar un archivo anterior** por la ruta de LibreOffice, de **pisar dos archivos homónimos** en el mismo lote y de mostrar **18 mensajes de error en español** en los ocho idiomas; el instalador deja de contradecir al producto sobre LibreOffice. **307 pruebas.** |
| **2.6.1** | Los **desplegables se veían borrosos** sobre Mica: fondo sólido en claro, oscuro y alto contraste. El arreglo va en `App.xaml` **después** de `XamlControlsResources` — en la raíz no se aplica. **230 pruebas.** |
| **2.6.0** | **Pase de UX/UI sobre capturas** — galería de todos los estados (×claro/oscuro, incluidos convirtiendo/resultados con conversión real) que destapó **3 bugs** vistos solo mirando la app (historial que no distinguía éxito de fallo, diálogos que ignoraban el tema, panel de resultados con tilde verde sobre errores) + pulido (destructivos *outline*, jerarquía de tarjetas, layout, diálogo legal, duración con unidad). **230 pruebas.** |
| **2.5.0** | **Tier H** — el instalador probado de punta a punta: **`/VERYSILENT` no era silencioso** (bloqueaba con un diálogo modal), la app **se cerraba aunque el usuario rechazara el UAC**, y el flujo de actualización estaba **en español a fuego**. **226 pruebas.** |
| **2.4.0** | **Tiers E, F y G** — legal in-app (licencia y avisos **embebidos**, 8 idiomas), `THIRD-PARTY-NOTICES.txt` **verificado paquete a paquete** (no todo es MIT), capturas regenerables, README público, infraestructura agéntica, y **UI/UX: 3 bugs** (contador de reintentos invertido, carpeta de destino que prometía lo que no hacía, historial que se borraba sin preguntar) + **accesibilidad**. **212 pruebas.** |
| **2.3.0** | **Tier D** — `Core/` extraído y **170 pruebas** (152 unitarias + 18 de UI con FlaUI). Las pruebas destaparon **dos bugs de localización** en producción: la UI estaba **en español en los 8 idiomas**, y el diálogo de cierre no se traducía. |
| **2.2.0** | **Tier C** — el updater **verifica** el instalador antes de ejecutarlo (Authenticode → SHA-256). Primeras pruebas del proyecto (11, xUnit). |
| **2.1.0** | **Tier A** — instancia única + menú contextual que funciona, los 8 idiomas persisten, aviso al terminar sin modal, build 0/0, `LICENSE`, README real. **Tier B** — pipeline de release en un paso (`release.ps1`), instalador scriptado y `.sha256`. |
| **2.0.0** | Migración de WPF a **WinUI 3** (Mica, title bar propia). Post-tag, sin release: publish self-contained, tooling MSIX + idiomas en el publish, progreso de descarga en el updater. |
| **1.0.0** | La app WPF completa: conversión por lotes a 5 formatos, 8 idiomas, historial, cola persistente, bandeja, menú contextual y aviso de actualización vía GitHub. |

---

### 2026-09-01 — TJ-14 y TJ-16: la miniatura que nunca se vio, y la ventana que encogía sola

**TJ-14 — la ficha preguntaba «¿se ven hoy las miniaturas?». La respuesta es no, y nunca se vieron.**

El código guardaba un PNG en `%TEMP%`, se lo daba a `BitmapImage.UriSource` —que carga de forma
**asíncrona**— y lo borraba en el `finally` inmediato. La auditoría lo anotó como una carrera que se
pierde en los dos sentidos: o se borra antes de cargar, o no se borra y `%TEMP%` se llena. La realidad era
peor y más simple: **siempre ganaba la misma rama**. El `BitmapImage` se construía dentro de un
`ContinueWith(..., TaskScheduler.Default)`, o sea **fuera del hilo de UI**, donde WinUI no permite
crearlo; reventaba, el `catch { return null; }` se lo tragaba, y el borrado llegaba de sobra. Resultado:
la lista mostraba siempre el icono genérico y `%TEMP%` quedaba limpio —la basura que la ficha temía **no
llegó a existir**, porque el fallo anterior la evitaba—.

Comprobado conduciendo la app real con un `.docx`, la misma ventana antes y después: con el código
antiguo, icono genérico; con el nuevo, la miniatura del documento. Ahora el disco no se toca: el trabajo
pesado devuelve **bytes** y el `BitmapImage` se crea en el hilo que llama, con `SetSourceAsync` sobre un
flujo en memoria. Y el `catch` registra.

> **Lo que enseña:** un `catch` mudo en el camino de la interfaz no «hace la app robusta», la deja
> **rota en silencio**. Este llevaba desde la v1.0 escondiendo que la función entera no funcionaba, y
> ninguna prueba podía verlo porque no había ninguna.

**TJ-16 — la ventana.** `AppWindow.Resize` habla en píxeles **físicos**, así que `Resize(1050, 800)` solo
es correcto al 100 %: al 150 % —un portátil cualquiera— el contenido se dibuja un 50 % más grande dentro
de la misma caja y la ventana nace un tercio más pequeña de lo pensado. Y no había mínimo: se podía
arrastrar hasta montar unos controles sobre otros, con desplegables de ancho fijo (110/140/160 px) y
etiquetas alemanas dentro.

El cálculo vive en `Core/WindowSizing` porque es aritmética pura y así se prueba sin abrir una ventana; el
mínimo se fija con `OverlappedPresenter.PreferredMinimumWidth/Height`. **Verificado sobre la ventana
real:** abre 1050×800 a 96 ppp y, forzada a 400×300 con `MoveWindow`, se queda en **880×620**.

⚠️ **No verificado:** el aspecto a 150 % y 200 % en hardware — esta pantalla está al 100 %. Lo que se
prueba es la aritmética que fallaba, no cómo se ve.

**Pruebas:** 284 pasan · 9 omitidas · 0 fallan; UI 34 · 0. Build 0/0. `ThumbnailServiceTests` comprobado
en rojo escribiendo el PNG a disco a propósito: 49 restos en `%TEMP%`.

---

### 2026-09-01 — TJ-15 y TJ-09: la salida que se saltaba el cierre, y siete controles mudos

Dos Medias, y las dos del mismo tipo: **el arreglo estaba escrito en un solo camino** y había un segundo
camino que nadie miró.

**TJ-15 — actualizar a mitad de un lote se saltaba todo.** El botón «instalar actualización» no estaba
atado a `IsConverting`, y el flujo terminaba en `Application.Current.Exit()`, que **no pasa por
`OnAppWindowClosing`**. Es decir: se saltaba la confirmación al cerrar convirtiendo, la cancelación del
lote, el guardado de ajustes y el `Dispose` del ViewModel — precisamente las cuatro cosas que existen para
no dejar **procesos de Office huérfanos**, que es *el* riesgo declarado de esta app. Todo eso vivía escrito
en línea dentro del manejador de cierre de ventana, así que solo ocurría al cerrar con el aspa.

Tres piezas, y ninguna sobra:

- el botón se apaga solo cuando `IsConverting` cambia (`SyncUpdateButtonState`);
- el manejador comprueba `CanClose()` **por su cuenta**: un botón deshabilitado es una promesa de la
  interfaz, no una garantía;
- la salida pasa por `ShutdownForUpdateAsync`, que cancela el lote, espera hasta 10 s a que Office suelte
  lo suyo y hace el mismo cierre ordenado que el aspa (`ReleaseResources`, extraído del manejador).

> **Por qué el botón apagado no basta:** entre pulsar «instalar» y salir pasan los **minutos de la
> descarga**, con la ventana viva y usable. El usuario puede encolar y arrancar un lote mientras tanto.
> El estado que se comprobó al pulsar no es el estado que hay al salir.

**TJ-09 — siete controles mudos, y el guardián que los tapaba.** Cuatro `ComboBox` (*Formato*, *Tema*,
*Idioma*, *Formato por defecto*), dos `NumberBox` (*Conversiones en paralelo*, *Reintentos máximos*) y —de
propina, no estaba en la ficha— el `ComboBox` de formato **por archivo** no tenían nombre accesible: el
Narrador anunciaba «cuadro combinado, PDF» o «cuadro de número, 2» sin decir nunca de qué. La etiqueta de
al lado es un `TextBlock` hermano, y **UI Automation no asocia nada por proximidad visual**.

Lo grave no es el arreglo —un `AutomationProperties.Name` atado a la misma clave que la etiqueta, así se
traduce con ella— sino que `AccessibilityTests` existía desde el Tier G **filtrando por
`ControlType.Button`**: llevaba meses en verde sobre estos siete. Cazó los `ToggleSwitch` de puro rebote,
porque UIA los expone como botones. Ahora recorre `Button`, `ComboBox`, `Spinner` y `Edit` — los tres
últimos porque el reparto de tipos depende de la versión de WinUI, y de eso no puede depender la
accesibilidad de nadie.

**Guardianes** (los dos comprobados en rojo antes de darlos por buenos):

- `AccessibilityTests`, ampliado — rojo quitando el `Name` de *Idioma*.
- `ShutdownPathsTests`, nuevo y **estructural**: ninguna llamada a `Application.Current.Exit()` sin cierre
  ordenado delante, el flujo de actualización comprueba `CanClose()` y el botón sigue al estado de la
  conversión. Estructural porque no hay forma de probarlo por la interfaz: el botón vive en una `InfoBar`
  que solo aparece si hay una versión publicada más nueva, y los UI tests no convierten nada a propósito.
  Rojo al reponer las dos versiones anteriores.

> Y una repetición de la trampa de siempre: el escáner de salidas se puso rojo con **su propio comentario**
> —el que explica que `Application.Current.Exit()` no pasa por `OnAppWindowClosing`—. Es la tercera vez en
> este tier que un guardián lee comentarios como si fueran código (pasó con el `.iss` en TJ-04 y con
> `WizardSilent`). Se vacían los comentarios antes de mirar, siempre.

**Pruebas:** 272 pasan · 9 omitidas · 0 fallan; UI **34** · 0. Build 0/0.

De paso, §6 de este documento seguía diciendo que `FileValidationService` devolvía sus mensajes en español
a fuego y que `HardcodedUiTextTests` no lo cazaba: las dos cosas las cerró el propio Tier J (TJ-06 y
TJ-17) y el párrafo llevaba desde entonces mintiendo. Corregido.

---

### 2026-09-01 (con LibreOffice instalado) — TJ-25: la deuda que la v2.7.0 dejó por escrito

La v2.7.0 publicó el arreglo de TJ-25 diciendo, en sus propias notas, que el criterio de aceptación
**no se había ejecutado**: en la máquina no había LibreOffice. Instalado (26.8.0.3), se salda.

**El criterio, tal cual: ocho documentos, paralelismo 4.** Ahora es una prueba
(`LibreOfficeEndToEndTests`), no una comprobación de una tarde, y corre con el mismo mecanismo que la app
—semáforo + `Task.WhenAll`— sobre ocho `.docx` reales construidos con `ZipArchive`. **8 de 8.**

**Pero primero se midió la premisa, en vez de darla por buena.**

| | PDFs | código != 0 | tiempo |
|---|---|---|---|
| Perfil compartido | **4 de 8** | 4 | **12,8 s** |
| Perfil propio | **8 de 8** | 0 | 25,9 s |

Dos cosas que la ficha no sabía:

1. **Los cuatro que caen no dan ningún error.** Código de salida 1, y `stdout` **y** `stderr`
   **vacíos**. La ficha prometía «errores que no parecen de conversión»; la realidad es peor — no hay
   nada que leer. El usuario veía desaparecer archivos del lote sin un solo mensaje.
2. 🔴 **La configuración ROTA es la que parece rápida:** la mitad de tiempo, porque la mitad de
   los documentos moría al instante en vez de convertirse. **Un lote medido por su duración habría
   premiado el fallo**, y «ahora tarda el doble» es justo lo que se ve al arreglarlo.

Cuatro repeticiones: 4/8 en todas — sistemático, no intermitente; lo que varía es **cuáles** caen. Y la
prueba se comprobó en rojo apuntando el servicio a un perfil compartido: cae con `Fallaron 4`, el mismo
número medido por fuera.

**Un guardián que volvía a llevar la lista a mano.** `ReleaseScriptTests` comprobaba que las clases con
puerta de entorno estuvieran en el `ExpectedSkipPattern` del corte… contra **tres nombres escritos a
mano**. Al añadir la cuarta puerta (`LibreOfficeFact`) había que acordarse de venir aquí: el fallo de
TJ-17 otra vez. Ahora las descubre — busca los `*FactAttribute` que miran una variable de entorno, y
luego quién los usa. Verificado en rojo quitando la clase del patrón.

**Dos trampas del entorno, para no volver a pagarlas:**

- **`soffice --version` es INTERACTIVO en Windows.** Abre una consola y espera un
  «Press Enter to continue…». Capturado desde un script devuelve **cadena vacía** y deja la ventana
  abierta esperando a un humano — en un script desatendido, ahí se queda. La instalación se detecta
  **por la ruta del ejecutable**, que es lo que ya hace `LibreOfficeConversionService`.
- Escribiendo este mismo registro, un escape `\ud83d\udd34` dentro de un literal de Python
  reventó el `write` **después** de que `open(..., 'w')` truncara el archivo: `ROADMAP.md` se quedó en
  **0 bytes** (recuperado con `git checkout`). Dos reglas: los emojis se construyen con `chr()`, y se
  **escribe a un temporal y se reemplaza**, nunca encima del archivo bueno. La segunda salvó a
  `CONTEXT.md` del mismo fallo diez minutos después.

---

### 2026-09-01 — El corte de la v2.7.0, y dos formas de medir mal

Primera versión cortada con el pipeline que el propio Tier J arregló: las notas salen del `CHANGELOG.md`
(TJ-07), el corte imprime pasan/omitidas/fallan por proyecto (TJ-08) y las pruebas de UI conducen el
binario Release que empaqueta el instalador (TJ-05). El ensayo (`-DryRun`) salió limpio a la primera:
269/7/0 y 31/0/0, sin omisiones imprevistas, instalador de 58,2 MB con su `.sha256`.

Lo que costó trabajo no fue cortar, sino **escribir un número honesto**.

**1. El `[Sin publicar]` no era una nota de release, era un sedimento.** Se había ido escribiendo tanda a
tanda, así que contenía **dos líneas base contradictorias** — «307 pruebas (antes 268)» y «249 (antes
237)»— y un cambio de comportamiento (PowerPoint pasa a convertir en serie) archivado como corrección.
Ninguna de las dos cifras era la de la v2.6.1: eran fotos de mitad del tier, ciertas cuando se
escribieron y falsas al leerlas juntas en un release. **Una sección de changelog se redacta entera al
cortar, no se acumula**; lo que se acumula es material para redactarla.

**2. La línea base había que medirla, y el primer intento midió otra cosa.** Para decir «307 frente a
N» hice un `git worktree` sobre `v2.6.1` y lo puse **bajo el scratchpad de la sesión**, cuya ruta es
larga. Resultado: **177 de 200 pruebas en rojo** con
`DllNotFoundException: Microsoft.WindowsAppRuntime.dll ... el nombre del archivo o la extensión es
demasiado largo (0x800700CE)`. No era la v2.6.1: era **MAX_PATH**. Y lo peligroso es que también
falseaba el **recuento** — decía 196 pruebas donde hay 200, porque algunas se enumeran leyendo archivos
y esa enumeración también fallaba. Repetido en `C:\ofc-b261`: **200 · 1 omitida · 0 fallos**, verde.
Base real **230** (200 unitarias + 30 de UI) frente a **307** de hoy (276 + 31).

> **Regla, hermana de la de TJ-39:** un árbol de trabajo para medir va en una **ruta corta**. Un WinUI 3
> que no arranca en un `git worktree` casi nunca es culpa del commit que se está midiendo. Es la
> **segunda** vez que MAX_PATH muerde a este proyecto: ya obligaba a publicar en `%TEMP%` (§4,
> *Build y publicación*), porque el publish self-contained del Windows App SDK trae nombres de hasta
> 76 caracteres.

Y una corrección al pasar: TJ-06 fueron **18** mensajes de error. El literal número 19 que aparece en el
registro del 2026-08-31 (`StateMessage = "Pendiente"`) lo cazó el guardián de TJ-17, pero es un **estado
de la lista**, no un aviso de error; contarlo dentro de TJ-06 inflaba la cifra de cara al usuario.

---

### 2026-08-31 — El pipeline, lo legal, y una carrera en las propias pruebas

**TJ-23 — eran CUATRO paquetes sin atribuir, no uno.** La ficha señalaba `System.Drawing.Common`. Leer lo
que de verdad se publica destapó tres más: `Microsoft.Win32.SystemEvents`, `System.Numerics.Tensors` y
`H.GeneratedIcons.System.Drawing`. **949 KB de DLL** redistribuidas sin una línea de atribución.

Ninguna se pidió: las cuatro son **transitivas** de `H.NotifyIcon.WinUI` y del Windows App SDK. Por eso
`LegalTextTests` tenía razón al advertir de sí misma —«*si mañana entra una dependencia nueva y nadie
toca el archivo de avisos, esto no lo caza*»— y por eso el arreglo no es añadir cuatro líneas sino
**cruzar `obj/project.assets.json` con las DLL de la salida**. Licencias leídas del `.nuspec` de cada
paquete en la caché de NuGet, **no de memoria**, como manda la regla de este proyecto.

> 🔴 **Y el aviso más útil de todo esto es sobre el propio test.** La primera versión buscaba el nombre
> con `Contains` sobre el documento entero. Al comprobarla en rojo —quitando la atribución— **pasó en
> verde**: el nombre seguía apareciendo en la descripción de otra entrada, «*9) Microsoft.Win32.SystemEvents
> (dependencia de System.Drawing.Common)*». **Una mención no es una atribución.** Ahora se exige que el
> paquete esté en el **título** de una entrada numerada. Sin el paso de verificar en rojo, ese test se
> habría quedado ahí dando falsa tranquilidad.

**TJ-24 — la contraseña no se «asegura», se quita de en medio.** `SecureString` arregla que se teclee y
se quede en `ConsoleHost_history.txt`, pero **no** lo importante: `signtool /p <contraseña>` la publicaba
en la línea de comandos del proceso, que **cualquier proceso del equipo puede leer** sin permisos
especiales mientras dura la firma. Ahora el `.pfx` se importa al almacén con el `SecureString`, se firma
por **huella** —que no es secreta— y el certificado se retira en un `finally`: dejar la clave privada en
el almacén sería cambiar una fuga por otra.

**TJ-08 — «omitida» no es «pasa», y el código de salida no las distingue.** El corte lee ya el `.trx` y
dice los tres números por proyecto. Lo que más valor tiene no es el recuento sino **nombrar** las
omitidas inesperadas: «1 omitida» no dice nada; «`PublishedReleaseTests` omitida» sí.

**TJ-18 — al escáner de claves le faltaba el sentido de vuelta.** La ida ya estaba (siete formas). Lo que
faltaba es lo que habría cazado `TJ-06` mucho antes: **nada declarado puede quedarse sin usar**. Hay 33,
congeladas como trinquete y anotadas por grupos —ocho son de la función a medio construir de `TJ-26` y
hay que **usarlas**, no borrarlas—. Con un segundo test que obliga a limpiar la propia lista de
excepciones: *una lista que no se limpia deja de ser un trinquete y pasa a tapar casos nuevos*.

---

**🔴 TJ-39 — hallazgo nuevo, y la lección de método de la jornada.**

Durante un corte de prueba falló `UserMessageTranslationTests`. Una vez. En la suite completa no se
reproducía.

**El mecanismo:** el idioma es **estado estático** en `LocalizationService` —invariante de §4, y tiene
que serlo—, pero xUnit corre **cada clase en su propia colección y las colecciones en paralelo**. Las dos
clases que cambian de idioma se pisaban la misma variable. **Medido: 3 rojos de 8**, con víctima distinta
cada vez, que es la firma de una carrera. Compartir `[Collection]` las serializa: **10 de 10 en verde**.

> ⚠️ **Pero antes de esa medición hice una mala.** El primer intento dio «6 rojos de 6» *antes y después*
> del arreglo, y estuve a punto de darlo por bueno como reproducción. El detector buscaba el texto «Con
> error» en la salida… y la línea de éxito de `dotnet test` dice literalmente «**Con error: 0**». Marcaba
> rojo siempre.
>
> **La regla:** *una medición que da el mismo resultado con y sin el arreglo no está midiendo el
> arreglo.* Es la misma disciplina del «en rojo antes que en verde», aplicada al instrumento y no al
> código. Se mira el **código de salida**, no un texto que puede contener su propia negación.

**Estado:** 21 de 39 del Tier J. Build 0/0, **269 unitarias pasan · 7 se omiten · 31 de UI**; con
`OFICONVERT_OFFICE_TESTS=1`, 276.

---

### 2026-08-31 — TJ-11, TJ-13, TJ-10 y TJ-19: cuatro fallos que el usuario nunca podría diagnosticar

Lo que une a los cuatro: **ninguno da un error**. La app hace algo mal y sigue como si nada.

**TJ-11 — dos archivos distintos acababan siendo uno.** `OutputPath.GetSafe` decidía con `File.Exists`,
que solo ve lo **ya escrito**. Con destino común y dos orígenes homónimos (`ventas\informe.docx` y
`compras\informe.docx`, de lo más corriente), las dos conversiones preguntaban a la vez por
`informe.pdf`, las dos oían que no existía, y la segunda pisaba a la primera. **Las dos se apuntaban como
correctas en el historial.**

La cura no es un candado sobre la escritura, sino **reservar el nombre al calcularlo**:
`Core/OutputReservations`. Tres decisiones que conviene no deshacer:

- **El alcance es el LOTE.** Uno por conversión no arregla nada; uno global para toda la vida del
  programa haría que reconvertir el mismo documento fuera a `informe (1).pdf` sin motivo. Un lote es
  exactamente el tiempo en que dos conversiones pueden solaparse.
- **Mirar y apuntar van bajo el mismo candado.** Soltarlo entre las dos cosas reabre la carrera.
- **Las carpetas también** (PPT→imágenes): dos presentaciones homónimas exportaban a la misma carpeta y
  mezclaban sus diapositivas por número.

*Verificado en rojo:* de **32 reservas simultáneas, solo 1** era distinta.

**TJ-13 — dos avisos a la vez son cero avisos.** `AddFiles` avisaba **dentro** del bucle y
`ShowInformation` es `async void`. WinUI admite **un** `ContentDialog`: el segundo lanza, y sobre un
`async void` esa excepción sale sin dueño, la traga `App.UnhandledException` y el usuario **no ve nada** —
ni el aviso ni el error. Solo archivos que no aparecen.

> **La regla, que vale para toda la app:** *ningún diálogo dentro de un bucle*. Acumular y avisar una vez.
> La vigila `DialogsInLoopsTests`, que no mira este caso sino **la forma**.

**TJ-10 — la frase se cortaba donde iba la respuesta.** El resumen formateaba `MsgFilesSavedTo` con
`OutputFolder`, que está **vacío** mientras el usuario no elige carpeta — el camino que el propio Tier G
recomienda. Así que el flujo **por defecto** terminaba en «*Archivos guardados en:*» y nada. Sin carpeta
común no hay una ruta que enseñar, así que hay que decirlo con palabras. Las dos ramas del resumen
—con errores y sin ellos— formaban la frase por separado con el mismo fallo: ahora hay **un solo**
`DondeSeGuardo()`.

**TJ-19 — se quita, y por una razón.** El `IProgress<ConversionProgress>` atravesaba las dos firmas, las
dos implementaciones y el ViewModel, que construía un `Progress<>` con «Convirtiendo 3/7». No se ejecutó
**nunca**.

La tentación era implementarlo. No hay qué implementar: Word y Excel convierten con **una** llamada COM
sin devolución de llamada, LibreOffice es un proceso externo mudo, y solo PPT→imágenes conoce el número
de diapositivas —y aun así exporta de una vez—. Reportar en 1 de 6 caminos daría una barra que se mueve
para un formato y se queda quieta para los demás: **peor que no tenerla**.

> **Una API que promete lo que no puede cumplir es peor que una API pequeña.** El porqué queda escrito en
> `IFileConversionService`, que es donde alguien irá a preguntarse por qué no hay progreso. Y
> `DeadProgressTests` cierra la clase entera: quien declare un `IProgress<>` tiene que reportarlo.

**Estado:** 16 de 38 del Tier J. Build 0/0, **257 unitarias pasan · 7 se omiten · 31 de UI**; con
`OFICONVERT_OFFICE_TESTS=1`, 263.

---

### 2026-08-31 (noche) — TJ-21, TJ-20 y TJ-25: el motor, y una ficha que se equivocaba de culpable

**TJ-21 — la ficha decía una cosa y la medición dijo otra.** La tarea pedía cambiar un `-1` por un `0` en
`HidePowerPointWindows`. Medir primero cambió el arreglo entero:

| Medición (Office 16 ClickToRun) | Resultado |
|---|---|
| `presentation.Windows.Count` con `WithWindow:=False` | **0** → la función era código muerto |
| `Application.Visible = msoFalse` | **lanza**: *«Hiding the application window is not allowed»* |
| PowerPoint recién activado, sin tocar nada | `Visible = msoFalse`, **`MainWindowHandle = 0`** |

O sea: **PowerPoint es headless de fábrica**, y la ventana que salía la pedía nuestro código con
`Visible = msoTrue`. El comentario que lo justificaba —«PowerPoint no admite trabajar oculto»— decía
*media verdad*: es cierto que no se le puede poner `msoFalse`, pero de ahí no se sigue que haya que
ponerlo a **true**. Entre las dos opciones que parecían existir faltaba la buena: **no tocarlo**.

`HidePowerPointWindows` se borró en vez de corregirse. Sin ventanas que ocultar no había nada que
arreglar, y dejar una función que no se ejecuta —y que hace lo contrario de su nombre— es dejar una
trampa cargada para quien algún día abra con ventana.

> **La lección, que es de método:** una ficha de auditoría es una hipótesis, no un encargo. Esta señalaba
> el sitio correcto y el culpable equivocado. Se comprueba **antes** de arreglar.

El guardián vigila **durante** la conversión, no al final: una ventana que aparece y se va sigue siendo
una ventana que le salta al usuario. En rojo, con el código anterior: **16 muestras** con la ventana en
pantalla.

**TJ-20 — un proceso huérfano por intento.** `CreateOfficeApp` llamaba a `configure(app)` fuera de todo
`try`. Si lanzaba, propagaba **sin haber devuelto el objeto**: el `finally` del llamante recibía `null` y
no tenía a quién cerrarle. Verificado en rojo con Word real: **1 `WINWORD.EXE` por intento**. Es el
riesgo que este documento lleva señalando desde el principio, y estaba abierto justo en la ventana entre
«ya existe el proceso» y «el llamante tiene la referencia».

Se prueba con **Word y no con PowerPoint** a propósito: Word arranca proceso propio, así que contar
procesos dice la verdad. Con PowerPoint —instancia única— el recuento no distinguiría el nuestro del que
ya hubiera.

**TJ-25 — cerrada con la verificación a medias, y dicho.** Cada conversión de LibreOffice lleva ya su
propio perfil (`-env:UserInstallation=file:///…`, **delante** de `--convert-to`, porque LibreOffice
decide si arranca motor propio antes de mirar qué convertir).

> ⚠️ **En esta máquina no hay LibreOffice**, así que el criterio de aceptación —8 documentos con
> paralelismo 4— **no se ha ejecutado nunca**. Lo que sí está probado es la forma exacta del argumento, y
> eso no es un consuelo menor: pasarle una **ruta de Windows** en vez de una URL **no da error**,
> LibreOffice la ignora y vuelve al perfil compartido. El fallo seguiría ahí **en silencio** y un test que
> solo mirase «que aparezca `-env:`» pasaría igual. Por eso se comprueba carácter a carácter, y en rojo
> por los dos lados: barras sin normalizar (caen 3) y `-env:` mal colocado (cae 1).

**Estado:** 12 de 38 del Tier J. Build 0/0, **242 unitarias pasan · 7 se omiten · 31 de UI**; con
`OFICONVERT_OFFICE_TESTS=1`, 248.

---

### 2026-08-31 (segundo equipo) — TJ-12, y tres cosas que solo se ven al validar en otra máquina

El Tier J se implementó en un equipo y se validó en otro. **Ese cambio de máquina destapó cosas que en
la primera no se veían**, que es exactamente para lo que sirve.

**🔴 Las tres pruebas que conducen Office eran INESTABLES.** No fallaban por el producto: fallaban por su
propia limpieza. Cada una abría con un `Assert.Equal(0, PowerPointProcesses())` **instantáneo**, y el
`EsperarACierre()` de la anterior aguantaba como mucho **5 s**. `POWERPNT.EXE` a veces tarda más en
morir después de un `Quit()`, así que la siguiente arrancaba viendo un proceso que ya se estaba
apagando. **Medido:** ejecutando la clase entera falló con «Expected: 0, Actual: 1» **a los 8 ms**; sola
pasaba, y al repetir la clase también.

*Y la demostración de que el arreglo es el correcto se hizo al revés:* dejando el `EsperarACierre` del
teardown **sin esperar nada**, la clase ya no falla en la línea 119 (la precondición) sino en la **142**
— la postcondición, que dependía del mismo reloj corto. O sea: el diagnóstico era ese y no otro, y la
postcondición tenía el mismo defecto. Las dos esperan ahora **15 s**, y la precondición trae un mensaje
que distingue *«una prueba anterior no soltó el suyo»* de *«tienes PowerPoint abierto con tu trabajo»*,
que piden cosas distintas. Estable 3 de 3 tras el cambio.

> **La regla:** una prueba que falla una de cada dos veces por su propia limpieza **deja de ser un
> guardián** — se acaba ignorando. Y justo esta cubre el fallo más grave del tier.

**🔴 `HardcodedUiTextTests` tenía un hueco en su propia expresión regular.** Con `\b` pegado a la lista
de nombres, `StateMessage = "Pendiente"` **no casaba**: entre la «e» de `State` y la «M» de `Message` no
hay frontera de palabra. El literal estaba en `Models/FileItem.cs` mientras el test pasaba en verde.
Es el **vigésimo** literal del tier y el **segundo que se le escapa a su propio guardián** — el primero
fue mirar solo dos archivos (TJ-17). Lo que importa es cómo **acaba** el nombre de la propiedad, no cómo
empieza: `[A-Za-z_]*(?:Title|Message|Content|Text|Header)`.

*Comprobación limpia:* con el regex anterior y el literal delante, **verde**; con el nuevo, **rojo**.

Al ampliarlo salieron otros dos, y ninguno se ha «arreglado» a lo tonto: el tooltip de la bandeja
(`"OfiConvert"`, nombre propio, no se traduce) y el rótulo del menú contextual del Explorador
(`"Convertir con OfiConvert"`), que **sí** es un fallo real pero ya está fichado como `TJ-22` — y no se
cierra escribiendo una clave: hay que **reescribir el registro** al cambiar de idioma. Va a la lista de
excepciones **con su fecha de caducidad apuntada**, no se tapa.

**TJ-12 — el instalador contradecía al producto.** Avisaba de que sin Microsoft Office la app «no
funcionará». Es falso: LibreOffice sirve igual y el README lo anuncia.

*Verificado sobre el instalador compilado*, con los dos detectores forzados por línea de comandos, y sin
mostrar un solo diálogo (la sonda vuelca la decisión a un archivo y se ejecuta con `/VERYSILENT`):

| Office | LibreOffice | Decisión |
|---|---|---|
| 0 | 0 | **AVISA** |
| 0 | 1 | calla ← *la fila que antes mentía* |
| 1 | 0 | calla |
| 1 | 1 | calla |

Los seis textos se volcaron desde el propio instalador, uno por idioma, y se leyeron con sus acentos.

> ⚠️ **Trampa nueva de Inno, pagada aquí:** una línea de comentario que **empiece por corchete** la lee
> ISCC como etiqueta de sección y aborta con *«Invalid section tag»* — **aunque esté dentro de un
> comentario `{ }` y sangrada**. Mencionar `[CustomMessages]` al principio de una línea rompió el build.
> Queda anotado dentro del propio `.iss`.

> ⚠️ **Y un error propio, para que no se repita:** el primer intento escribió los acentos como `%363`,
> dando por hecho que Inno admite escapes numéricos en los mensajes personalizados. **No los admite**:
> ahí `%1`–`%9` son parámetros y `%n` el salto de línea. Como el `.iss` ya lleva BOM UTF-8, los acentos
> van **literales**.

**Estado al cerrar:** 9 de 38 del Tier J. Build 0/0, **233 unitarias pasan · 4 se omiten · 31 de UI**;
con `OFICONVERT_OFFICE_TESTS=1`, 236.

---

### 2026-08-31 — TJ-06 y TJ-17: la quinta vez del texto en español, y el guardián que miraba dos archivos

Con esto se cierran **las siete tareas Altas** del [Tier J](ROADMAP.md).

**TJ-06 — 18 mensajes en español, en los ocho idiomas.** `FileValidationService`,
`LibreOfficeConversionService`, `OfficeFileConversionService` y el propio `MainViewModel` devolvían frases
escritas a mano, y esas frases llegaban al panel de resultados, a la columna *Error* del historial y al
CSV/TXT exportado. Lo peor no es el número: **cinco de ellas ya estaban traducidas a los ocho idiomas y
no las usaba nadie**. «El archivo no existe.» era, letra por letra, el valor de `MsgFileNotFound`.

**La causa no es el descuido: es la forma.** Un servicio que devuelve `string` no tiene manera de
devolver algo traducible, porque corre en un hilo de fondo y no sabe en qué idioma está la interfaz. Por
eso el arreglo no es «cambiar 18 literales» —eso lo deshace el próximo `return`— sino **cambiar el tipo**:

- `Core/UserMessage` (clave + argumentos) es lo que viaja desde los servicios;
- `ConversionResult.Error` y `FileValidationResult.Error` dejan de ser `string`, así que el compilador
  ya no deja devolver una frase por descuido;
- la traducción ocurre en **un único borde**, `LocalizationService.Translate`, donde sí se sabe el idioma;
- 13 claves nuevas × 8 idiomas.

**TJ-17 — y el guardián que debía haberlo impedido.** `HardcodedUiTextTests` existía desde el Tier D…
mirando **dos archivos de veintitantos**, escritos a mano, y **ninguno** de los 18 literales vivía en
ellos. Además su patrón solo casaba *asignaciones* (`Title = "…"`), así que no habría visto
`Failed("El archivo de origen no existe")` ni con el archivo en la lista. Ahora los archivos se
**descubren** y hay un segundo patrón para los literales que viajan **como argumento**, donde solo se
admite una clave. Comprobado en rojo reintroduciendo dos de los 18.

> **El patrón que se repite en este tier:** el guardián existía y pasaba en verde sobre problemas de su
> propia especialidad. `HardcodedUiTextTests` miraba dos archivos; `AccessibilityTests` filtra por
> `ControlType.Button` (TJ-09); `LocalizationUsageTests` conocía tres formas de pedir una clave y ya
> había una cuarta (TJ-18). Un test verde dice «no encontré nada», no «no hay nada».

De ahí una regla nueva, ya aplicada aquí: **cada forma nueva de pedir una clave se añade al escáner en el
mismo cambio que la crea.** `UserMessage("…")` y `Failed("…")` entraron en `LocalizationUsageTests` en
este mismo commit, junto con la `T("…")` que TJ-18 llevaba pendiente. Van siete formas.

**Pruebas:** 231 pasan · 4 omitidas · 0 fallan; UI 31 · 0. Build 0/0. `UserMessageTranslationTests` fija
el criterio de TJ-06 donde se puede comprobar sin abrir la app: con el idioma en japonés, lo que devuelven
los servicios llega en japonés, con sus argumentos dentro y sin clave cruda a la vista.

---

### 2026-08-31 — TJ-01: PowerPoint es uno solo, y puede ser el del usuario

El peor hallazgo del [Tier J](ROADMAP.md), y el único que se cerró **conduciendo Office de verdad**.

**La premisa, medida otra vez aquí antes de tocar nada:** dos activaciones de `PowerPoint.Application`
dejan **un** `POWERPNT.EXE`; Word y Excel dejan **dos**. PowerPoint no se puede instanciar dos veces: lo
que devuelve `Activator.CreateInstance` es **el PowerPoint que ya está corriendo** — que puede ser el del
usuario, con su presentación a medias.

Sobre esa instancia, la app ponía `DisplayAlerts = ppAlertsNone` y al terminar llamaba a `Quit()`. Es
decir: **le cerraba su PowerPoint sin preguntar por lo no guardado**. Y con `MaxParallelConversions > 1`,
N conversiones de `.pptx` conducían la misma aplicación.

**Lo hecho:**

- `Services/SerialGate` — las conversiones de PowerPoint pasan de una en una. Word y Excel siguen en
  paralelo: ahí cada activación sí crea su proceso, y el paralelismo es real.
- `PowerPointSession` — mira `POWERPNT.EXE` **antes** de activar y de ahí sale todo lo demás: solo cierra
  lo que ha abierto, y a la instancia prestada le devuelve `DisplayAlerts` y `AutomationSecurity` como
  estaban. Ante cualquier duda (no se puede listar procesos, no se puede leer un ajuste) **se asume que
  es del usuario**: cerrar de más cuesta su trabajo; no cerrar solo deja un proceso abierto.

**Lo que enseñó escribir la prueba —y que ninguna lectura del código iba a dar—:** la primera versión del
arreglo **seguía cerrándole las presentaciones al usuario**. La sesión soltaba la instancia prestada con
`Marshal.FinalReleaseComObject`, y el RCW de una aplicación COM es **compartido en el proceso**: «Final»
suelta las referencias de **todos**, PowerPoint se queda sin clientes de automatización y descarta lo que
se abrió por esa vía. El proceso seguía vivo —así que un test que solo mirara «¿sigue abierto?» habría
pasado— y el trabajo se perdía igual. La prueba miraba **el número de presentaciones**, y por eso salió.
Se suelta **una** referencia, con `ReleaseComObject`.

**Pruebas nuevas contra el Office real** (`PowerPointSharedInstanceTests`, gated por
`OFICONVERT_OFFICE_TESTS=1`, con el patrón de `NetworkFactAttribute` — **omitir no es fallar**, y el corte
no puede depender de que haya Office):

- la premisa (2 activaciones → 1 proceso; Word → 2), medida y no supuesta;
- **el criterio de aceptación entero**: con PowerPoint abierto y una presentación sin guardar, convertir
  3 `.pptx` a la vez → las 3 salen, PowerPoint sigue abierto y su presentación intacta. Con el código
  antiguo, **falla**;
- el mismo lote sin sesión del usuario: las 3 salen y la instancia propia **sí** se cierra.

⚠️ **Lo que NO se reprodujo:** el «la primera en terminar mata a las demás». Ni con tres presentaciones de
40 diapositivas en paralelo y sin la puerta: `Quit()` sobre una instancia con otros clientes de
automatización enganchados no la termina. La serialización se queda —conducir en paralelo una instancia
que Windows no puede duplicar es incorrecto de por sí, y `SerialGateTests` cubre que la puerta funciona—
pero ese escenario concreto está **sin reproducir**, no confirmado. Queda dicho aquí para que nadie lo lea
como verificado.

**Pruebas:** 224 pasan · 4 omitidas · 0 fallan (227 pasan con `OFICONVERT_OFFICE_TESTS=1`); UI 31 · 0.
Build 0/0.

---

### 2026-08-31 — TJ-03 y TJ-02: el motor de LibreOffice borraba archivos y podía congelarse

Las dos peores del [Tier J](ROADMAP.md) después de la de PowerPoint, y las dos en el **mismo archivo**,
`Services/LibreOfficeConversionService.cs`: 96 líneas que nadie había vuelto a mirar desde la v1.0 porque
LibreOffice es el motor *alternativo* y casi nunca entra. Entra cuando no hay Office — o sea, para el
usuario que menos margen tiene.

**TJ-03 — se perdían archivos.** `--outdir` recibía la carpeta del usuario, y **LibreOffice no acepta un
nombre de salida**: escribe con el del original. Con un `informe.pdf` ya presente, LibreOffice lo
**sobrescribía** y acto seguido el `File.Move` de la app se llevaba el recién nacido a `informe (1).pdf`.
Resultado: dos archivos donde antes había dos, todo aparentemente correcto, y **el contenido del primero
perdido para siempre**. Rompía la garantía nº 2 de `OutputPath` y la promesa del README, y no dejaba
rastro en ningún sitio.

> Lo que lo hizo invisible: `OutputPath.GetSafe` **sí** calculaba el nombre libre, y está probado.
> Simplemente **nadie se lo pasaba a LibreOffice**, porque no hay forma de pasárselo. Una garantía
> comprobada en su propia unidad, y burlada en el borde donde se usa.

**TJ-02 — la conversión podía congelarse para siempre.** Se redirigían `stdout` y `stderr` y no se leía
**ninguno** hasta después de `WaitForExitAsync` (y `stdout`, jamás). Cuando el búfer de la tubería (~4 KB)
se llena, `soffice` se **bloquea escribiendo** y la espera no vuelve: la conversión se queda ahí, sin
error, sin registro y **ocupando una plaza del semáforo de paralelismo**. Con unas cuantas así, la app
deja de convertir sin decir por qué. Un documento con bastantes avisos de fuentes o macros basta.

**Lo hecho:**

- `Core/LibreOfficeOutput` — carpeta de trabajo **exclusiva** por conversión, elección del archivo
  producido (por nombre esperado; si solo hay uno, ese; con varios y ninguno esperado **no se adivina**)
  y movimiento al destino **recomprobando que sigue libre**. El `GetSafe` original se calculó *antes* de
  convertir; entre medias pasan segundos, el resto del lote y el propio usuario.
- `Services/ProcessRunner` — un único sitio donde se lanza un proceso externo, con la regla escrita:
  **leer los dos flujos antes de esperar**. Está fuera del servicio para poder probarlo sin LibreOffice.
- **Terminar en 0 sin producir nada ya no se da por bueno.** Pasaba con formatos que el filtro no
  soporta para ese documento: la app apuntaba en el historial un archivo que no existía.

**Guardianes** (los dos comprobados en rojo antes de darlos por buenos):

- `ProcessRunnerTests` — 64 KB de salida, dieciséis veces el búfer, por `stdout`, por `stderr` y por los
  dos a la vez. Con el orden antiguo las tres **se cuelgan** y el plazo de 30 s las pone en rojo. Es la
  forma de reproducir un deadlock sin depender de qué documento tenga a mano quien lo pruebe.
- `LibreOfficeOutputTests` — 11 pruebas, entre ellas la regresión literal de TJ-03 (con `informe.pdf`
  ocupado, los **dos** archivos quedan intactos) y la de dos conversiones homónimas en paralelo.

**Pruebas:** 221 pasan · 1 omitida · 0 fallan; UI 31 · 0. Build 0/0.

⚠️ **No se ha convertido un documento de verdad con LibreOffice** (no está instalado en esta máquina): lo
verificado es la lógica, que es donde estaban los dos fallos. La conversión real por LibreOffice sigue
sin cubrir, aquí y en todo el proyecto.

---

### 2026-08-31 — TJ-05 y TJ-04: probar lo que se publica, e instalar sin nadie delante

Dos tareas Altas del [Tier J](ROADMAP.md), las dos del mismo tipo: **fallos del andamio**, invisibles
mientras todo sale verde.

**TJ-05 — los UI tests conducían el binario equivocado.** `release.ps1` compilaba en Release y acto
seguido corría `dotnet test` **sin `-c Release`**; MSBuild reconstruía la app en Debug por el
`ProjectReference`, y `AppFixture` —que cogía *el `OfiConvert.exe` de `bin\**\win-x64\` más reciente*—
acababa conduciendo ese Debug recién hecho. Las 30 pruebas de interfaz llevaban meses validando un
binario que **no es el que empaqueta el instalador**, y nada lo decía porque el criterio era la fecha,
no la configuración. Ahora `AppFixture` deduce la configuración de su propia ruta, busca **solo** dentro
de `bin\{Config}\` (sin `publish\`), **registra qué `.exe` conduce** y `DrivenBinaryTests` falla si no
es el que toca.

> Es la **misma familia** que el bug del Tier G, donde los UI tests conducían un `.exe` viejo. Allí se
> arregló que el binario fuera **fresco**; nadie comprobó que fuera **el correcto**. Cuando un arreglo se
> formula como «que esté al día» en vez de «que sea el que se publica», la segunda mitad se queda fuera.

**TJ-04 — el aviso del instalador salía en modo silencioso.** `InitializeWizard` planta un `MsgBox` si no
detecta Word, e **Inno llama a `InitializeWizard` también en `/VERYSILENT`**: el modificador silencia el
asistente, no los `MsgBox`. La auto-actualización lanza el instalador con la app **ya cerrada**, así que
el usuario que solo tiene LibreOffice —soportado a propósito— veía su programa esfumarse y quedarse un
diálogo huérfano, o la actualización parada. Es **literalmente el fallo del Tier H** (`/VERYSILENT` que
no era silencioso) en un segundo sitio del mismo archivo.

Arreglo con dos capas, porque el instalador también se lanza a mano: el aviso vive dentro de
`if (not WizardSilent)`, y la línea de comandos del updater —ahora en
`Core.InstallScope.SilentInstallArguments`, no incrustada en el code-behind— manda `/SUPPRESSMSGBOXES`.
Sacarla a `Core/` no es cosmética: **así es como se perdió el modificador**, escribiéndola a mano en un
sitio que ninguna prueba miraba.

**Guardianes nuevos** (todos comprobados en rojo antes de darlos por buenos):

- `DrivenBinaryTests` — el `.exe` conducido es el de la configuración compilada. Rojo reponiendo la
  búsqueda por fecha con un `bin\Debug` más nuevo, que es exactamente el escenario del corte.
- `InstallerScriptTests` — el `.iss` vigilado como código: ningún `MsgBox` sin guarda `WizardSilent`
  (**vaciando antes los comentarios**: el primer intento pasaba en verde porque el propio comentario que
  explica la guarda menciona `WizardSilent`), `commandline` en `PrivilegesRequiredOverridesAllowed`, y
  ningún `/VERYSILENT` escrito a mano fuera de `Core/`.
- `InstallScopeTests` — la línea de comandos del updater lleva sus cinco modificadores.

**Pruebas:** 206 pasan · 1 omitida · 0 fallan; UI 31 · 0. Build 0/0. El `.iss` recompilado con ISCC
(instalador de 58,2 MB, generado y descartado).

⚠️ **Lo que sigue sin comprobarse:** el criterio de aceptación entero de TJ-04 exige una máquina **sin
Office**, y esta tiene Office. Queda verificado por construcción (guarda + modificador + el script
compila), no de punta a punta — el mismo hueco que ya anota §3 sobre el instalador.

---

### 2026-08-31 — TJ-07: el changelog manda sobre las notas del release

Primera tarea cerrada del [Tier J](ROADMAP.md). `CHANGELOG.md` nació el 2026-08-29, pero nadie lo leía:
`release.ps1` seguía generando la **misma plantilla genérica** para toda versión, así que las nueve
publicadas cuentan lo mismo («Instalador self-contained para Windows x64…») y ninguna dice qué cambió.

Ahora las notas **son** la sección `## [X.Y.Z]` del changelog, más un pie fijo con la instalación y el
`.sha256`. Si la sección falta, el corte **muere ahí mismo**.

Tres decisiones que no son obvias leyendo el diff:

- **La comprobación va DELANTE del build y las pruebas**, no donde estaban las notas. Puesta en su sitio
  natural —justo antes del `gh release create`— el corte habría compilado, publicado el instalador y
  corrido 200 pruebas para morir al final por una sección de Markdown. Ahora falla en segundos.
- **`Get-ChangelogSection` lee con `ReadAllText`, no con `Get-Content -Raw`.** Es la misma trampa de
  PS 5.1 que ya está documentada para el `.csproj` (§4, *Trampas de PowerShell 5.1*): con la página de
  códigos ANSI, cada acento del changelog llegaría roto a las notas del GitHub Release, que es
  precisamente la superficie más pública del proyecto.
- **El corte no es el único guardián.** `tests/OfiConvert.Tests/ChangelogTests.cs` comprueba en
  `dotnet test` que la versión declarada en el `.csproj` está contada y que ninguna versión publicada se
  quedó sin fecha absoluta. Ambas se rompieron a propósito antes de darlas por buenas (`<Version>` a
  9.9.9 y una fecha borrada): salen en rojo con el mensaje que deben.

**Efecto sobre el flujo de trabajo:** cortar una versión ahora empieza por escribir su sección en
`CHANGELOG.md`. No es burocracia: es el único momento en que se sabe qué cambió.

**Pruebas:** 201 pasan · 1 omitida · 0 fallan. `.
elease.ps1 -Version 9.9.9 -DryRun` aborta con
«Falta la sección 9.9.9 en CHANGELOG.md», sin tocar git.

---

### 2026-08-29 — Re-auditoría externa: se abre el **Tier J** y nace `CHANGELOG.md`

Revisión completa del repositorio con el plan **cerrado** y los tiers 0 y A–I dados por buenos. Alcance
acordado: **12 de las 13 áreas** del prompt de revisión (SEO no aplica: no hay superficie web),
**profundidad exhaustiva**, **sin exclusiones** (los 133 archivos versionados) y normativa **ninguna**
—el área legal se acota a licencias y atribuciones—. **No se ha tocado código:** el resultado es el
[Tier J](ROADMAP.md), con **38 tareas** (7 Altas · 19 Medias · 12 Bajas).

**Verificado antes de auditar, no dado por bueno:** build **0/0** y **199 pasan · 1 omitida · 0 fallan**.
Coincide con lo que decía este documento.

**Lo que distingue a esta pasada de los tiers G e I:** aquellos revisaron **la interfaz**, sobre
capturas. Esta ha leído **el motor** —COM, LibreOffice, el pipeline y los propios guardianes—, y lo peor
no salió de leer más código sino de **dos comprobaciones que nadie había hecho**:

- 🔴 **Activar Office dos veces por COM.** `PowerPoint.Application` devuelve **la misma instancia**
  (medido: 1 solo `POWERPNT.EXE`); Word y Excel sí crean procesos separados. Eso significa que las
  conversiones **paralelas** de `.pptx` se matan entre sí —la primera en terminar hace `Quit()`— y que,
  si el usuario tiene PowerPoint abierto, la app **cierra su sesión sin preguntar por lo no guardado**
  (`DisplayAlerts = ppAlertsNone`). Es el hallazgo más grave y está anotado como invariante en §4
  *Conversión COM*. → `TJ-01`
- 🔴 **Mirar los primeros bytes de los `.ps1`.** `tools/capture-dropdown.ps1` —el más nuevo, el de la
  propia v2.6.1— es **el único sin BOM UTF-8**, justo el invariante que §4 documenta. Reproducido en
  PowerShell 5.1 / CP1252: parsea, pero sus mensajes salen como `"No se encontrÃ³ OfiConvert.exe"`, y
  está a un `—` dentro de una cadena de convertirse en el error del tokenizer que ya pagaron los
  hermanos. → `TJ-27`

**Dos regresiones conceptuales** de tiers que se dieron por cerrados —el problema volvió por otra
puerta, así que van como tareas NUEVAS que citan a la anterior, no como reapertura—:

- **`/VERYSILENT` vuelve a no ser silencioso** (Tier H). Esta vez no es el diálogo de modo de
  instalación sino el `MsgBox` de «no se detectó Office» de `InitializeWizard`: Inno **no suprime los
  MsgBox en modo silencioso** salvo `/SUPPRESSMSGBOXES`, y el updater no lo pasa. Le ocurre justo al
  usuario que la app dice soportar: el que tiene **solo LibreOffice**. → `TJ-04`
- **Los UI tests siguen sin conducir el binario que se publica** (Tier G). El Tier G garantizó que
  fuera **fresco**; nadie garantizó que fuera **el mismo**: `release.ps1` compila Release y luego corre
  `dotnet test` **sin `-c Release`**, así que el `ProjectReference` reconstruye en **Debug** y
  `AppFixture` —que coge el `.exe` de `bin\**\win-x64\` **más reciente**— acaba conduciendo ese.
  → `TJ-05`

**Y el patrón que conviene tener presente, porque explica casi todo lo demás:** *el guardián cubre el
sitio donde dolió, no el riesgo.*

| Guardián | Qué vigila | Por dónde se le escapa |
|---|---|---|
| `HardcodedUiTextTests` | 2 archivos, y solo asignaciones a propiedades | Los **18 literales en español** de `Services/` y `ViewModels/` (`TJ-06`) |
| `AccessibilityTests` | `ControlType.Button` *(hasta TJ-09)* | Los 4 `ComboBox` y 2 `NumberBox`, mudos para un lector (`TJ-09`) — cazó los `ToggleSwitch` solo porque UIA los expone **como botones**. Ahora mira también `ComboBox`, `Spinner` y `Edit` |
| `LocalizationUsageTests` | 3 formas de pedir una clave | La cuarta, `T("…")`, estrenada en el mismo arreglo que añadió la tercera (`TJ-18`) |
| `LegalTextTests` | Que no se **borre** una atribución | `System.Drawing.Common 9.0.0`, que se redistribuye sin estar citado (`TJ-23`) — el propio test avisa de este hueco en su comentario |

**La quinta reincidencia del texto en español a fuego**, y con el mismo agravante del Tier D: **las
traducciones ya existen y no se usan**. `MsgFileNotFound` = «El archivo no existe.» es *idéntica* al
literal de `FileValidationService.cs:19`; igual `MsgFileLocked`, `MsgPasswordProtected`,
`MsgCorruptFile` y `MsgOfficeNotFound`. Son 18 mensajes que el usuario lee en el panel de resultados, en
la columna *Error* del historial —que el Tier I hizo visible— y en el CSV/TXT exportado. → `TJ-06`

**Otros hallazgos con pérdida de datos**, que no encajan en ninguna categoría anterior: la ruta de
LibreOffice **destruye el resultado de una conversión previa** (convierte con el nombre del original y
luego lo mueve, así que rompe la garantía «nunca se sobrescribe» de `Core/OutputPath` y la promesa del
README) → `TJ-03`; y puede **colgarse para siempre** por deadlock de las tuberías, porque redirige
`stdout` y `stderr` y no lee ninguno antes de `WaitForExitAsync` → `TJ-02`.

**`CHANGELOG.md`, creado hoy.** Era el tercer documento vivo que faltaba: hasta ahora su papel lo hacía
el *Índice de versiones* de este archivo, y `release.ps1` publicaba **notas de plantilla genéricas**
idénticas para toda versión. Se ha reconstruido de la 1.0.0 a la 2.6.1 a partir de los nueve tags y de
este registro —marcado como reconstrucción aproximada—, y queda pendiente que `release.ps1` **aborte si
falta la sección de la versión** (`TJ-07`). **Reparto a partir de ahora: el _qué_ al changelog, el
_porqué_ aquí.**

**Limpio, y conviene decirlo:** `dotnet list package --vulnerable --include-transitive` y `--deprecated`
no devuelven nada. Los ocho diccionarios tienen las **mismas 137 claves**. `Core/` respeta su frontera
(sin UI, sin `Process`, sin `HttpClient`, sin COM). La verificación del updater sigue montada como manda
el Tier C, con la descarga en su propio método.

**Qué quedó a medias:** nada de esto se ha corregido — la revisión era de diagnóstico. Y hay tres cosas
que **no se han podido comprobar** y que están marcadas como tales en sus tareas: si las miniaturas se
ven hoy (`TJ-14`), el comportamiento de LibreOffice en paralelo (`TJ-25`, no está instalado en esta
máquina) y la instalación silenciosa en un equipo **sin** Office (`TJ-04`, que es donde se manifiesta).

---

### 2026-08-24 — Los desplegables se veían borrosos — **v2.6.1** (publicada 2026-08-29)

Reportado sobre una captura de **Ajustes**: al abrir el `ComboBox` de *Tema*, el menú salía **desenfocado**
y se transparentaba el contenido de la tarjeta de debajo. No era un bug del layout: es el **estilo por
defecto de WinUI**, que pinta los popups con acrílico (`AcrylicBackgroundFillColorDefaultBrush`). Sobre el
backdrop **Mica** de la ventana, ese acrílico apila dos capas translúcidas y el texto del menú pierde
contraste contra lo que hay detrás.

Arreglado en **`App.xaml`**, no en `MainWindow.xaml`: `ThemeDictionaries` (Light `#F9F9F9` / Dark `#2C2C2C`
/ HighContrast a los colores del sistema) que fuerzan opacos `ComboBoxDropDownBackground`,
`FlyoutPresenterBackground`, `MenuFlyoutPresenterBackground` y los `Acrylic*FillColorDefaultBrush`. Va a
nivel de App a propósito: cubre los cuatro `ComboBox` (formato, tema, idioma, formato por defecto) y
cualquier flyout futuro sin repetir el override control por control.

**🐞 Y el primer intento no hizo NADA.** Se colocaron las `ThemeDictionaries` en la **raíz** de
`Application.Resources` — que ya tenía `MergedDictionaries`. Compila 0/0, no avisa de nada y el override
**no se resuelve**: el desplegable salía idéntico. Se dio por arreglado y el usuario respondió *"se sigue
viendo igual"*. Lo que lo destapó no fue leer más XAML sino **mirar la app**: un script de UI Automation
que abre el `ComboBox` y lo fotografía, y al ampliar el recorte apareció el **moteado** del acrílico. La
prueba objetiva es contar colores en un recuadro del fondo: antes ~6 valores (`#2B2B2B`–`#303030`, el ruido
del acrílico); después **1800 de 1800 píxeles exactamente `#2C2C2C`**. El diccionario tiene que ir dentro
de `MergedDictionaries`, **después** de `XamlControlsResources`.

**Y ahora sí hay con qué comprobarlo: `tools/capture-dropdown.ps1`.** Tercer primo de los scripts de
captura: abre los cuatro `ComboBox` por UI Automation, en claro y oscuro, y **mide** el fondo del popup en
vez de dejarlo a ojo. La métrica es la *cuota de ruido*: el % de píxeles a ±3 del color dominante sin ser
el dominante — el grano del acrílico. Sale con código 1 si alguno sigue acrílico.

Se comprobó **en rojo**, como manda la casa: con el `App.xaml` roto a propósito (las `ThemeDictionaries` de
vuelta a la raíz) marca **38–68% de ruido** en los ocho casos; con el arreglo, **0%**. Margen de sobra, no
un umbral apretado. De paso destapó dos trampas propias, ya resueltas dentro del script:

- **El popup no cuelga del `ComboBox` ni expone ningún `List`**: en el árbol de UIA solo aparecen sus
  `ListItem` sueltos (y por duplicado). El rectángulo del desplegable se reconstruye como la **unión** de
  los rects de esos elementos.
- **El primer intento traía un fallback al propio `ComboBox`** cuando no encontraba la lista — y eso daba
  un **OK falso**: el control cerrado tiene fondo sólido (`#383838` en oscuro), así que medía limpio y
  cantaba "opaco" sin haber mirado el popup ni una vez. Si no encuentra los elementos, ahora **falla**.

Build **0/0**, **229 pruebas** (199 + 30 UI) en verde. Las pruebas de xUnit no cubren esto: no miran
píxeles. Para esto está el script.

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
