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
| **A** | Higiene: bugs de la auditoría, README real, `LICENSE`, build 0/0 | ⬜ Pendiente | 2.1.0 |
| **B** | Pipeline de release: instalador scriptado + release en un paso + `.sha256` | ⬜ Pendiente | — |
| **C** | Actualización confiable: verificar el instalador antes de ejecutarlo | ⬜ Pendiente *(requiere B)* | 2.2.0 |
| **D** | Pruebas: extraer `Core/`, unitarios xUnit, UI tests FlaUI | ⬜ Pendiente | 2.3.0 |
| **E** | Cara pública: README de usuario, capturas reproducibles, legal in-app | ⬜ Pendiente | 2.4.0 |
| **F** | Infraestructura agéntica (`.claude`, skills, codegraph) | ⬜ Pendiente | — |

\* Orientativas. Orden recomendado: **A → B → C → D → E** (F puede ir en cualquier momento). C sin B
es imposible: la verificación necesita el asset `.sha256` que genera el pipeline. Idealmente D iría
antes que C, pero C puede llevar sus propios tests, como hicieron los hermanos.

---

## 🧹 Tier A — Higiene y bugs reales (auditoría 2026-07-13)

Todo confirmado contra el código; el detalle de cada hallazgo está en `CONTEXT.md` §6.

| # | Ítem | Dónde |
|---|------|-------|
| 1 | **Persistir los 8 idiomas** — hoy `ValidateSettings` resetea a `es` los 6 que no sean es/en | `Services/SettingsService.cs` |
| 2 | **Procesar los argumentos del menú contextual**: encolar el `%1` al arrancar (y decidir instancia única vs. una ventana por archivo) | `App.xaml.cs`, `ViewModels/MainViewModel.cs` |
| 3 | **README y metadatos veraces**: stack WinUI 3 real, paquetes actuales, `<Description>` con los 5 formatos | `README.md`, `OfiConvert.csproj` |
| 4 | **`LICENSE` (MIT)** en la raíz — el README ya lo promete | raíz del repo |
| 5 | **Build con 0 advertencias**: `[ObservableProperty]` → propiedades parciales (39 × MVVMTK0045) | `ViewModels/MainViewModel.cs` |
| 6 | **Notificación honesta al terminar**: hoy es un `ContentDialog` modal con claves `TrayNotif*`; elegir entre notificación de bandeja real (H.NotifyIcon la soporta) o renombrado honesto | `MainWindow.xaml.cs` |
| 7 | *(Opcional)* `crash.log` a `%AppData%\OfiConvert\` en vez de junto al `.exe` | `Program.cs`, `App.xaml.cs` |

---

## 🚀 Tier B — Pipeline de release *(portar de los hermanos)*

Hoy el corte es artesanal: bump de versión en dos archivos, compilar el `.iss` a mano y subir el
instalador al release. Portar los scripts con sus lecciones **ya pagadas** (BOM UTF-8 en los `.ps1`,
lectura del `.csproj` conservando el BOM, `Invoke-Git` para el stderr de git, publish a `%TEMP%` por
MAX_PATH).

| # | Ítem | Fuente |
|---|------|--------|
| 1 | `installer/build-installer.ps1`: publish a `%TEMP%` (MAX_PATH: los nombres del Windows App SDK llegan a 76 caracteres y aquí el publish es self-contained), **versión leída del `.csproj`** (adiós a la doble fuente con el `.iss`), genera el **`.sha256`**, firma opcional | FormatDiskPro (self-contained, como aquí) |
| 2 | `release.ps1`: validar → tests → bump (`Version` + `AssemblyVersion` + `FileVersion`, conservando BOM) → instalador → commit + tag `vX.Y.Z` → push → `gh release create` con `.exe` **y** `.sha256` | WingetUSoft (bump triple) |
| 3 | Guardas: `-DryRun`, `-AllowDirty`, abortar si falta el `.sha256`; documentar que solo hace `git add -u` (los archivos nuevos se añaden antes) | ambos |

> **Primer corte con el pipeline:** publicar los 3 commits que `main` acumula sobre `v2.0.0`.

---

## 🔐 Tier C — Actualización confiable *(requiere Tier B)*

`GitHubUpdateService` descarga el instalador y **lo ejecuta sin comprobar nada** — el mismo agujero
que ambos hermanos cerraron y marcaron como NO ROMPER.

| # | Ítem | Fuente |
|---|------|--------|
| 1 | Verificar antes de ejecutar: firma Authenticode válida → OK; si no, **SHA-256** contra el asset `*.exe.sha256` del release; sin ninguna de las dos, **borrar y abortar** | `GitHubUpdateService` de WingetUSoft |
| 2 | Mantener la descarga en método propio, con el `FileStream` cerrado **antes** de verificar — el bug del auto-bloqueo que WingetUSoft pagó (auto-actualización rota en 1.4.1–1.5.0) | ídem |
| 3 | Tests del verificador con servidor HTTP local sobre `TcpListener` (no `HttpListener`: exige reservar la URL como admin) | `GitHubUpdateServiceTests` de WingetUSoft |

> **Consecuencia operativa:** desde que esto se publique, **todo release debe subir su `.sha256`**
> (o ir firmado), o los clientes rechazarán la actualización. `release.ps1` (Tier B) lo garantiza.
>
> **Alcance honesto** (documentarlo así): el `.exe` y su hash salen del mismo release → detecta
> corrupción y manipulación **en tránsito**, no un compromiso de la cuenta de GitHub.

---

## 🧪 Tier D — Pruebas

| # | Ítem | Notas |
|---|------|-------|
| 1 | Extraer **`Core/`** (lógica pura: sin UI, sin `Process`, sin `HttpClient`, sin COM): rutas de salida seguras, formateo de bytes, mapeo de formatos, sanitización CSV, comparación de versiones, validación por magic bytes | La regla de oro de los hermanos |
| 2 | `tests/OfiConvert.Tests` (**xUnit** — el estándar de la casa; no MSTest/NUnit/TUnit) | |
| 3 | Test de **completitud de localización**: cada clave presente en los 8 `Lang/*.xaml`, y cada clave usada en el código existente en los diccionarios — el indexer devuelve la clave si falta, así que un typo hoy no rompe nada (la trampa `L.T` de los hermanos) | |
| 4 | `tests/OfiConvert.UiTests` (FlaUI/UIA3) sobre la app real: **sin elevación** (la app es `asInvoker`). Los tests que exijan **Office o LibreOffice** instalado se **OMITEN** si faltan — patrón `[TestDriveFact]` de FormatDiskPro: *omitido = "no tengo el entorno"; fallido = "la app está rota"* | Modelo WingetUSoft (sin `EnsureElevated`) |

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
