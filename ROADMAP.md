# OfiConvert — Hoja de ruta

> ## Estado (2026-08-29)
>
> **Los tiers 0 y A–I están todos completados. El Tier J acaba de abrirse.** El proyecto ya tiene la
> infraestructura que sus hermanos habían pagado (pipeline de release, actualización verificada, cara
> pública y documentación viva) y, encima, dos pases completos de UI hechos **mirando la app**.
>
> **Tier J — Re-auditoría externa 🔶 ABIERTO** (2026-08-29): la primera revisión que mira **el motor**
> en vez de la interfaz. **38 tareas, 7 de severidad Alta.** Lo peor no salió de leer más código, sino
> de dos comprobaciones que nadie había hecho: **activar Office dos veces por COM** (PowerPoint devuelve
> la MISMA instancia: las conversiones paralelas se matan entre sí y pueden cerrar la sesión del
> usuario) y **mirar los primeros bytes de los `.ps1`**. Además, dos regresiones conceptuales: el
> instalador vuelve a bloquear el modo silencioso —ahora en equipos sin Office— y `release.ps1` prueba
> el binario **Debug** mientras empaqueta el **Release**.
>
> **Tier G — UI/UX ✅** (v2.4.0): tres bugs reales, los comandos se apagan solos y la app deja de ser muda
> para un lector de pantalla.
>
> **Tier H — Instalador end-to-end ✅** (v2.5.0): probarlo de verdad destapó que **`/VERYSILENT` no era
> silencioso** — bloqueaba con un diálogo modal. Era el último hueco sin cubrir. **226 pruebas.**
>
> **Tier I — Pase de UX/UI sobre capturas ✅** (v2.6.0): fotografiar **todos** los estados en claro y
> oscuro destapó **tres bugs que el XAML no delataba**. **230 pruebas.**
>
> **v2.6.1** (2026-08-29): el arreglo de los desplegables borrosos sobre Mica. No queda nada en `main`
> sin publicar.

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
| **D** | Pruebas: extraer `Core/`, cobertura real, UI tests FlaUI | ✅ Completado (2026-07-14) | **2.3.0** ✔ publicada |
| **E** | Cara pública: README de usuario, capturas reproducibles, legal in-app | ✅ Completado (2026-07-14) | 2.4.0 *(sin publicar)* |
| **F** | Infraestructura agéntica (`.claude`, skills, codegraph) | ✅ Completado (2026-07-14) | — |
| **G** | UI/UX: 3 bugs reales, comandos que se apagan solos, accesibilidad | ✅ Completado (2026-07-14) | 2.4.0 |
| **H** | Instalador end-to-end: el `/VERYSILENT` que no era silencioso | ✅ Completado (2026-07-14) | **2.5.0** ✔ publicada |
| **I** | Pase de UX/UI sobre capturas: 3 bugs vistos solo mirando la app | ✅ Completado (2026-07-21) | **2.6.0** ✔ publicada |
| **J** | **Re-auditoría externa: el motor, el pipeline y los guardianes** | 🔶 **Abierto (2026-08-29)** — **16/38 cerradas**, las **7 Altas** completas | — |

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

## ✅ Tier E — Cara pública *(completado 2026-07-14)*

| # | Ítem | Dónde |
|---|------|-------|
| 1 | ✅ **README de usuario**: badges, capturas, *Instalación* desde Releases, modelo de confianza de las actualizaciones y sección legal honesta (**no todo es MIT**) | `README.md` |
| 2 | ✅ **`THIRD-PARTY-NOTICES.txt`** + licencia y avisos **embebidos en el `.exe`**, con diálogo in-app en *Configuración → Acerca de* (8 idiomas) | `THIRD-PARTY-NOTICES.txt`, `Core/LegalText.cs`, `MainWindow.xaml(.cs)` |
| 3 | ✅ **`tools/capture-screenshots.ps1`** + `docs/screenshots/`: 4 capturas **regeneradas** conduciendo la app real por UI Automation | `tools/`, `docs/screenshots/` |
| 4 | ✅ **9 pruebas nuevas** (6 unitarias de `LegalText` + 3 de UI que abren los diálogos legales de verdad) | `tests/` |

> ### ⚖️ Lo que encontró el "verificar cada `.nuspec`, no de memoria"
>
> **El `THIRD-PARTY-NOTICES.txt` de WingetUSoft está MAL**, y copiarlo habría propagado el error: declara
> el **Windows App SDK como MIT**. El repositorio de GitHub sí es MIT, pero **los binarios que el
> instalador redistribuye vienen del paquete NuGet**, que se publica bajo los *Microsoft Software License
> Terms* (un EULA propietario). Verificado leyendo el `license.txt` del paquete.
>
> Y no es la única: **Serilog es Apache-2.0**, no MIT — y la Apache 2.0 (cláusula 4.a) **exige entregar
> una copia de la licencia**, así que su texto íntegro viaja dentro del `.exe`. **WebView2 es BSD-3-Clause**
> (llega como dependencia del App SDK; nadie lo había mirado).
>
> `LegalTextTests` fija estas tres: si alguien "simplifica" el archivo a un "todo es MIT", el build cae.

---

## ✅ Tier F — Infraestructura agéntica *(validado y completado 2026-07-14)*

Estaba **a medias sin que el ROADMAP lo supiera**: las skills y el índice de codegraph ya existían; lo
que faltaba era todo lo que las hace *utilizables*.

| # | Ítem | Estado |
|---|------|--------|
| 1 | `.agents/skills/` + `.claude/skills/` + `skills-lock.json` — 9 skills C#/.NET de `github/awesome-copilot` | ✅ **Ya estaba** (y bien: sin la skill `accessibility` de WingetUSoft, que es de web) |
| 2 | Índice de codegraph (`.codegraph/codegraph.db`) | ✅ **Ya estaba**, y correctamente **auto-ignorado**: solo se commitea su `.gitignore`, la base de 2 MB no entra en el repo |
| 3 | **`.mcp.json`** (servidor codegraph) | ✅ **Añadido** — el índice existía pero **no había nada que lo sirviera**: 2 MB de grafo que ninguna herramienta podía consultar |
| 4 | **`.claude/CLAUDE.md`** | ✅ **Añadido** — leer `CONTEXT.md` al empezar, los 6 invariantes que no se rompen, y cómo se compila/prueba/publica aquí |
| 5 | **`.claude/settings.json`** | ✅ **Añadido** (permisos de las herramientas `codegraph_*`) |

> **Ojo con lo que el plan daba por hecho:** decía que el `CLAUDE.md` de WingetUSoft manda *«leer
> `CONTEXT.md` al iniciar sesión y mantenerlo»*. **No lo dice**: su `CLAUDE.md` es solo el bloque que
> genera codegraph. Aquí esa parte se ha escrito de verdad, en vez de copiarla de donde no existía.

---

## ✅ Tier G — UI/UX *(completado 2026-07-14)*

Nace de revisar la interfaz sobre **capturas de la app real**, no leyendo el XAML. Tres de los hallazgos
resultaron ser **bugs**, no preferencias estéticas — y llevaban en producción desde el principio.

### 1. Los tres bugs

| # | Ítem | Dónde |
|---|------|-------|
| 1 | ✅ **El contador de reintentos estaba INVERTIDO.** `CountToVisibilityConverter` **ignoraba su `ConverterParameter`** y el XAML le pasaba `Invert` creyendo que lo respetaba: el contador se veía cuando valía **0** (`↻ 0` en todas las filas) y **se escondía justo cuando un archivo había reintentado** | `Core/VisibilityRules.cs`, `Converters/CountToVisibilityConverter.cs` |
| 2 | ✅ **La carpeta de destino prometía algo que no existía.** El placeholder decía *«Misma ubicación que archivos originales»* y esa función **no estaba implementada**: al convertir sin carpeta, la app interrumpía con un diálogo y, si decías que no, **cancelaba el lote entero**. **Ahora la promesa se cumple**: cada documento se convierte junto al original y la app funciona **sin configurar nada** | `MainViewModel.GetDestinationFolder`, `Core/OutputPath.GetSafeFolder` |
| 3 | ✅ **«Limpiar historial» borraba hasta 1000 registros sin preguntar** — la única acción irreversible de la app, y la única sin confirmación | `MainViewModel.ClearHistoryAsync` |

### 2. La causa raíz: la app dejaba hacer lo imposible y luego regañaba

✅ **Ninguno de los 15 `[RelayCommand]` tenía `CanExecute`.** «Convertir» estaba habilitado con la cola
vacía, «Limpiar» sin nada que limpiar, «Exportar CSV» con el historial vacío (generaba un CSV con solo la
cabecera). La app lo compensaba con diálogos que reñían al usuario.

Ahora **los botones se apagan solos** — y el arreglo **quita** código: desaparecen tres diálogos y **cinco
claves de localización × 8 idiomas**.

### 3. Accesibilidad: la app era muda para un lector de pantalla

✅ `AutomationProperties` no aparecía **ni una vez** en todo el XAML. Nombre accesible + tooltip para los
botones de solo icono… y **para los tres `ToggleSwitch`**, que UI Automation expone **como botones sin
nombre** (su etiqueta es un `TextBlock` aparte que el lector no asocia): anunciaban *«botón, activado»* sin
decir de qué. **Esos tres los encontró el propio test**, no la revisión visual.

### 4. Jerarquía visual

| # | Ítem |
|---|------|
| ✅ | **Un solo botón de acento**: «Convertir». «Archivo» pasa a neutro |
| ✅ | La **barra de progreso** solo existe mientras se convierte (antes: una barra vacía y un «0%» ocupando sitio para no decir nada) |
| ✅ | **Estado vacío del historial** con icono + título + subtítulo, igual que el de la pestaña de conversión |
| ✅ | **Ajustes agrupado** en tres cabeceras (Apariencia · Conversión · Integración) |

### 5. Lo que se descubrió de camino *(y no estaba en el plan)*

- 🐞 **Los UI tests conducían un `.exe` VIEJO.** `OfiConvert.UiTests` no referenciaba la app (a propósito,
  para no cargar WinUI en el proceso de test), y por eso `dotnet test` **no la recompilaba**: las pruebas
  pasaban en verde contra un binario que ya no existía. *Un test que aprueba código que no se va a publicar
  es peor que no tener test.* Resuelto con `ProjectReference ReferenceOutputAssembly="false"` (dependencia
  de compilación, sin referencia al ensamblado).
- 🐞 **Los UI tests dependían de los datos reales del usuario.** «El botón se apaga si no hay archivos»
  habría fallado en la máquina de quien tuviera una cola pendiente — sin que la app tuviera ningún fallo.
  `SettingsBackup` ahora **siembra un estado conocido** (cola e historial vacíos, español) además de
  respaldar y restaurar.

---

## ✅ Tier H — El instalador, probado de punta a punta *(completado 2026-07-14)*

Era **el único hueco que ninguna prueba cubría**, y estaba señalado desde el Tier 0: *«el instalador nunca
se ha probado end-to-end; FormatDiskPro encontró ahí un fallo con un diálogo modal»*. Pues eso mismo, casi
palabra por palabra.

### 🐞 `/VERYSILENT` no era silencioso

Con `PrivilegesRequiredOverridesAllowed=dialog`, Inno Setup planta el cuadro **«Seleccione el modo de
instalación»** (solo para mí / para todos los usuarios) **aunque se le pase `/VERYSILENT`**, y se queda ahí
**bloqueado esperando un clic**.

En la instalación limpia de prueba tardó **76 segundos en vez de 9**: los que tardó el humano en verlo y
pulsar. En una instalación desatendida colgaría para siempre. Y en la **auto-actualización** es peor: la app
**ya se ha cerrado**, así que el usuario ve su programa esfumarse y aparecer un diálogo que no ha pedido.
*(No saltaba en la actualización porque Inno recuerda el modo de la instalación anterior — de ahí que
llevara cuatro versiones escondido.)*

| # | Ítem | Dónde |
|---|------|-------|
| 1 | ✅ `PrivilegesRequiredOverridesAllowed=**commandline** dialog` — sin `commandline`, Inno **rechaza** `/ALLUSERS` y `/CURRENTUSER` | `installer/OfiConvert.iss` |
| 2 | ✅ El updater manda el modo **que el usuario ya eligió** (`/ALLUSERS` si está bajo `Program Files`, `/CURRENTUSER` si no): una actualización no puede mover la app de sitio por sorpresa | `Core/InstallScope.cs` |

### 🐞 La app se cerraba aunque el usuario rechazara el UAC

Instalada *para todos los usuarios*, el instalador **pide UAC**. La app lanzaba el instalador, esperaba 1,5 s
y hacía `Application.Current.Exit()` **sin mirar nada**: si el usuario decía que no, el programa **desaparecía
igual**, seguía en la versión vieja y no recibía explicación alguna. Ahora se detecta `ERROR_CANCELLED` (y un
instalador que muere con error) y la app **sigue viva**, avisando de lo ocurrido.

### 🐞 Y el mismo bug de localización, por CUARTA vez

Todo el flujo de actualización estaba **en español a fuego** (*«Descargando… 42%»*, *«Instalar ahora»*,
*«Comprobando…»*), igual que los diálogos de `DialogService` (*«Sí»*, *«No»*, *«Aceptar»*, *«Error»*). Y otra
clave inexistente tapada por un *fallback defensivo* (`MsgCheckingUpdate`).

**Lo grave es por qué no se cazó:** `LocalizationUsageTests` buscaba `LocalizationService.Instance["…"]` y
`GetLocalizedString("…")`, pero **no `loc["…"]`** — la forma que usa medio `MainWindow`. *Un escáner que no
mira donde de verdad se usa el código no prueba nada.*

| # | Ítem |
|---|------|
| ✅ | **`HardcodedUiTextTests`**: ningún literal puede asignarse a `Title`/`Message`/`Content`/`…ButtonText` en el código de UI. Es la prueba que faltaba para que esto no vuelva una quinta vez |
| ✅ | `LocalizationUsageTests` amplía su escáner a `loc["…"]` |
| ✅ | 15 claves nuevas × 8 idiomas (136 por archivo) y **fuera todos los fallbacks defensivos** |

---

## ✅ Tier I — Pase de UX/UI sobre capturas *(completado 2026-07-21)*

El Tier G revisó la interfaz sobre capturas; este la revisó sobre **todas** las capturas. Primero la
instrumentación, después el pulido: **`tools/capture-ui-states.ps1`** fotografía los siete estados (vacío,
con cola, historial poblado, historial vacío, ajustes arriba y abajo, diálogo legal) en **claro y oscuro**
— 14 imágenes por corrida, sembrando cada estado por JSON. Con la galería delante aparecieron **tres bugs
que leer el XAML no delataba**.

| # | Ítem | Dónde |
|---|------|-------|
| 1 | ✅ **El historial no distinguía un fallo de un éxito.** El `FontIcon` de estado tenía el glifo (tilde) y el color (verde) **en duro**, sin mirar `Success`: una conversión fallida se veía **idéntica** a una correcta y **sin decir el motivo**. Ahora glifo y color salen de `Success`, y las filas fallidas muestran su `ErrorMessage` | `Core/HistoryStatus.cs`, `Converters/BoolToStatus*` |
| 2 | ✅ **Los `ContentDialog` ignoraban el tema de la app.** Un diálogo se enraíza en la capa de popups, hermana de `Content`, así que **no hereda** el `RequestedTheme` del root: en modo Claro con Windows en Oscuro, el diálogo legal salía negro. Se les pasa el tema a mano, a los cuatro | `MainWindow.xaml.cs` (`RootTheme`) |
| 3 | ✅ **El panel de resultados encabezaba los errores con un tilde verde.** Mismo patrón que el 1, en otro sitio. Ahora el icono se enlaza a `HasConversionErrors`: con errores, **aviso ámbar** (no rojo: parte del lote sí se convirtió) | `MainWindow.xaml`, `Converters/ErrorsToResult*` |

> **✨ Pulido**, verificado en los dos temas: diálogo legal ensanchado (el MIT a 80 columnas ya no parte
> palabras sueltas); historial con **duración con unidad** (`UnitSeconds` ×8 idiomas) y columnas
> equilibradas; fila de acciones reorganizada (origen a la izquierda, acciones a la derecha); **botones
> destructivos en *outline*** en vez de relleno sólido, que chocaba con los acentos cálidos del sistema; y
> menos monotonía de tarjetas. **+4 pruebas** (`HistoryStatusTests`) → **230**.
>
> **Higiene de las capturas:** la app respeta el acento de Windows, así que el repo enseñaba las capturas
> en el **rojo personal** del equipo del autor. Los scripts fijan ahora un acento neutro
> (`OFICONVERT_ACCENT`) y `docs/screenshots/` se regeneró en azul.

### 🐞 Los desplegables borrosos sobre Mica *(2026-08-24 — publicado en **v2.6.1**)*

WinUI pinta los popups con acrílico; sobre el backdrop **Mica** de la ventana eso apila dos capas
translúcidas y el texto del menú pierde contraste. Arreglado con `ThemeDictionaries` en **`App.xaml`**
(no en `MainWindow.xaml`): cubre los cuatro `ComboBox` y cualquier flyout futuro.

> **El primer intento no hizo NADA:** las `ThemeDictionaries` en la raíz de `Application.Resources`
> compilan 0/0, no avisan y **no se resuelven** — tienen que ir dentro de `MergedDictionaries`, después
> de `XamlControlsResources`. Lo destapó **mirar la app**, no leer más XAML. De ahí
> **`tools/capture-dropdown.ps1`**: abre los cuatro `ComboBox` por UI Automation en claro y oscuro y
> **mide** el ruido del fondo en vez de dejarlo a ojo (roto a propósito: 38–68% de ruido; arreglado: 0%).

---

## 🔶 Tier J — Re-auditoría externa: lo que ningún tier anterior miró *(abierto 2026-08-29)*

> **De dónde sale:** re-auditoría completa del repositorio (12 de las 13 áreas del prompt de revisión;
> SEO no aplica), con el plan **cerrado** y los tiers 0 y A–I dados por buenos. Profundidad exhaustiva,
> sin exclusiones: los 133 archivos versionados.
>
> **Qué la distingue de los tiers G e I:** aquellos revisaron **la interfaz**, sobre capturas. Este ha
> leído **el motor** —COM, LibreOffice, el pipeline y los guardianes de las pruebas— y ha hecho dos
> comprobaciones que nadie había hecho: **activar Office dos veces por COM** y **mirar los primeros
> bytes de los `.ps1`**. De ahí salen los hallazgos que más duelen.
>
> **Los tiers 0–I son de severidad; este es una tanda temática.** Que un hallazgo esté en el Tier J
> **no baja su prioridad**: cada tarea lleva la suya, y las siete primeras son **Altas**.
>
> **Patrón que se repite y conviene nombrar:** *el guardián cubre el sitio donde dolió, no el riesgo.*
> `HardcodedUiTextTests` vigila dos archivos; `AccessibilityTests` vigila un `ControlType`;
> `LocalizationUsageTests` vigila tres formas de pedir una clave y ya hay una cuarta. Los tres pasan en
> verde sobre problemas de su propia especialidad.

**Índice del tier:** 38 tareas — **7 Altas · 19 Medias · 12 Bajas**. **Cerradas: 16** (TJ-01 a TJ-07, TJ-10, TJ-11, TJ-12, TJ-13, TJ-17, TJ-19, TJ-20, TJ-21 y TJ-25) — **las 7 Altas, completas**.
Esfuerzo agregado: **~19 bajo · ~16 medio · ~3 alto**.

### J.1 — Severidad ALTA

- [x] ✅ **[TJ-01] PowerPoint es una instancia COM ÚNICA y compartida** · **Alto** *(cerrado 2026-08-31)*
  - **Área:** Rendimiento / Código
  - **Ubicación:** `Services/OfficeFileConversionService.cs:463-503`, `:509-527`, `:550-570`
  - **Qué hacer:** `Type.GetTypeFromProgID("PowerPoint.Application")` + `Activator.CreateInstance`
    **no crea un proceso nuevo**: devuelve el PowerPoint que ya está corriendo. *Verificado en esta
    máquina (Office 16 ClickToRun): dos activaciones seguidas dejan **1** `POWERPNT.EXE`; Word y Excel
    sí crean dos.* Consecuencias: (a) con `MaxParallelConversions > 1`, N conversiones de `.pptx`
    conducen **el mismo** PowerPoint y la primera que termina llama a `Quit()` **matando las demás**;
    (b) si el usuario tiene PowerPoint abierto, la app se engancha **a su sesión** y la cierra — con
    `DisplayAlerts = ppAlertsNone (2)` puesto, así que **sin preguntar por lo no guardado**.
    Arreglo: serializar las conversiones de PowerPoint con un semáforo propio de 1, y **detectar si la
    instancia era preexistente** (comprobar `POWERPNT.EXE` antes de activar) para **no llamar a
    `Quit()`** en ese caso — cerrar solo la presentación.
  - **Criterio de aceptación:** con PowerPoint abierto y un documento sin guardar, convertir un lote de
    3 `.pptx` con paralelismo 4: los 3 se convierten, PowerPoint **sigue abierto** y no pierde nada.
  - **Esfuerzo:** alto · **Depende de:** ninguna
  - **Hecho:** `Services/SerialGate` serializa las conversiones de PowerPoint (Word y Excel siguen en
    paralelo: ahí cada activación sí crea proceso), y `PowerPointSession` decide **de quién es** la
    instancia mirando `POWERPNT.EXE` **antes** de activar: solo cierra la que ha abierto ella, y a la
    prestada le devuelve `DisplayAlerts` y `AutomationSecurity` como estaban. Ante cualquier duda se
    asume que es del usuario.
  - **Lo que enseñó escribir la prueba:** soltar la instancia prestada con `FinalReleaseComObject`
    **cerraba igualmente las presentaciones del usuario** —el RCW de una aplicación COM es compartido en
    el proceso, así que «Final» suelta las referencias de todos y PowerPoint se queda sin clientes de
    automatización—. El proceso seguía vivo y el trabajo se perdía igual. Se suelta **una** referencia
    con `ReleaseComObject`. Sin la prueba contra el Office real, este fallo se publica.
  - **Verificado:** `PowerPointSharedInstanceTests` (omitidas salvo `OFICONVERT_OFFICE_TESTS=1`) — la
    premisa medida aquí (2 activaciones → **1** `POWERPNT.EXE`; Word → 2) y el criterio entero: con
    PowerPoint abierto y una presentación sin guardar, las 3 conversiones salen, PowerPoint sigue abierto
    y su presentación intacta. Con el código antiguo, esa prueba **falla**.
  - ⚠️ **Lo que NO se reprodujo:** el «la primera en terminar mata a las demás». Ni con tres
    presentaciones de 40 diapositivas en paralelo y sin puerta: `Quit()` sobre una instancia con otros
    clientes de automatización enganchados no la termina. La serialización se mantiene porque conducir
    en paralelo una instancia que Windows no puede duplicar es incorrecto de todas formas —y
    `SerialGateTests` cubre que la puerta hace su trabajo—, pero el escenario concreto queda **sin
    reproducir**, no confirmado.

- [x] ✅ **[TJ-02] LibreOffice podía quedarse colgado para siempre (deadlock de las tuberías)** · **Alto** *(cerrado 2026-08-31)*
  - **Área:** Rendimiento / Código
  - **Ubicación:** `Services/LibreOfficeConversionService.cs:56-63`, `:82`
  - **Qué hacer:** se redirigen `StandardOutput` **y** `StandardError` y **no se lee ninguno** hasta
    después de `WaitForExitAsync`. Cuando el búfer de la tubería (~4 KB) se llena, `soffice` **se
    bloquea escribiendo** y `WaitForExitAsync` no vuelve nunca: la conversión se congela ocupando una
    plaza del semáforo. `stdout` no se lee **jamás**. Arreglo: lectura asíncrona de ambos flujos
    **antes** de esperar, o dejar de redirigir el que no se usa.
  - **Criterio de aceptación:** un documento que haga a LibreOffice escribir >8 KB de avisos termina
    con resultado, no colgado. Regresión con un proceso simulado que escupa 64 KB por stdout.
  - **Esfuerzo:** medio · **Depende de:** ninguna
  - **Hecho:** la ejecución del proceso se extrajo a `Services/ProcessRunner`, que arranca la lectura de
    **los dos** flujos antes de `WaitForExitAsync`. `ProcessRunnerTests` reproduce el cuelgue con 64 KB
    (por stdout, por stderr y por los dos a la vez) sin necesitar LibreOffice: con el orden antiguo las
    tres pruebas se cuelgan y el plazo de 30 s las pone en rojo — comprobado.

- [x] ✅ **[TJ-03] La ruta de LibreOffice destruía el resultado de una conversión anterior** · **Alto** *(cerrado 2026-08-31)*
  - **Área:** Seguridad / Rendimiento
  - **Ubicación:** `Services/LibreOfficeConversionService.cs:66-76`
  - **Qué hacer:** `--outdir` recibe la carpeta destino y LibreOffice escribe **con el nombre del
    original** (`informe.pdf`), ignorando el `outputPath` sin colisiones que calculó
    `Core/OutputPath.GetSafe` (`informe (1).pdf`). Si `informe.pdf` ya existía, LibreOffice **lo pisa**
    y acto seguido `File.Move` se lo lleva a `informe (1).pdf`: el archivo anterior **desaparece**.
    Rompe la garantía nº 2 de `OutputPath` y la promesa del README («**Sin sobrescrituras**»). Arreglo:
    convertir a una **carpeta temporal exclusiva** y mover desde allí al `outputPath` calculado.
  - **Criterio de aceptación:** con `informe.pdf` ya presente, convertir `informe.docx` por LibreOffice
    deja **los dos** archivos intactos. Test sobre la lógica de destino, extraída a `Core/`.
  - **Esfuerzo:** medio · **Depende de:** ninguna
  - **Hecho:** `Core/LibreOfficeOutput` — carpeta de trabajo **exclusiva** por conversión (dos documentos
    homónimos en paralelo tampoco se pisan), elección del archivo producido y movimiento al destino
    **recomprobando** que sigue libre en el último momento: el `GetSafe` original se calculó antes de
    convertir, y entre medias pasan segundos, el resto del lote o el propio usuario. Además, terminar en
    0 sin producir nada ya no se da por bueno: antes se apuntaba en el historial un archivo inexistente.
    11 pruebas nuevas, una de ellas la regresión literal del criterio de aceptación.

- [x] ✅ **[TJ-04] El instalador bloqueaba el modo silencioso en equipos sin Office** · **Alto** *(cerrado 2026-08-31)*
  - **Área:** DevOps
  - **Ubicación:** `installer/OfiConvert.iss:111-121`; `MainWindow.xaml.cs:344`
  - **Qué hacer:** `InitializeWizard` planta un `MsgBox` cuando no detecta Word. Inno **no suprime los
    MsgBox en modo silencioso** salvo `/SUPPRESSMSGBOXES`, y el updater lanza
    `/VERYSILENT /NORESTART {scope} /autoinstall=1` **sin** ese modificador. Es **exactamente** el
    fallo del Tier H (`/VERYSILENT` que no era silencioso) en un segundo sitio: la app ya se ha
    cerrado y el usuario se queda con un diálogo que no ha pedido, o con la actualización colgada.
    Y le ocurre justo al usuario que la app dice soportar: el que tiene **solo LibreOffice**. Arreglo:
    envolver el aviso en `if not WizardSilent() then`, y añadir `/SUPPRESSMSGBOXES` en el updater como
    cinturón y tirantes.
  - **Criterio de aceptación:** en una máquina sin Office, `Setup.exe /VERYSILENT /CURRENTUSER
    /autoinstall=1` termina **sin intervención humana** y en tiempo comparable a una con Office.
  - **Esfuerzo:** bajo · **Depende de:** ninguna
  - **Regresión conceptual de:** Tier H (el `/VERYSILENT` que no era silencioso)
  - **Hecho:** el aviso va dentro de `if (not WizardSilent) and (not IsOfficeInstalled)`, y el updater
    manda además `/SUPPRESSMSGBOXES` (cinturón y tirantes: el instalador también se lanza a mano). La
    línea de comandos se construye ahora en `Core.InstallScope.SilentInstallArguments`, donde se puede
    probar, en vez de a mano en el code-behind — que es justo cómo se perdió el modificador.
    `InstallerScriptTests` vigila el `.iss` como código: ningún `MsgBox` sin guarda, `commandline` en
    `PrivilegesRequiredOverridesAllowed` y ningún `/VERYSILENT` escrito a mano fuera de `Core/`.
    El `.iss` se recompiló con ISCC (compila limpio). ⚠️ **Falta la comprobación de punta a punta en una
    máquina SIN Office**, que es la única que puede firmar el criterio de aceptación entero.

- [x] ✅ **[TJ-05] `release.ps1` validaba un binario que NO era el que publica** · **Alto** *(cerrado 2026-08-31)*
  - **Área:** QA / DevOps
  - **Ubicación:** `release.ps1:175`; `tests/OfiConvert.UiTests/AppFixture.cs:71-75`
  - **Qué hacer:** la línea 160 compila **Release**; la 175 corre `dotnet test $proj` **sin `-c
    Release`**, así que MSBuild reconstruye la app en **Debug** por el `ProjectReference`, y
    `AppFixture.ResolveExePath()` —que coge *el `.exe` de `bin\**\win-x64\` más reciente*— acaba
    conduciendo **el binario Debug**. El instalador empaqueta un publish Release. Es la misma familia
    que el bug del Tier G («los UI tests conducían un `.exe` VIEJO»): allí se garantizó que el binario
    fuera **fresco**, no que fuera **el que se publica**. Arreglo: `dotnet test -c Release` y que
    `AppFixture` **exija** configuración explícita (o `OFICONVERT_EXE`) en vez de adivinar por fecha.
  - **Criterio de aceptación:** `AppFixture` registra qué `.exe` conduce y falla si no es el de la
    configuración pedida. Un `bin\Debug` más reciente ya no cambia lo que se prueba.
  - **Esfuerzo:** bajo · **Depende de:** ninguna
  - **Regresión conceptual de:** Tier G (`ReferenceOutputAssembly="false"`)
  - **Hecho:** `release.ps1` corre `dotnet test -c Release`, y `AppFixture` dejó de elegir por fecha:
    deduce la configuración de su propia ruta (`bin\{Config}\`, forzable con `OFICONVERT_CONFIGURATION`),
    busca **solo** dentro de `bin\{Config}\` —excluyendo `publish\`— y **registra qué `.exe` conduce**.
    `DrivenBinaryTests` falla si el binario no es el de la configuración compilada. Comprobado en rojo
    reponiendo la búsqueda por fecha con un `bin\Debug` más nuevo: el test caza exactamente ese caso.

- [x] ✅ **[TJ-06] QUINTA reincidencia del texto en español a fuego** · **Alto** *(cerrado 2026-08-31)*
  - **Área:** Localización / Ortografía
  - **Ubicación:** `Services/FileValidationService.cs:19,23,31,42,49,52,55,60`;
    `Services/LibreOfficeConversionService.cs:33,36,46,84,88,94`;
    `Services/OfficeFileConversionService.cs:59,64,103`; `ViewModels/MainViewModel.cs:525,543,602`
  - **Qué hacer:** **18 mensajes** que el usuario lee —en el panel de resultados, en la columna
    *Error* del historial (que el Tier I hizo visible) y en el CSV/TXT exportado— están escritos en
    español dentro del código. Y **las claves ya están traducidas a los 8 idiomas, sin usarse**:
    `MsgFileNotFound` = «El archivo no existe.» es **idéntica** al literal de
    `FileValidationService.cs:19`; igual `MsgFileLocked`, `MsgPasswordProtected`, `MsgCorruptFile` y
    `MsgOfficeNotFound`. Es **letra por letra** el bug nº 2 del Tier D (el diálogo de cierre pedía
    claves inexistentes teniendo las traducciones escritas con otro nombre). Arreglo: devolver
    **claves** desde `Services/` y traducir en el borde de la UI, y **borrar** los literales.
  - **Criterio de aceptación:** con la app en japonés, un archivo protegido con contraseña y un fallo
    de LibreOffice se anuncian en japonés en el panel, en el historial y en el TXT exportado.
  - **Esfuerzo:** medio · **Depende de:** TJ-17 (para que no vuelva a colarse)
  - **Hecho:** `Core/UserMessage` (clave + argumentos) viaja desde los servicios; `ConversionResult.Error`
    y `FileValidationResult.Error` dejan de ser `string`; la traducción ocurre en **un solo borde**,
    `LocalizationService.Translate`, y el historial guarda ya el texto traducido (es lo que se exporta).
    13 claves nuevas × 8 idiomas. `UserMessageTranslationTests` fija el criterio: con la app en japonés,
    los mensajes de servicio llegan en japonés y no como clave cruda.

- [x] ✅ **[TJ-07] `CHANGELOG.md` y notas de release de verdad** · **Alto** *(cerrado 2026-08-31)*
  - **Área:** Documentación / DevOps
  - **Ubicación:** raíz del repo; `release.ps1:187-203`
  - **Qué hacer:** el índice de versiones vive dentro de `CONTEXT.md`, que así hace de changelog y de
    contexto a la vez. Sin `-NotesFile`, `release.ps1` **genera una plantilla genérica** («Instalador
    self-contained para Windows x64…») idéntica para toda versión: las notas de un release no cuentan
    qué cambió. Arreglo: `CHANGELOG.md` con *Keep a Changelog* (**creado en esta re-auditoría**, a
    partir de los 9 tags y del índice de `CONTEXT.md`), y que `release.ps1` **extraiga de él** la
    sección `## [X.Y.Z]` y **aborte si no está**.
  - **Criterio de aceptación:** `.\release.ps1 -Version 9.9.9 -DryRun` aborta con «falta la sección
    9.9.9 en CHANGELOG.md»; con la sección puesta, las notas del release son las suyas.
  - **Esfuerzo:** medio · **Depende de:** ninguna
  - **Hecho:** `CHANGELOG.md` (2026-08-29) + `Get-ChangelogSection` en `release.ps1`, que extrae la
    sección y **aborta antes de compilar nada** si falta — el chequeo se puso DELANTE del build para no
    descubrirlo cinco minutos después. Las notas del GitHub Release son esa sección más un pie fijo con
    la instalación y el `.sha256`; `-NotesFile` sigue mandando. Dos pruebas nuevas
    (`tests/OfiConvert.Tests/ChangelogTests.cs`) llevan el mismo contrato a `dotnet test`: la versión del
    `.csproj` tiene que estar contada, y ninguna versión publicada puede quedarse sin fecha.

### J.2 — Severidad MEDIA

- [ ] **[TJ-08] El corte no distingue «omitido» de «correcto»** · Medio
  - **Área:** QA · **Ubicación:** `release.ps1:171-178`
  - **Qué hacer:** solo se mira `$LASTEXITCODE`. Hoy mismo el corte sale en verde con **199 pasan · 1
    omitido** (`PublishedReleaseTests`, gated por `OFICONVERT_NETWORK_TESTS`) sin decirlo. Leer el
    `.trx` (`--logger trx`) e informar de los tres números, avisando si algún omitido no está en la
    lista de omisiones esperadas.
  - **Criterio de aceptación:** la salida del corte imprime «pasan / omitidos / fallan» por proyecto.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[TJ-09] Cuatro `ComboBox` y dos `NumberBox` son mudos para un lector de pantalla** · Medio
  - **Área:** Accesibilidad · **Ubicación:** `MainWindow.xaml:134,692,713,744,767,803`;
    `tests/OfiConvert.UiTests/AccessibilityTests.cs:58`
  - **Qué hacer:** su etiqueta es un `TextBlock` hermano, que UI Automation **no asocia** — la misma
    trampa que `CONTEXT.md` documenta para el `ToggleSwitch` y que allí sí se arregló. El Narrador
    anuncia «cuadro combinado, Sistema», «cuadro combinado, Español», «cuadro combinado, PDF» y
    «cuadro de número, 2», sin decir qué controla cada uno. `AccessibilityTests` no lo ve porque
    **filtra por `ControlType.Button`**: cazó los `ToggleSwitch` solo porque UIA los expone como
    botones. Poner `AutomationProperties.Name` (o `LabeledBy`) y **ampliar el test a ComboBox, Spinner
    y Edit**.
  - **Criterio de aceptación:** el test recorre las tres pestañas y falla si cualquier control
    interactivo visible de esos tipos se queda sin nombre.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] ✅ **[TJ-10] «Archivos guardados en: » sin nada detrás, en el flujo por defecto** · Medio *(cerrado 2026-08-31)*
  - **Área:** UI/UX · **Ubicación:** `ViewModels/MainViewModel.cs:652,663`
  - **Qué hacer:** el resumen formatea `MsgFilesSavedTo` con `OutputFolder`, que está **vacío** cuando
    el usuario no eligió carpeta — que desde el Tier G es el camino recomendado («sin configurar
    nada»). El panel de resultados termina la frase en dos puntos y nada. Decir «junto a cada archivo
    original» (clave nueva ×8) cuando `UseCustomOutputFolder` es falso.
  - **Criterio de aceptación:** convertir sin carpeta elegida produce una frase completa; con carpeta,
    la ruta. Añadir el estado a la galería de `capture-ui-states.ps1`.
  - **✅ Cerrado 2026-08-31.** Un único `DondeSeGuardo()` decide la frase para las dos ramas del
    resumen (con errores y sin ellos), que antes la formaban por separado con el mismo fallo. Con carpeta
    elegida, la ruta; sin ella, `MsgFilesSavedNextToOriginal` («Cada archivo se ha guardado junto a su
    original.», ×8 idiomas) — porque sin carpeta común **no hay una ruta que enseñar**: cada archivo va
    al lado del suyo. Falta añadir el estado a la galería de `capture-ui-states.ps1`.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] ✅ **[TJ-11] Dos archivos del mismo nombre pueden pisarse en paralelo** · Medio *(cerrado 2026-08-31)*
  - **Área:** Seguridad · **Ubicación:** `ViewModels/MainViewModel.cs:470,605-623`; `Core/OutputPath.cs:25`
  - **Qué hacer:** `GetOutputPath` se calcula **antes** de convertir y `GetSafe` decide por
    `File.Exists`. Con carpeta de destino común y dos orígenes distintos que se llaman igual
    (`C:\a\informe.docx` y `C:\b\informe.docx`), ambos resuelven a `informe.pdf` porque ninguno se ha
    escrito todavía: el segundo pisa al primero. Reservar el nombre al calcularlo (crear el archivo
    vacío, o llevar en memoria las rutas ya asignadas del lote).
  - **Criterio de aceptación:** un lote con dos orígenes homónimos y paralelismo 2 produce dos salidas.
  - **✅ Cerrado 2026-08-31.** El nombre se **reserva al calcularlo**, no al escribirlo:
    `Core/OutputReservations` lleva lo ya repartido en el lote y `OutputPath.GetSafe` acepta ahora un
    predicado de «ocupado» que **suma** a `File.Exists`. Alcance = el lote, a propósito: uno por
    conversión no arreglaría nada y uno global mandaría a `informe (1).pdf` al reconvertir el mismo
    documento. Cubre también las **carpetas** de PPT→imágenes, donde dos presentaciones homónimas
    mezclaban sus diapositivas. **Verificado en rojo:** de **32 reservas simultáneas solo 1** era
    distinta.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [x] ✅ **[TJ-12] El instalador contradice al producto sobre LibreOffice** · Medio *(cerrado 2026-08-31)*
  - **Área:** Redacción / DevOps · **Ubicación:** `installer/OfiConvert.iss:115-119`
  - **Qué hacer:** el aviso dice que la app «**no funcionará** hasta que instale Microsoft Office»,
    cuando LibreOffice es un motor soportado y así lo dicen el README y `CONTEXT.md`. Reescribirlo
    nombrando las dos opciones. Se resuelve junto con TJ-04, que toca el mismo bloque.
  - **Criterio de aceptación:** el texto menciona LibreOffice como alternativa válida.
  - **✅ Cerrado 2026-08-31.** El aviso mira ahora **los dos** motores (`IsLibreOfficeInstalled`:
    App Paths de `soffice.exe` en HKLM 64/32 bits y, si no, las rutas por defecto en `{commonpf}` y
    `{commonpf32}`) y su texto vive en la sección de mensajes personalizados, en los **seis** idiomas del
    instalador — antes salía en español en todos.
    **Verificado sobre el instalador compilado**, con los dos detectores forzados por línea de comandos:
    `0/0 → AVISA`, `0/1 → CALLA`, `1/0 → CALLA`, `1/1 → CALLA`. La fila `0/1` (solo LibreOffice) es
    justamente la que antes mentía. Los seis textos se volcaron a archivo desde el instalador y se
    comprobaron uno a uno, con sus acentos.
    Guardianes nuevos en `InstallerScriptTests`, ambos comprobados en rojo:
    `ElAviso_DeSinMotor_MiraTambienLibreOffice` y `ElTextoDelAviso_EstaEnLosSeisIdiomas`.
  - **Esfuerzo:** bajo · **Depende de:** TJ-04

- [x] ✅ **[TJ-13] Dos archivos demasiado grandes = dos diálogos a la vez** · Medio *(cerrado 2026-08-31)*
  - **Área:** UI/UX · **Ubicación:** `Services/DialogService.cs:65-73`; `ViewModels/MainViewModel.cs:217-222`
  - **Qué hacer:** `ShowInformation` es `async void` (dispara y olvida) y `AddFiles` lo llama **dentro
    del bucle**. Soltar dos documentos de más de 500 MB abre el segundo `ContentDialog` con el primero
    aún en pantalla: WinUI solo admite uno y el segundo lanza — sobre un `async void`, así que la
    excepción sale sin dueño y la traga `App.UnhandledException` **sin que el usuario vea nada**.
    Acumular los rechazados y avisar **una vez**, al terminar el bucle.
  - **Criterio de aceptación:** soltar 3 archivos de 600 MB muestra **un** aviso que los nombra.
  - **✅ Cerrado 2026-08-31.** `AddFiles` acumula los rechazados y avisa **una vez** al terminar el
    bucle, con `Core/TooBigReport` (mensaje de uno / de varios, clave `MsgFilesTooBig` nueva ×8). Se
    conserva el texto de un solo archivo: la forma plural con una única línea se lee como un error de la
    app. Guardián nuevo y **general**: `DialogsInLoopsTests` prohíbe abrir cualquier diálogo dentro de un
    bucle en todo el código — es la clase de fallo, no este caso. **Verificado en rojo**, y localiza la
    línea exacta.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[TJ-14] Las miniaturas dependen de una carrera que se pierde en los dos sentidos** · Medio
  - **Área:** Código / UI · **Ubicación:** `Services/ThumbnailService.cs:64-83`
  - **Qué hacer:** se guarda un PNG temporal, se asigna `BitmapImage.UriSource` —que carga de forma
    **asíncrona**— y en el `finally` inmediato se **borra el archivo**. O el borrado falla (y quedan
    `oficonvert_thumb_*.png` acumulándose en `%TEMP%` para siempre) o gana y la imagen no carga.
    Además el `BitmapImage` se construye en un `ContinueWith(..., TaskScheduler.Default)`, es decir
    **fuera del hilo de UI**, y el `catch { return null; }` se traga el fallo. Usar `SetSourceAsync`
    sobre un stream en memoria, en el hilo de UI, y no tocar el disco.
  - **Criterio de aceptación:** las miniaturas se ven en la lista y `%TEMP%` no acumula PNG tras
    encolar 50 archivos. *(Pendiente de verificación: no se ha comprobado si hoy se ven.)*
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [ ] **[TJ-15] Instalar una actualización a mitad de un lote salta el cierre protegido** · Medio
  - **Área:** Arquitectura · **Ubicación:** `MainWindow.xaml.cs:355`; `MainWindow.xaml:69-71`
  - **Qué hacer:** `btnInstalarUpdate` **no** está atado a `IsConverting`, y el flujo termina en
    `Application.Current.Exit()`, que **no pasa por `OnAppWindowClosing`**: se salta la confirmación y
    la cancelación que existen precisamente para no dejar procesos de Office huérfanos —*EL* riesgo
    declarado de esta app. Deshabilitar el botón mientras se convierte y cancelar el lote antes de salir.
  - **Criterio de aceptación:** con una conversión en curso, el botón de instalar está apagado.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[TJ-16] La ventana no tiene tamaño mínimo y se dimensiona en píxeles crudos** · Medio
  - **Área:** Diseño responsivo · **Ubicación:** `MainWindow.xaml.cs:49`
  - **Qué hacer:** `_appWindow.Resize(new SizeInt32(1050, 800))` usa **píxeles físicos** sin escalar
    por DPI (a 150 % la ventana nace un tercio más pequeña de lo pensado) y no se fija
    `OverlappedPresenter.PreferredMinimumWidth/Height`: se puede encoger hasta romper el layout, con
    `ComboBox` de ancho fijo (110/140/160 px) y etiquetas alemanas dentro.
  - **Criterio de aceptación:** la ventana no baja de un mínimo usable y abre del mismo tamaño aparente
    a 100 %, 150 % y 200 %.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [x] ✅ **[TJ-17] `HardcodedUiTextTests` solo miraba dos archivos de veintitantos** · Medio *(cerrado 2026-08-31)*
  - **Área:** QA · **Ubicación:** `tests/OfiConvert.Tests/HardcodedUiTextTests.cs:28-32`
  - **Qué hacer:** su lista es `MainWindow.xaml.cs` y `DialogService.cs`. Todo el texto de TJ-06 vive
    fuera de ella y, además, el patrón solo casa **asignaciones a propiedades** (`Title = "…"`), así
    que no vería `_dialogService.ShowError("Error general:…")` ni aunque el archivo estuviera en la
    lista. Ampliar a `Services/`, `ViewModels/`, `Models/` y `Core/`, y añadir un patrón para los
    literales que viajan como **argumento** hacia `ShowError`/`ShowInformation`/
    `ConversionResult.Failed`/`FileValidationResult`.
  - **Criterio de aceptación:** el test se pone **en rojo** al reintroducir cualquiera de los 18
    literales de TJ-06 (comprobarlo reintroduciendo uno a propósito).
  - **Esfuerzo:** medio · **Depende de:** ninguna
  - **Hecho:** los archivos ya no se listan, se **descubren** (todos los `.cs` de la app, menos los textos
    legales y el propio `LocalizationService`), y hay un segundo patrón para los literales que viajan
    **como argumento** hacia `ShowError`/`ShowInformation`/`Failed`/`UserMessage`/`FileValidationResult`:
    ahí solo se admite una **clave**, y una frase con espacios delata al culpable. Comprobado en rojo
    reintroduciendo dos de los 18 literales de TJ-06. La prueba también falla si deja de encontrar
    archivos: un escáner que no mira nada pasa en verde.

- [ ] **[TJ-18] El escáner de claves va otra vez por detrás del código** · Medio
  - **Área:** QA · **Ubicación:** `tests/OfiConvert.Tests/LocalizationUsageTests.cs:28-30`
  - **Qué hacer:** cubre `GetLocalizedString("…")`, `LocalizationService.Instance["…"]` y `loc["…"]`.
    **Falta `T("…")`**, el envoltorio que `DialogService:63` estrenó *en el mismo arreglo* que añadió
    la tercera forma: seis claves (`MsgInformation`, `MsgError`, `MsgConfirmation`, `BtnYes`, `BtnNo`,
    `BtnOk`) no las mira nadie. Hoy existen todas — es suerte, no cobertura. Añadir la cuarta forma y,
    mejor, detectar envoltorios genéricamente. Añadir además el chequeo **inverso**: claves declaradas
    y no usadas (hay 39; ver TJ-29).
  - **Criterio de aceptación:** borrar `MsgError` de `es-ES.xaml` pone el test en rojo.
  - **Esfuerzo:** bajo · **Depende de:** ninguna
  - **Avance (2026-08-31):** el escáner ya cubre **siete** formas — se le añadieron `T("…")` (la que
    pedía esta tarea) y las dos que nacieron con TJ-06, `new UserMessage("…")` y `Failed("…")`, en el
    mismo cambio que las creó. Queda **solo el chequeo inverso** (claves declaradas y no usadas), que
    depende de limpiar las 39 de TJ-29.

- [x] ✅ **[TJ-19] `IProgress<ConversionProgress>` atraviesa toda la API y nadie lo reporta** · Medio *(cerrado 2026-08-31)*
  - **Área:** Arquitectura · **Ubicación:** `Services/IFileConversionService.cs:10,17`;
    `OfficeFileConversionService.cs:44,55`; `LibreOfficeConversionService.cs:19,28`;
    `ViewModels/MainViewModel.cs:565-568`
  - **Qué hacer:** ningún motor llama a `progress.Report(...)` (el único `Report(` del proyecto está en
    el updater). El `Progress<ConversionProgress>` del ViewModel, con su mensaje «Convirtiendo 3/7»,
    **no se ejecuta jamás** y el modelo `ConversionProgress` está muerto. Decidir: reportar progreso de
    verdad (PPT→imágenes sabe cuántas diapositivas hay) o **quitar** el parámetro de las dos interfaces
    y el modelo entero.
  - **Criterio de aceptación:** o el mensaje aparece durante una conversión real, o no queda ni un
    `IProgress<ConversionProgress>` en el árbol.
  - **✅ Cerrado 2026-08-31 — QUITÁNDOLO, y por una razón, no por pereza.** No hay progreso que
    reportar: Word y Excel convierten con **una** llamada COM sin devolución de llamada, LibreOffice es un
    proceso externo que no informa de nada, y solo PPT→imágenes conoce el número de diapositivas — aun así
    exporta de una vez. Reportar en 1 de 6 caminos daría una barra que se mueve para un formato y se queda
    quieta para los demás: peor que no tenerla. Fuera el parámetro de las dos firmas, de las dos
    implementaciones, el `Progress<>` muerto del ViewModel y el modelo `ConversionProgress` entero. El
    porqué queda escrito en `IFileConversionService`. Guardián: `DeadProgressTests` — quien declare un
    `IProgress<>` tiene que reportarlo. **Verificado en rojo.**
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [x] ✅ **[TJ-20] Un fallo al configurar Office deja el proceso huérfano** · Medio *(cerrado 2026-08-31)*
  - **Área:** Código · **Ubicación:** `Services/OfficeFileConversionService.cs:509-527`
  - **Qué hacer:** `CreateOfficeApp` crea la instancia y **luego** llama a `configure(app)` fuera de
    todo `try/finally`. Si esa configuración lanza (un `InvokeMember` que la versión de Office no
    admite), el método propaga la excepción **sin haber devuelto el objeto**: el `finally` del llamante
    recibe `null`, no llama a `Quit()`, y queda un `WINWORD.EXE`/`EXCEL.EXE` vivo por cada intento —
    el fallo que `CONTEXT.md` señala como *EL* riesgo de la app.
  - **Criterio de aceptación:** con `configure` forzado a lanzar, no queda ningún proceso de Office.
  - **✅ Cerrado 2026-08-31.** `CreateOfficeApp` envuelve la configuración: si algo falla entre «ya existe
    el proceso» y «el llamante tiene la referencia», lo cierra antes de propagar — y propaga la excepción
    **original**, que también se comprueba. **Verificado en rojo con Office real**
    (`OfficeAppLifetimeTests`, con puerta `OFICONVERT_OFFICE_TESTS=1`): sin el arreglo queda **1
    `WINWORD.EXE` por intento**. Se prueba con Word y no con PowerPoint a propósito: Word sí arranca
    proceso propio, así que contar procesos dice la verdad.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [x] ✅ **[TJ-21] `HidePowerPointWindows` hace lo contrario de lo que dice** · Medio *(cerrado 2026-08-31)*
  - **Área:** Código · **Ubicación:** `Services/OfficeFileConversionService.cs:329-331`
  - **Qué hacer:** el bucle pone `window.Visible = -1`, que es **msoTrue** — *muestra* la ventana. Hoy
    no se nota porque las presentaciones se abren con `WithWindow:=False` y la colección viene vacía:
    es código muerto que, el día que deje de estarlo, hará justo lo contrario de su nombre. Y con
    `Visible = -1` también en la **aplicación** (`:229`, `:466`), PowerPoint se hace visible durante el
    lote. Poner `0` (msoFalse) y comprobar qué se ve realmente al convertir un `.pptx`.
  - **Criterio de aceptación:** convertir 3 `.pptx` no muestra ninguna ventana de PowerPoint.
  - **✅ Cerrado 2026-08-31, y la causa NO era la que decía esta ficha.** Medido en esta máquina
    (Office 16 ClickToRun): recién activado por COM, PowerPoint está en `Visible = msoFalse` y **sin
    ventana principal** (`MainWindowHandle = 0`), y abrir con `WithWindow:=False` lo deja igual —
    **headless de fábrica**. Lo que sacaba la ventana a pantalla era **nuestro** `Visible = msoTrue`.
    El comentario que lo justificaba decía media verdad: `Visible = msoFalse` sí lanza
    («*Hiding the application window is not allowed*»), pero de ahí no se sigue que haya que ponerlo a
    **true**. Ahora **no se toca**.
    `HidePowerPointWindows` se **borra** en vez de corregirse a `msoFalse`: con `WithWindow:=False`,
    `Windows.Count` es **0** —medido—, así que no había ventanas que ocultar; dejar una función que no se
    ejecuta es dejar una trampa cargada.
    Guardián: `Convertir_NoAbreNingunaVentanaDePowerPoint`, que vigila **durante** la conversión y no al
    final. **Verificado en rojo**: con el `Visible = msoTrue` anterior, **16 muestras** con la ventana en
    pantalla.
  - **Esfuerzo:** bajo · **Depende de:** TJ-01

- [ ] **[TJ-22] El menú contextual del Explorador está siempre en español** · Medio
  - **Área:** Localización · **Ubicación:** `Services/ShellIntegrationService.cs:11`
  - **Qué hacer:** `MenuText = "Convertir con OfiConvert"` se escribe en el registro tal cual, en los
    ocho idiomas. Es la única superficie de la app **fuera** de su ventana. Escribir el texto del
    idioma activo y **reescribirlo** al cambiar de idioma (o usar `MUIVerb` con un recurso indirecto).
  - **Criterio de aceptación:** con la app en alemán, el menú contextual está en alemán.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [ ] **[TJ-23] `System.Drawing.Common` se redistribuye y no está atribuido** · Medio
  - **Área:** Legal · **Ubicación:** `THIRD-PARTY-NOTICES.txt`; `tests/OfiConvert.Tests/Core/LegalTextTests.cs:40-48`
  - **Qué hacer:** `System.Drawing.Common 9.0.0` llega como dependencia de `H.NotifyIcon.WinUI`, se usa
    en `ThumbnailService` y en el icono de bandeja, y **viaja como DLL en el instalador** (verificado:
    `bin\Release\...\win-x64\System.Drawing.Common.dll`). No aparece en los avisos. Es MIT, así que el
    arreglo es una entrada más — pero es **exactamente** lo que `LegalTextTests` advierte de sí mismo:
    *«si mañana entra una dependencia nueva y nadie toca el archivo de avisos, esto no lo caza»*.
    Añadirlo y, ya puestos, un test que **compare los ensamblados publicados** con los componentes
    nombrados.
  - **Criterio de aceptación:** el test falla si un `.dll` de un paquete NuGet publicado no está citado.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [ ] **[TJ-24] La contraseña del certificado viaja como `[string]`** · Medio
  - **Área:** Seguridad · **Ubicación:** `release.ps1:56`; `installer/build-installer.ps1:43,85`
  - **Qué hacer:** `-CertPassword` es `[string]`, así que se teclea en la línea de comandos (y queda en
    `ConsoleHost_history.txt`), se reenvía entre scripts en claro y acaba como `/p <contraseña>` en la
    **línea de comandos de `signtool`**, legible por cualquier proceso del equipo mientras dura.
    Pasarla como `SecureString` o leerla de una variable de entorno. Hoy no se firma, pero este es el
    camino documentado para el día que se firme.
  - **Criterio de aceptación:** ninguna contraseña aparece en `Get-CimInstance Win32_Process` durante
    una firma.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [x] ✅ **[TJ-25] Varios `soffice --headless` a la vez comparten perfil de usuario** · Medio *(cerrado 2026-08-31)*
  - **Área:** Rendimiento · **Ubicación:** `Services/LibreOfficeConversionService.cs:53`
  - **Qué hacer:** LibreOffice **no admite** instancias headless concurrentes sobre el mismo perfil: la
    segunda se enchufa a la primera o falla. Con `MaxParallelConversions` hasta 8, un lote por
    LibreOffice puede degradarse o romperse. Dar a cada proceso su perfil con
    `-env:UserInstallation=file:///…`. *(Pendiente de verificación: no hay LibreOffice en esta máquina.)*
  - **Criterio de aceptación:** un lote de 8 documentos con paralelismo 4 por LibreOffice los convierte
    todos.
  - **🟡 Cerrado 2026-08-31 — CON UNA VERIFICACIÓN A MEDIAS, dicho claro.** Cada conversión crea su
    propio perfil (`Core/LibreOfficeCommand`) y lo pasa con `-env:UserInstallation=file:///…`, **delante**
    de `--convert-to`: LibreOffice decide si arranca motor propio o se enchufa a otro *antes* de mirar qué
    convertir. El perfil se borra en el `finally`, como la carpeta de trabajo.
    **Lo que SÍ está probado** (9 pruebas, sin LibreOffice): la forma exacta del argumento. Es lo
    delicado, porque pasarle una ruta de Windows en vez de una URL **no da error** — LibreOffice la
    ignora y vuelve al perfil compartido, así que el fallo seguiría ahí en silencio y un test que solo
    mirase «que aparezca `-env:`» pasaría igual. Verificado en rojo por los dos lados: barras sin
    normalizar (3 pruebas caen) y `-env:` detrás de `--convert-to` (1 prueba cae).
    **⚠️ Lo que NO está probado:** el criterio de aceptación tal cual. En esta máquina **no hay
    LibreOffice instalado**, así que el lote de 8 con paralelismo 4 no se ha ejecutado nunca. Queda
    pendiente para una máquina que lo tenga.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [ ] **[TJ-26] Una función a medio construir: opciones que existen en todas partes menos en la UI** · Medio
  - **Área:** Refactorización / Arquitectura
  - **Ubicación:** `Models/ConversionOptions.cs:13-26`; `Lang/*.xaml` (`LblPageRange`, `LblSheetNames`,
    `LblSlideRange`, `LblImageQuality`, `LblImageDpi`, `TipPageRange`, `TipSheetNames`, `TipSlideRange`)
  - **Qué hacer:** `PageRange`, `SheetNames`, `SlideRange`, `ImageQuality` e `ImageDpi` están en el
    modelo, tienen **etiqueta y tooltip traducidos a los 8 idiomas**, y **ninguna aparece en
    `MainWindow.xaml`**. `SheetNames` hasta tiene código de motor que lo usa
    (`OfficeFileConversionService.cs:423-438`) y siempre llega vacío; `ImageDpi` decide el tamaño de
    las imágenes de PowerPoint con un 150 fijo. Decidir: exponerlas, o retirar modelo, claves y código.
  - **Criterio de aceptación:** o hay UI para ellas, o no quedan ni las claves ni las propiedades.
  - **Esfuerzo:** alto · **Depende de:** ninguna

### J.3 — Severidad BAJA

- [ ] **[TJ-27] `tools/capture-dropdown.ps1` es el único `.ps1` SIN BOM** · Bajo
  - **Área:** DevOps · **Ubicación:** `tools/capture-dropdown.ps1:1`
  - **Qué hacer:** empieza por `3C 23 0D`; los otros cuatro `.ps1` del repo empiezan por `EF BB BF`.
    `CONTEXT.md` §4 lo declara invariante. Verificado en PowerShell **5.1** con página de códigos
    **Windows-1252**: el archivo *parsea* (sus acentos caen en comentarios o en cadenas que no generan
    comillas), pero sus mensajes salen corruptos —
    `Die "No se encontrÃ³ OfiConvert.exe…"`— y basta meter un `—` dentro de una cadena para reproducir
    el «Falta el paréntesis de cierre» que ya pagaron los proyectos hermanos. Reguardar con BOM.
  - **Criterio de aceptación:** los cinco `.ps1` empiezan por `EF BB BF`, y `release.ps1` lo comprueba.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[TJ-28] Código muerto en cinco sitios** · Bajo
  - **Área:** Refactorización
  - **Ubicación:** `Core/OutputFormats.cs:37-48` (`GetDisplayName`: solo la usa un test, y devuelve
    «PNG (Imágenes)» en español); `Models/ConversionOptions.cs:38-46` (`Clone`, sin usar);
    `Models/ConversionResult.cs:9-10` (`WasRetried`/`RetryCount`: se escriben y no se leen);
    `ViewModels/MainViewModel.cs:835-838` (`ApplyTheme`, método **vacío** con parámetro sin usar);
    `Services/ThumbnailService.cs:86-94` (`CreateStreamOnHGlobal` y una interfaz `IStream` cuya firma
    no corresponde a la real — peligrosa si algún día alguien la usa).
  - **Criterio de aceptación:** los cinco eliminados y el build sigue en 0/0.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[TJ-29] 39 claves de idioma declaradas y sin usar (×8 archivos)** · Bajo
  - **Área:** Refactorización · **Ubicación:** `Lang/*.xaml`
  - **Qué hacer:** entre ellas, cinco `Tray*` de la notificación modal que el Tier A retiró
    (`TrayNotifSuccess`, `TrayNotifErrors`, `TrayShow`, `TrayExit`, `TrayStartConversion`), las nueve
    de TJ-26 y las cinco de TJ-06 —que **hay que usar, no borrar**. Revisarlas una a una.
    **Cuidado:** en este proyecto una clave huérfana no ha sido basura ninguna de las dos veces que ha
    aparecido: ha sido la señal de una función a medio conectar.
  - **Criterio de aceptación:** el chequeo inverso de TJ-18 pasa en verde sin lista de excepciones.
  - **Esfuerzo:** medio · **Depende de:** TJ-06, TJ-18, TJ-26

- [ ] **[TJ-30] Cuatro afirmaciones del README que ya no son ciertas** · Bajo
  - **Área:** Documentación · **Ubicación:** `README.md:60,79,81,195`
  - **Qué hacer:** (a) «*todos los controles tienen nombre para lectores de pantalla*» — falso, ver
    TJ-09; (b) «*Sin sobrescrituras*» — falso por la ruta de LibreOffice, ver TJ-03; (c) «puedes elegir
    instalar para *todos los equipos*» → *todos los **usuarios***; (d) el árbol dice que `tools/`
    contiene `capture-screenshots.ps1` y ya son **tres** scripts.
  - **Criterio de aceptación:** las cuatro corregidas y coherentes con el estado real tras TJ-03/TJ-09.
  - **Esfuerzo:** bajo · **Depende de:** TJ-03, TJ-09

- [ ] **[TJ-31] El log se para al llegar a 10 MB y no rota** · Bajo
  - **Área:** Código / Observabilidad · **Ubicación:** `Services/LoggingService.cs:21-26`
  - **Qué hacer:** hay `fileSizeLimitBytes: 10 MB` pero **no** `rollOnFileSizeLimit: true`: alcanzado
    el límite, Serilog **deja de escribir** el resto del día en vez de abrir otro archivo. Un lote
    grande con errores puede perder justo el registro que se iba a consultar.
  - **Criterio de aceptación:** superado el límite aparece `oficonvert-YYYYMMDD_001.log`.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[TJ-32] Los fallos irrecuperables se tragan en silencio** · Bajo
  - **Área:** Código · **Ubicación:** `App.xaml.cs:20-24`; `Helpers/AppPaths.cs:22-33`
  - **Qué hacer:** `UnhandledException` pone `e.Handled = true` **siempre**: la app sigue viva en un
    estado indefinido y el usuario no ve nada — solo queda un `crash.log` que además se
    **sobrescribe**, así que sobrevive únicamente el último fallo. Avisar en la UI y **anexar** al log
    en vez de reemplazarlo.
  - **Criterio de aceptación:** un fallo no controlado deja rastro visible y no borra el anterior.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[TJ-33] El historial exportado está en español pase lo que pase** · Bajo
  - **Área:** Localización · **Ubicación:** `Services/ConversionHistoryService.cs:69,87-107`;
    `ViewModels/MainViewModel.cs:750,765`
  - **Qué hacer:** la cabecera del CSV (`Fecha,Archivo,Salida,…`), el informe TXT entero («Historial de
    Conversiones», «✓ Éxito», «Total: … exitosas … fallidas») y los nombres de archivo sugeridos
    (`historial_conversiones.csv/.txt`) están en español para los ocho idiomas.
  - **Criterio de aceptación:** exportar con la app en inglés produce cabeceras en inglés.
  - **Esfuerzo:** bajo · **Depende de:** TJ-06

- [ ] **[TJ-34] La verificación Authenticode no comprueba revocación** · Bajo
  - **Área:** Seguridad · **Ubicación:** `Services/GitHubUpdateService.cs:258`
  - **Qué hacer:** `fdwRevocationChecks = WTD_REVOKE_NONE`. Hoy es inocuo (no se firma nada y siempre
    se cae al SHA-256), pero el día que haya certificado, una firma con el certificado **revocado**
    pasaría por buena y **cortocircuitaría** la comprobación del hash — `VerifyInstallerAsync` acepta
    y vuelve sin mirar nada más. Usar `WTD_REVOKE_WHOLECHAIN` con `WTD_CACHE_ONLY_URL_RETRIEVAL` para
    no depender de la red.
  - **Criterio de aceptación:** cambiado y documentado antes del primer release firmado.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[TJ-35] El instalador habla 6 de los 8 idiomas de la app** · Bajo
  - **Área:** Localización / DevOps · **Ubicación:** `installer/OfiConvert.iss:65-71,85`
  - **Qué hacer:** faltan japonés (Inno 6 trae `Japanese.isl`: una línea) y chino (`.isl` no oficial).
    Además el icono de desinstalación dice «Desinstalar» en duro: usar `{cm:UninstallProgram,…}`.
  - **Criterio de aceptación:** el instalador ofrece japonés y el atajo usa el mensaje común.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[TJ-36] Hasta 300 MB de logs pueden acabar en el perfil móvil** · Bajo
  - **Área:** Código · **Ubicación:** `Helpers/AppPaths.cs:9-12`
  - **Qué hacer:** todo cuelga de `SpecialFolder.ApplicationData` (**Roaming**), incluidos hasta 30
    archivos de log de 10 MB y un historial de 1000 entradas. En un perfil de dominio con
    sincronización, eso viaja por la red en cada inicio de sesión. Mover `logs\` (y probablemente
    `queue.json`) a `LocalApplicationData`, o justificar la decisión en `CONTEXT.md`.
  - **Criterio de aceptación:** decidido y escrito; si se mueve, migrar lo que ya exista.
  - **Esfuerzo:** medio · **Depende de:** ninguna

- [ ] **[TJ-37] El único lector del `.csproj` que no sigue la regla de la casa** · Bajo
  - **Área:** DevOps · **Ubicación:** `installer/build-installer.ps1:110`
  - **Qué hacer:** `[xml](Get-Content $csproj)` es justo la forma que `CONTEXT.md` §4 y el propio
    `release.ps1` prohíben. Hoy es inocuo —el `.csproj` tiene BOM y este script **no reescribe**—, pero
    es la línea que habría que cambiar el día que escriba, y contradecir un invariante allí donde está
    escrito es como se pierden los invariantes. Usar `[System.IO.File]::ReadAllText`.
  - **Criterio de aceptación:** ningún script lee el `.csproj` con `Get-Content`.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

- [ ] **[TJ-38] Rastro de los documentos convertidos tras desinstalar** · Bajo
  - **Área:** Legal / Privacidad · **Ubicación:** `installer/OfiConvert.iss` (sin `[UninstallDelete]`);
    `README.md:113-120`
  - **Qué hacer:** `history.json` y `logs\` guardan la **ruta completa** de cada documento convertido y
    sobreviven a la desinstalación sin que se avise ni se ofrezca borrarlos. Conservar los datos del
    usuario por defecto es correcto; **no decírselo**, no. Documentarlo en *Datos y privacidad* y
    ofrecer una casilla de «eliminar también mis datos» al desinstalar.
  - **Criterio de aceptación:** el README lo dice y el desinstalador lo ofrece.
  - **Esfuerzo:** bajo · **Depende de:** ninguna

---

## Progreso

| Fecha | Qué se cerró |
|---|---|
| 2026-07-13 | Tier 0 (docs vivos), Tier A (higiene), Tier B (pipeline), Tier C (updater verificado) |
| 2026-07-14 | Tier D (pruebas), Tier E (cara pública), Tier F (agéntica), Tier G (UI/UX), Tier H (instalador) |
| 2026-07-21 | Tier I (pase de UX/UI sobre capturas) — v2.6.0 |
| 2026-08-29 | v2.6.1 publicada (desplegables opacos). **Tier J abierto**: 38 tareas, 0 cerradas |
| 2026-08-31 | **TJ-07**: el `CHANGELOG.md` es la fuente de las notas del release y el corte aborta sin ella (1/38) |
| 2026-08-31 | **TJ-05** (los UI tests conducían el binario Debug) y **TJ-04** (el aviso del instalador salía en modo silencioso) (3/38) |
| 2026-08-31 | **TJ-03** (LibreOffice borraba un archivo anterior) y **TJ-02** (deadlock de las tuberías) (5/38, **5 de 7 Altas**) |
| 2026-08-31 | **TJ-01**: PowerPoint serializado y la sesión del usuario intocable, verificado contra el Office real (6/38, **6 de 7 Altas**) |
| 2026-08-31 | **TJ-06** (18 mensajes en español a fuego → claves traducidas) y **TJ-17** (el guardián miraba 2 archivos de 20) — **las 7 Altas cerradas** (8/38) |
| 2026-08-31 | **TJ-11** (dos archivos homónimos se pisaban en paralelo), **TJ-13** (dos avisos a la vez = ninguno), **TJ-10** (la frase del resumen se cortaba en el flujo por defecto) y **TJ-19** (progreso muerto: se quita) (16/38) |
| 2026-08-31 | **TJ-21** (PowerPoint ya no saca su ventana: la sacábamos nosotros), **TJ-20** (un fallo al configurar dejaba un proceso huérfano por intento) y **TJ-25** (perfil propio por proceso de LibreOffice, *verificación de punta a punta pendiente*) (12/38) |
| 2026-08-31 | **TJ-12**: el instalador deja de decirle a quien usa LibreOffice que la app no funcionará, y el aviso se traduce a los 6 idiomas (9/38). Afinado del propio Tier J: las pruebas con Office dejan de ser inestables, y `HardcodedUiTextTests` caza un vigésimo literal que su `\b` no veía |

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
