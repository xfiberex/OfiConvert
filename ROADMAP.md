# OfiConvert — Hoja de ruta

> **Qué hay aquí:** el trabajo pendiente agrupado por **tiers**, con su porqué y dónde vive cada cosa.
>
> **Qué NO hay aquí:** el detalle de lo ya hecho y sus decisiones — eso vive en
> [`CONTEXT.md`](CONTEXT.md) (§4 *Decisiones* y el *Registro de cambios*).
>
> **Propósito del proyecto:** conversor de escritorio **por lotes** de documentos Office
> (Word/Excel/PowerPoint) a **PDF/HTML/CSV/PNG/JPG**, con Office COM como motor principal y
> LibreOffice como alternativo.
>
> **De dónde nace este plan:** la auditoría del 2026-07-13 comparando con los hermanos
> **FormatDiskPro** y **WingetUSoft** (ambos TERMINADOS). OfiConvert tiene el producto; le falta la
> infraestructura que ellos ya pagaron, depuraron y documentaron. **Portar > reinventar.**

## Estado

| Tier | Tema | Estado | Versión |
|---|---|---|---|
| **0** | Docs vivos (`CONTEXT.md` + `ROADMAP.md`) | ✅ Completado (2026-07-13) | — |
| **A** | Higiene: bugs de la auditoría, README real, `LICENSE`, build 0/0 | ✅ Completado (2026-07-13) | **2.1.0** ✔ publicada |
| **B** | Pipeline de release: instalador scriptado + release en un paso + `.sha256` | ✅ Completado (2026-07-13) | **2.1.0** ✔ publicada |
| **C** | Actualización confiable: verificar el instalador antes de ejecutarlo | ✅ Completado (2026-07-13) | **2.2.0** ✔ publicada |
| **D** | Pruebas: extraer `Core/`, cobertura real, UI tests FlaUI | ✅ Completado (2026-07-14) | 2.3.0 *(sin publicar)* |
| **E** | Cara pública: README de usuario, capturas reproducibles, legal in-app | ⬜ **Siguiente** | 2.4.0 |
| **F** | Infraestructura agéntica (`.claude`, skills, codegraph) | ⬜ Pendiente | — |

\* Orden recomendado: **A → B → C → D → E** (F puede ir en cualquier momento). Idealmente D habría ido
antes que C, pero C se trajo sus propios tests, como hicieron los hermanos.

---

## ✅ Tier A — Higiene y bugs reales *(completado 2026-07-13)*

Los 8 hallazgos de la auditoría, cerrados salvo el del updater (que necesita el `.sha256` del Tier B).
El porqué de cada decisión y las trampas que costó, en [`CONTEXT.md`](CONTEXT.md) (§4 y el registro).

| # | Ítem | Dónde |
|---|------|-------|
| 1 | ✅ **Los 8 idiomas persisten** — `SettingsService` tenía su propia lista (`es`/`en`) y reseteaba los otros seis **al cargar**, pisando la elección del usuario en disco | `Services/SettingsService.cs`, `Helpers/LocalizationService.cs` |
| 2 | ✅ **Menú contextual funcionando, con INSTANCIA ÚNICA**: la segunda invocación redirige su activación a la ventana abierta y se cierra sin abrir otra | `Program.cs`, `App.xaml.cs`, `Helpers/ActivationArguments.cs` |
| 3 | ✅ **README y metadatos veraces** (describían el stack **WPF** de la v1.0) | `README.md`, `OfiConvert.csproj` |
| 4 | ✅ **`LICENSE` (MIT)** — el README lo prometía y no existía | raíz del repo |
| 5 | ✅ **Build 0/0**: `[ObservableProperty]` → propiedades parciales. Obligó a subir `CommunityToolkit.Mvvm` a **8.4.2**: la 8.4.0 las ignora en silencio | `ViewModels/`, `Models/`, `OfiConvert.csproj` |
| 6 | ✅ **Aviso honesto al terminar**: sonido + parpadeo de la barra de tareas, solo si la ventana no está delante. Fuera el `ContentDialog` modal | `Helpers/Notifier.cs`, `MainWindow.xaml.cs` |
| 7 | ✅ `crash.log` a `%AppData%\OfiConvert\`; rutas de datos y extensiones admitidas unificadas | `Helpers/AppPaths.cs`, `Models/OfficeFormats.cs` |

> **Dos bugs latentes destapados de camino** (ninguno estaba en el plan):
> - Los **defaults pisaban los ajustes del usuario**: al mover los valores por defecto al constructor,
>   cada asignación disparaba un guardado con el estado **a medio cargar**.
> - **Los archivos añadidos a mitad de un lote se borraban sin convertir** — este ya estaba en
>   producción y afectaba al *drag & drop*, no solo al menú contextual nuevo. El lote se fija al empezar.

---

## ✅ Tier B — Pipeline de release *(completado 2026-07-13)*

El corte era artesanal: bump a mano **en dos archivos**, compilar el `.iss` desde el IDE y subir el
instalador al release. Ahora: `.\release.ps1 -Version X.Y.Z`. Portado de los hermanos **con sus
lecciones ya pagadas** (BOM UTF-8 en los `.ps1`, lectura del `.csproj` conservando el BOM, `Invoke-Git`
para el stderr normal de git, publish a `%TEMP%` por MAX_PATH).

| # | Ítem | Dónde |
|---|------|-------|
| 1 | ✅ **`build-installer.ps1`**: publish self-contained a `%TEMP%` → instalador → **`.sha256`**; versión leída del `.csproj` (**fuente única**: se acabó la doble fuente con el `.iss`); firma opcional | `installer/build-installer.ps1`, `installer/OfiConvert.iss` |
| 2 | ✅ **`release.ps1`**: validar → compilar y probar → bump de **las tres** etiquetas de versión → instalador → commit + tag `vX.Y.Z` → push → `gh release create` con `.exe` **y** `.sha256` | `release.ps1` |
| 3 | ✅ Guardas: `-DryRun` (compila el instalador de verdad y revierte el `.csproj`), `-AllowDirty`, aborta si falta el `.sha256`, avisa de los archivos sin rastrear (solo hace `git add -u`) | `release.ps1` |

> **Tres guardas que no estaban en el plan**, todas contra el mismo fallo —*el corte "sale bien" y lo
> que se rompe es el equipo del usuario*—: el **publish se verifica** antes de empaquetar (`.exe` +
> `.pri` + los 8 idiomas); fuera el `skipifsourcedoesntexist` del `.iss`, que dejaba compilar un
> instalador **sin la app dentro**; y **las tres etiquetas de versión suben juntas** (el updater compara
> contra `<AssemblyVersion>`).
>
> **Primer corte con el pipeline:** la **2.1.0**, con los Tiers A y B y los 3 commits que `main` ya
> acumulaba sobre `v2.0.0`. **Publicada** (2026-07-13), con instalador y `.sha256`.

---

## ✅ Tier C — Actualización confiable *(completado 2026-07-13)*

Era el agujero más serio: `GitHubUpdateService` descargaba un `.exe` de internet y **lo ejecutaba sin
comprobar nada**. Port del de WingetUSoft, con sus tropiezos ya conocidos.

| # | Ítem | Dónde |
|---|------|-------|
| 1 | ✅ **Verificar antes de ejecutar**: firma Authenticode válida → OK; si no, **SHA-256** contra el asset `*.exe.sha256`; sin ninguna de las dos, **borrar y abortar**. Motivo del rechazo visible en la UI (en los 8 idiomas) y en el log | `Services/GitHubUpdateService.cs`, `MainWindow.xaml.cs`, `Lang/*.xaml` |
| 2 | ✅ Descarga en **método propio**, con el `FileStream` cerrado **antes** de verificar — el bug del auto-bloqueo que dejó muerta la auto-actualización de WingetUSoft durante dos versiones | `Services/GitHubUpdateService.cs` |
| 3 | ✅ **11 pruebas** (las primeras del proyecto): ejercen la **descarga completa** contra un servidor HTTP local sobre `TcpListener` (no `HttpListener`: exigiría terminal elevada) | `tests/OfiConvert.Tests/` |
| 4 | ✅ **`[NetworkFact]`**: verifica el **release real de GitHub** con el código real de la app; se omite salvo `OFICONVERT_NETWORK_TESTS=1` | `tests/OfiConvert.Tests/PublishedReleaseTests.cs` |

> **Consecuencia operativa:** **todo release debe subir su `.sha256`** (o ir firmado), o los clientes
> rechazarán la actualización. `release.ps1` lo garantiza (aborta si falta).
>
> **Alcance honesto:** el `.exe` y su hash salen del mismo release → detecta corrupción y manipulación
> **en tránsito**, no un compromiso de la cuenta de GitHub. La firma sigue siendo el objetivo.
>
> ⚠️ **Aún no se ha ejercido en producción:** solo actúa al actualizar **desde** una versión ≥ 2.2.0.
> El primer uso real será **2.2.0 → 2.3.0**.

---

## ✅ Tier D — Pruebas *(completado 2026-07-14)*

De **11 pruebas** (solo el updater) a **170**: 152 unitarias + 18 de UI sobre la app real. Y cumplieron su
oficio de inmediato — **encontraron dos bugs que nadie veía**, los dos en la localización.

| # | Ítem | Dónde |
|---|------|-------|
| 1 | ✅ **`Core/` extraído** (lógica pura, sin UI/`Process`/`HttpClient`/COM): `OutputPath` (rutas de salida seguras), `ByteSize`, `CsvField`, `FileSignature` (magic bytes), `OfficeFormats` + `OutputFormatHelper` (mapeo de formatos) | `Core/` |
| 2 | ✅ **141 pruebas nuevas** sobre `Core/`, `FileValidationService` (archivos reales: vacío, bloqueado, cifrado, `.docx` renombrado) y `ActivationArguments` (el menú contextual del Explorador) | `tests/OfiConvert.Tests/` |
| 3 | ✅ **Completitud de localización**: cada clave en los 8 idiomas + **cada clave usada en el código y en el XAML existe** en los diccionarios. Es la trampa `L.T` de los hermanos, y aquí **había caído ya** (ver abajo) | `LocalizationTests`, `LocalizationUsageTests` |
| 4 | ✅ **18 UI tests (FlaUI/UIA3)** contra el `.exe` real, **sin elevación** y **sin Office**: ninguno convierte nada, así que no dependen del entorno de la máquina que corta la versión | `tests/OfiConvert.UiTests/` |

> ### 🐞 Los dos bugs que destaparon las pruebas
>
> **1 — La UI estaba en español en los ocho idiomas.** `MainWindow.xaml` declaraba
> `<helpers:LocalizationService x:Key="Loc"/>`, que **construye una segunda instancia**: los ~40 bindings
> de la interfaz escuchaban a ese objeto, mientras el código cambiaba el idioma en el singleton
> `LocalizationService.Instance` — **otro objeto, que la UI no escuchaba jamás**. Botones y etiquetas se
> quedaban en español en los 8 idiomas y **ni reiniciando cambiaban**; solo se traducían los textos que
> pasan por código (mensajes y estados). El `settings.json` guardaba el idioma elegido, así que desde
> fuera todo parecía correcto. Arreglado haciendo que **el idioma sea estado compartido** por todas las
> instancias. *Lo caza `LocalizationUiTests`, conduciendo la app real.*
>
> **2 — El diálogo de cierre, sin traducir.** Pedía las claves `TitleConfirmClose`/`BtnYes`/`BtnNo`, que
> **no existían**, y caía a un texto español a fuego. Sus traducciones **ya estaban en los 8 idiomas** con
> otro nombre (`MsgCancelConfirm`/`MsgCancelConfirmTitle`) y **sin usarse en ningún sitio**. Es el diálogo
> que protege contra los procesos de Office huérfanos — *EL* riesgo de esta app. *Lo caza
> `LocalizationUsageTests`.*
>
> **Y un test que mentía:** `DownloadInstaller_ReportsProgress` (del Tier C) afirmaba sobre `reports[^1]`
> de una `List<double>` rellenada desde `Progress<T>`, que **despacha al thread pool**: orden no
> garantizado y `List.Add` concurrente. Pasaba por suerte; se puso en rojo en cuanto la suite creció y metió
> presión en el thread pool. Ahora usa un `IProgress<double>` **síncrono**.

---

## 📣 Tier E — Cara pública *(SIGUIENTE)*

| # | Ítem | Fuente |
|---|------|--------|
| 1 | **README de usuario**: badges, *Instalación* desde Releases, *Actualizaciones* con el modelo de confianza (alcance honesto del SHA-256), requisitos claros (Office **o** LibreOffice) | Tier D de WingetUSoft |
| 2 | `THIRD-PARTY-NOTICES.txt` + licencia y avisos **embebidos en el `.exe`** con diálogo in-app. Redistribuye: .NET, Windows App SDK, H.NotifyIcon.WinUI, Serilog, CommunityToolkit.Mvvm — **verificar cada `.nuspec`, no de memoria** | `Core/LegalText` de cualquiera de los dos |
| 3 | `docs/screenshots/` + `tools/capture-screenshots.ps1`: capturas **regeneradas** conduciendo la app real por UI Automation (`DWMWA_EXTENDED_FRAME_BOUNDS`, respaldo del `settings.json` real, sin elevación) | WingetUSoft |

---

## 🤖 Tier F — Infraestructura agéntica

| # | Ítem | Fuente |
|---|------|--------|
| 1 | `.claude/CLAUDE.md` (leer `CONTEXT.md` al iniciar sesión y mantenerlo) + `.claude/settings.json` | WingetUSoft |
| 2 | `.agents/skills/` + `skills-lock.json` (skills C#/.NET del registro `github/awesome-copilot`; el framework de pruebas es **xUnit**) | Copia directa de los hermanos |
| 3 | `.mcp.json` (codegraph) — inicializar el índice (`codegraph init`) es decisión del usuario | FormatDiskPro |

---

## 🚫 Decisiones cerradas / qué NO portar de los hermanos

- **Inno Setup como único empaquetador**, app unpackaged (`WindowsPackageType=None`). Nada de
  MSIX/ClickOnce.
- **Publish self-contained** (decidido 2026-04-03) — desviación deliberada de WingetUSoft: aquí el
  usuario no debe instalar runtimes.
- **`PrivilegesRequired=lowest`** (instalación per-user por defecto): la app corre `asInvoker` y no
  necesita admin; no copiar el `PrivilegesRequired=admin` de los hermanos.
- **No portar** `requireAdministrator` ni la ventana fija de FormatDiskPro (decisiones correctas para
  *su* producto, no para este), ni el parseo de winget de WingetUSoft.
- **CI (GitHub Actions) — descartado**, con el mismo argumento que los hermanos: los UI tests del
  Tier D **arrancan la app y la conducen**, y eso exige un **escritorio interactivo** que un runner
  hospedado no tiene. `release.ps1` ejecuta **todas** las pruebas (descubre solo los `.csproj` de
  `tests\`) antes de cada corte.
  > Matiz que el plan daba por hecho y resultó falso: **no hacen falta Office ni LibreOffice**. Ningún UI
  > test convierte un archivo — deliberadamente, para que un corte de versión no dependa de lo que haya
  > instalado en la máquina. Lo que impide el CI es el escritorio, no Office.
- **Firma de código (OV/EV) — no por ahora**: SmartScreen dirá "editor desconocido" y la confianza de
  las actualizaciones se apoyará en el `.sha256` (Tiers B/C). El pipeline portado deja la firma como
  opción (`-CertThumbprint`).
