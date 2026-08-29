# OfiConvert — Hoja de ruta

> ## Estado (2026-08-29)
>
> **El plan está cerrado: los tiers 0 y A–I están todos completados.** El proyecto ya tiene la
> infraestructura que sus hermanos habían pagado (pipeline de release, actualización verificada, cara
> pública y documentación viva) y, encima, dos pases completos de UI hechos **mirando la app**.
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
