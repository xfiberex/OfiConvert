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

| Tier | Tema | Estado | Versión objetivo* |
|---|---|---|---|
| **0** | Docs vivos (`CONTEXT.md` + `ROADMAP.md`) | ✅ Completado (2026-07-13) | — |
| **A** | Higiene: bugs de la auditoría, README real, `LICENSE`, build 0/0 | ✅ Completado (2026-07-13) | 2.1.0 *(sin publicar)* |
| **B** | Pipeline de release: instalador scriptado + release en un paso + `.sha256` | ✅ Completado (2026-07-13) | — |
| **C** | Actualización confiable: verificar el instalador antes de ejecutarlo | ✅ Completado (2026-07-13) | 2.2.0 *(sin publicar)* |
| **D** | Pruebas: extraer `Core/`, cobertura real, UI tests FlaUI | ⬜ **Siguiente** | 2.3.0 |
| **E** | Cara pública: README de usuario, capturas reproducibles, legal in-app | ⬜ Pendiente | 2.4.0 |
| **F** | Infraestructura agéntica (`.claude`, skills, codegraph) | ⬜ Pendiente | — |

\* Orientativas. Orden recomendado: **A → B → C → D → E** (F puede ir en cualquier momento). Idealmente
D iría antes que C, pero C puede llevar sus propios tests, como hicieron los hermanos.

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
> acumulaba sobre `v2.0.0`.

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

## 🧪 Tier D — Pruebas *(SIGUIENTE)*

El proyecto de pruebas **ya existe** (lo trajo el Tier C), pero cubre **solo el updater**: la
conversión, la validación de archivos, las rutas de salida y la localización siguen a cero.

| # | Ítem | Notas |
|---|------|-------|
| 1 | Extraer **`Core/`** (lógica pura: sin UI, sin `Process`, sin `HttpClient`, sin COM): rutas de salida seguras (`GetSafeOutputPath`), formateo de bytes, mapeo de formatos, sanitización CSV, validación por magic bytes | La regla de oro de los hermanos |
| 2 | ~~Crear `tests/OfiConvert.Tests`~~ ✅ **hecho en el Tier C** (xUnit, 11 pruebas) | |
| 3 | Test de **completitud de localización**: cada clave presente en los 8 `Lang/*.xaml`, y cada clave usada en el código existente en los diccionarios — el indexer devuelve la clave si falta, así que un typo hoy **no rompe nada** y se ve como texto raro en la UI | Es la trampa `L.T` de los hermanos |
| 4 | `tests/OfiConvert.UiTests` (FlaUI/UIA3) sobre la app real: **sin elevación** (la app es `asInvoker`). Los tests que exijan **Office o LibreOffice** instalado se **OMITEN** si faltan — el patrón ya está montado aquí con `[NetworkFact]` | Modelo WingetUSoft (sin `EnsureElevated`) |

---

## 📣 Tier E — Cara pública

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
  Tier D necesitan escritorio interactivo, y aquí además **Office instalado** — un runner hospedado no
  tiene ninguna de las dos cosas. `release.ps1` correrá las pruebas antes de cada corte.
- **Firma de código (OV/EV) — no por ahora**: SmartScreen dirá "editor desconocido" y la confianza de
  las actualizaciones se apoyará en el `.sha256` (Tiers B/C). El pipeline portado deja la firma como
  opción (`-CertThumbprint`).
