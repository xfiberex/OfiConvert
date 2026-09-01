# Registro de cambios

Todos los cambios relevantes de OfiConvert, contados **para quien usa el programa**: qué le pasaba antes
y qué le pasa ahora. Los detalles de implementación van bajo *Interno*.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el versionado,
[Versionado Semántico](https://semver.org/lang/es/).

> **Reparto con los otros dos documentos vivos —no se solapan:**
>
> | Archivo | Responde a |
> |---|---|
> | **`CHANGELOG.md`** (este) | **Qué cambió** en cada versión |
> | [`CONTEXT.md`](CONTEXT.md) | **Por qué** se decidió así, y qué se aprendió |
> | [`ROADMAP.md`](ROADMAP.md) | **Qué falta** por hacer |
>
> Ante la duda: el *qué* va aquí; el *por qué*, a `CONTEXT.md`.

> ⚠️ **Las versiones 1.0.0 a 2.6.1 son una RECONSTRUCCIÓN APROXIMADA.** Este archivo nació el
> 2026-08-29, durante la re-auditoría que abrió el [Tier J](ROADMAP.md), a partir de los nueve tags de
> git y del *Índice de versiones* y el *Registro de cambios* de [`CONTEXT.md`](CONTEXT.md) — que hasta
> hoy hacía también de changelog. Las fechas son las de los tags. Cada entrada enlaza al registro
> correspondiente, que es la fuente. **De la 2.7.0 en adelante, este archivo se escribe antes del
> corte, no después.**

## [Sin publicar]

### Corregido

- **Convertir presentaciones ya no te planta PowerPoint delante.** Durante todo el lote se abría la
  ventana de PowerPoint encima de lo que estuvieras haciendo. No hacía falta: la aplicación **pedía** esa
  ventana sin necesitarla. Ahora convierte sin que aparezca nada. *(Cierra [TJ-21](ROADMAP.md).)*
- **Ya no quedan procesos de Office colgados cuando algo falla al arrancarlos.** Si la preparación de
  Word o Excel fallaba —pasa con versiones que no admiten alguna de las opciones que se les piden—, el
  proceso se quedaba vivo e invisible, **uno por cada archivo del lote**, comiendo memoria hasta reiniciar
  el equipo. *(Cierra [TJ-20](ROADMAP.md).)*
- **Convertir varios documentos a la vez con LibreOffice ya no se estorba a sí mismo.** LibreOffice no
  admite dos conversiones simultáneas que compartan su configuración de usuario: la segunda se enganchaba
  a la primera o fallaba con un error que no parecía de conversión. Ahora cada una corre por su cuenta.
  *(Cierra [TJ-25](ROADMAP.md); pendiente de una prueba de punta a punta en un equipo con LibreOffice.)*
- **El instalador ya no te dice que la app no funcionará si usas LibreOffice.** Cuando no encontraba
  Microsoft Office avisaba de que OfiConvert «no funcionará hasta que instale Microsoft Office» — y eso
  es falso: LibreOffice sirve igual, y el propio programa lo anuncia. Ahora comprueba **los dos**
  motores y solo avisa si no hay ninguno; además el aviso está traducido a los **seis** idiomas del
  instalador, en vez de salir siempre en español. *(Cierra [TJ-12](ROADMAP.md).)*
- **Los mensajes de error ya salen en tu idioma.** Dieciocho avisos —«el archivo está protegido con
  contraseña», «el archivo está bloqueado por otro proceso», los fallos de LibreOffice y de Office—
  aparecían **en español en los ocho idiomas**: en el panel de resultados, en la columna *Error* del
  historial y en el CSV/TXT exportado. Varios de ellos ya estaban traducidos desde hace versiones, sin
  que nadie los usara. *(Cierra [TJ-06](ROADMAP.md).)*
- **Convertir presentaciones ya no cierra el PowerPoint que tengas abierto.** Si estabas trabajando en
  una presentación **sin guardar**, convertir cualquier `.ppt`/`.pptx` se enganchaba a esa misma sesión
  de PowerPoint y al terminar **la cerraba, sin preguntar**: se perdía lo que no estuviera guardado.
  Ahora la app solo cierra el PowerPoint que ha abierto ella, y el que ya estaba abierto lo deja como
  lo encontró. *(Cierra [TJ-01](ROADMAP.md).)*
- **Las presentaciones se convierten de una en una.** PowerPoint no admite dos sesiones a la vez —
  Windows abre una sola, por mucho que se le pidan más—, así que convertir varias presentaciones en
  paralelo las hacía competir por la misma. Word y Excel siguen convirtiéndose en paralelo, que ahí sí
  hay una sesión por documento.
- **Convertir con LibreOffice podía BORRAR un archivo anterior.** Si en la carpeta de destino ya había
  un `informe.pdf` y se convertía `informe.docx`, LibreOffice escribía encima del que estaba y la app
  renombraba el nuevo a `informe (1).pdf`: el archivo de antes **desaparecía**, en contra de lo que
  promete el programa («sin sobrescrituras»). Ahora cada conversión escribe en una carpeta temporal
  propia y de ahí se mueve al destino, que se vuelve a comprobar en el último momento: los dos archivos
  quedan intactos. *(Cierra [TJ-03](ROADMAP.md).)*
- **Una conversión con LibreOffice podía quedarse congelada para siempre.** Los avisos que escribe
  LibreOffice se acumulaban sin que nadie los leyera; al llenarse el búfer del sistema (unos 4 KB), el
  programa se bloqueaba a mitad y la conversión se quedaba ahí, sin error y sin terminar, ocupando una
  de las plazas de conversión simultánea — con unas cuantas así, la app dejaba de convertir. Basta un
  documento con bastantes avisos de fuentes o macros para llegar a ese tamaño. *(Cierra
  [TJ-02](ROADMAP.md).)*
- **Un fallo silencioso de LibreOffice ya se cuenta.** Cuando terminaba «bien» pero sin generar nada
  —pasa con formatos que su filtro no soporta para ese documento— la app daba la conversión por buena y
  apuntaba en el historial un archivo que no existía. Ahora se informa del fallo.
- **La actualización automática podía quedarse colgada en los equipos sin Office.** El instalador
  avisa cuando no encuentra Microsoft Office, y ese aviso salía **también en modo silencioso** — que es
  como lo lanza la propia app al actualizarse, con la ventana ya cerrada. Quien solo tiene
  **LibreOffice** (una instalación que OfiConvert soporta a propósito) veía desaparecer el programa y
  quedarse un diálogo que no había pedido, o la actualización parada esperando un clic. Ahora el aviso
  se calla en las instalaciones silenciosas. *(Cierra [TJ-04](ROADMAP.md).)*

### Interno

- **249 pruebas** (antes 237): +9 sobre la línea de comandos de LibreOffice y +3 con Office real
  (PowerPoint sin ventana, y los dos caminos de arranque de Word). Las tres correcciones, comprobadas en
  rojo.

- Afinado del propio Tier J, hecho al validar en un segundo equipo: (a) las tres pruebas que conducen
  Office eran **inestables** —fallaban una de cada dos veces por su propia limpieza, no por el
  producto—; ahora esperan a que PowerPoint se cierre de verdad en vez de darlo por hecho. (b) El
  cazador de texto sin traducir tenía un hueco en su expresión regular: `StateMessage = "Pendiente"`
  no casaba, y ahí seguía el literal. (c) Dos guardianes nuevos sobre el instalador. Los tres
  arreglos, comprobados en rojo.

- **Las notas de un release ya cuentan qué cambió.** Hasta ahora `release.ps1` publicaba en GitHub el
  mismo texto de plantilla en todas las versiones («Instalador self-contained para Windows x64…»), así
  que quien abría un release no podía saber qué traía. Ahora las notas **son la sección `## [X.Y.Z]` de
  este archivo**, y el corte **aborta antes de compilar nada** si esa sección no está escrita — lo que
  obliga a redactarla antes de cortar. `-NotesFile` sigue mandando sobre todo esto.
  *(Cierra [TJ-07](ROADMAP.md); ver [`CONTEXT.md`](CONTEXT.md).)*
- **Dos pruebas nuevas** (`ChangelogTests`) que fallan en `dotnet test` —y no a mitad del corte— si la
  versión del `.csproj` no está contada en el changelog, o si una versión publicada se quedó sin fecha.
- **Las pruebas de UI ya conducen el binario que se publica.** El corte compilaba en Release y luego
  corría `dotnet test` sin `-c Release`, así que MSBuild reconstruía la app en **Debug** y las 30
  pruebas de interfaz validaban ese binario, no el Release que empaqueta el instalador. `AppFixture`
  ya no elige "el `.exe` más reciente": exige el de la configuración compilada, deja por escrito cuál
  conduce y `DrivenBinaryTests` falla si no coinciden. *(Cierra [TJ-05](ROADMAP.md).)*
- **Los servicios devuelven claves de traducción, no frases** (`Core/UserMessage`), y la traducción
  ocurre en un único borde (`LocalizationService.Translate`). Un servicio que corre en un hilo de fondo
  no sabe —ni debe saber— en qué idioma está la interfaz: devolviendo texto, no había forma de acertar.
- **El guardián de textos en duro pasa de dos archivos a todos** (TJ-17): se descubren solos, y además
  mira los literales que viajan **como argumento** (`ShowError("…")`, `Failed("…")`), que es por donde
  se colaron los 18. Comprobado en rojo reintroduciendo dos de ellos. El escáner de claves aprende
  también las formas nuevas, en el mismo cambio que las crea.
- **Pruebas nuevas contra el Office real** (`PowerPointSharedInstanceTests`), omitidas por defecto y
  activables con `OFICONVERT_OFFICE_TESTS=1`: comprueban la premisa (dos activaciones de PowerPoint
  dejan **un** proceso; Word deja dos) y el escenario completo de TJ-01. El corte de versión no depende
  de ellas.
- **La ejecución de procesos externos se centraliza en `Services/ProcessRunner`**, que lee `stdout` y
  `stderr` antes de esperar al proceso. `ProcessRunnerTests` reproduce el cuelgue con 64 KB de salida
  —dieciséis veces el búfer— sin necesitar LibreOffice instalado, y la lógica de destino vive ahora en
  `Core/LibreOfficeOutput`, con pruebas propias.
- **Tres guardianes nuevos sobre el script del instalador** (`InstallerScriptTests`): ningún `MsgBox`
  sin la guarda `WizardSilent`, el alcance sigue fijándose por línea de comandos y los modificadores
  del updater se arman en `Core/InstallScope` —probado— y no a mano en el code-behind.

---

## [2.6.1] — 2026-08-29

### Corregido

- **Los menús desplegables se veían borrosos y con el texto poco legible.** Al abrir cualquier
  desplegable (*Tema*, *Idioma*, *Formato*, *Formato por defecto*) el menú transparentaba lo que había
  debajo y le pintaba encima una textura granulada, así que costaba leer las opciones. Ahora tienen
  fondo sólido, en tema claro, oscuro y alto contraste. *(Origen: reporte sobre una captura de Ajustes;
  ver [`CONTEXT.md`](CONTEXT.md#2026-08-24--los-desplegables-se-veían-borrosos--v261-publicada-2026-08-29).)*

### Interno

- Nueva herramienta `tools/capture-dropdown.ps1`: abre los cuatro desplegables por UI Automation y
  **mide** el ruido del fondo en vez de dejarlo a ojo (roto a propósito: 38–68 % de ruido; arreglado:
  0 %). Las pruebas de xUnit no miran píxeles; para eso está el script.
- El arreglo va en `App.xaml`, dentro de `MergedDictionaries` **después** de `XamlControlsResources`:
  en la raíz de `Application.Resources` los `ThemeDictionaries` compilan sin avisar y **no se aplican**.

---

## [2.6.0] — 2026-07-21

Pase de UX/UI hecho **mirando la app**, no leyendo el XAML: se fotografiaron los siete estados en tema
claro y oscuro y aparecieron tres fallos que el código no delataba.

### Corregido

- **El historial no distinguía un fallo de un acierto.** Todas las filas mostraban el mismo tilde verde,
  así que una conversión fallida se veía **idéntica** a una correcta y no decía por qué había fallado.
  Ahora el icono y el color cambian con el resultado, y las filas fallidas muestran su motivo.
- **Los diálogos ignoraban el tema elegido.** Con la app en Claro sobre un Windows en Oscuro, los
  diálogos (legal, cierre, actualización) salían negros.
- **El panel de resultados encabezaba los errores con un tilde verde.** Cuando parte del lote fallaba,
  el resumen se anunciaba como si todo hubiera ido bien. Ahora muestra un aviso ámbar — no rojo, porque
  parte del lote sí se convirtió.

### Cambiado

- Los botones que destruyen algo (*Cancelar*, *Limpiar historial*, *Desregistrar*) pasan a **contorno
  rojo** en vez de relleno sólido: con acentos de sistema cálidos, el relleno rojo era indistinguible
  del botón de acento que tenía al lado.
- El diálogo de textos legales se ensancha: la licencia MIT ya no parte palabras sueltas.
- El historial muestra la duración **con su unidad** y con las columnas equilibradas; la fila de
  acciones se reordena (origen a la izquierda, acciones a la derecha).
- Las capturas del README se regeneran con un **acento neutro**: antes salían con el color de acento
  personal de quien las generaba.

### Interno

- `tools/capture-ui-states.ps1`: galería de revisión que siembra y fotografía todos los estados ×2 temas.
- 230 pruebas (+4 de `HistoryStatus`). *(Detalle en [`CONTEXT.md`](CONTEXT.md).)*

---

## [2.5.0] — 2026-07-14

Primera vez que se probó el instalador **de punta a punta** (instalación limpia, desinstalación y
actualización sobre una instalación real). Salieron tres fallos, dos de ellos llevaban cuatro versiones
escondidos.

### Corregido

- **La instalación silenciosa no era silenciosa.** El instalador se plantaba con el cuadro «Seleccione el
  modo de instalación» **incluso lanzado en modo silencioso**, y se quedaba bloqueado esperando un clic.
  En una actualización automática eso significaba ver el programa desaparecer y aparecer un diálogo que
  nadie había pedido. Ahora la actualización conserva sin preguntar el modo con el que se instaló la app.
- **La app se cerraba aunque rechazaras el permiso de administrador.** Instalada *para todos los
  usuarios*, actualizar pide UAC; si decías que no, el programa se cerraba igual, seguía en la versión
  vieja y no daba ninguna explicación. Ahora sigue abierto y avisa de lo ocurrido.
- **Todo el flujo de actualización estaba en español**, en los ocho idiomas: «Descargando… 42 %»,
  «Instalar ahora», «Comprobando…», y los botones «Sí», «No», «Aceptar» de los diálogos.

### Interno

- `HardcodedUiTextTests`: prohíbe asignar un literal a una propiedad de texto de la interfaz.
- `LocalizationUsageTests` amplía su escáner a la forma `loc["Clave"]` — la que usa medio `MainWindow` y
  por la que se le había escapado una clave inexistente.
- 15 claves nuevas × 8 idiomas. 226 pruebas.

---

## [2.4.0] — 2026-07-14

### Añadido

- **Licencia y avisos de terceros dentro de la propia app** (*Configuración → Acerca de*), en los ocho
  idiomas y con el texto íntegro de las licencias que obligan a entregarlo.
- **README de usuario** con capturas, instalación y el modelo de confianza de las actualizaciones.
- **Estado vacío del historial** con icono, título y subtítulo, igual que el de la pestaña de conversión.
- Nombres accesibles y descripciones emergentes para los botones de solo icono y los tres interruptores
  de Ajustes: hasta ahora un lector de pantalla anunciaba «botón» y nada más.

### Corregido

- **El contador de reintentos estaba invertido:** se mostraba `↻ 0` en todas las filas y **desaparecía**
  justo cuando un archivo había reintentado.
- **La carpeta de destino prometía algo que no existía.** El texto decía «Misma ubicación que archivos
  originales», pero al convertir sin carpeta la app interrumpía con un diálogo y, si decías que no,
  **cancelaba el lote entero**. Ahora la promesa se cumple: cada documento se convierte junto al
  original y convertir **no exige configurar nada**.
- **«Limpiar historial» borraba hasta 1000 registros sin preguntar** — la única acción irreversible de
  la app, y la única sin confirmación.

### Cambiado

- **Los botones se apagan solos** cuando no hay nada que hacer: *Convertir* y *Limpiar* con la cola
  vacía, *Exportar* con el historial vacío. Antes estaban siempre encendidos y la app te reñía con un
  diálogo después de pulsarlos. Desaparecen tres diálogos y cinco textos × 8 idiomas.
- **Un solo botón de acento**, *Convertir*; *Archivo* pasa a neutro.
- La **barra de progreso** solo aparece mientras se convierte; antes ocupaba sitio mostrando «0 %».
- **Ajustes** se agrupa en tres bloques: Apariencia · Conversión · Integración.

### Interno

- `THIRD-PARTY-NOTICES.txt` **verificado paquete a paquete**: no todo es MIT (Serilog es Apache-2.0,
  WebView2 es BSD-3-Clause y el Windows App SDK va bajo términos de Microsoft). Los textos viajan
  **embebidos en el `.exe`**. `tools/capture-screenshots.ps1` regenera las capturas conduciendo la app.
  212 pruebas.

---

## [2.3.0] — 2026-07-14

### Corregido

- **La interfaz estaba en español en los ocho idiomas.** Botones y etiquetas no cambiaban al elegir otro
  idioma, y **ni reiniciando**: solo se traducían los mensajes y los estados. El idioma elegido sí se
  guardaba, así que desde fuera todo parecía correcto. Lo encontraron las pruebas nuevas.
- **El diálogo que protege al cerrar durante una conversión no estaba traducido**, y es el que evita
  dejar procesos de Office colgados. Sus traducciones ya existían en los ocho idiomas, con otro nombre y
  sin usarse.

### Interno

- De 11 pruebas a **170** (152 unitarias + 18 que conducen la app real con FlaUI). Se extrae `Core/`
  —lógica pura y comprobable: rutas de salida seguras, firmas de archivo, saneado del CSV, mapeo de
  formatos— y se añaden pruebas de completitud de los ocho diccionarios de idioma.

---

## [2.2.0] — 2026-07-13

### Seguridad

- **La app ya no ejecuta un instalador que no haya verificado.** Hasta ahora, la actualización
  automática descargaba un `.exe` de internet y **lo ejecutaba sin comprobar nada**. Ahora acepta una
  firma Authenticode válida o, en su defecto, comprueba el **SHA-256** contra el hash publicado junto al
  instalador; si no supera ninguna de las dos, **borra el archivo y cancela**, diciendo por qué.
  - *Alcance honesto:* el instalador y su hash salen del mismo release, así que esto detecta corrupción
    y manipulación **en tránsito**, no un compromiso de la cuenta de GitHub.
  - *Consecuencia:* **todo release debe publicar su `.sha256`**, o los clientes rechazarán la
    actualización.

### Interno

- Primeras 11 pruebas del proyecto: ejercen la **descarga completa** contra un servidor HTTP local, no
  solo el cálculo del hash. Más una prueba opcional que verifica el **release real publicado**.

---

## [2.1.0] — 2026-07-13

### Corregido

- **Los seis idiomas distintos de español e inglés no sobrevivían a un reinicio:** la app los reseteaba
  a español al arrancar, pisando la elección guardada.
- **El menú contextual del Explorador funciona, y con una sola ventana.** Seleccionar cinco documentos y
  elegir *Convertir con OfiConvert* los encola **todos en la ventana ya abierta**, en vez de abrir cinco
  programas peleándose por la misma cola.
- **Los archivos añadidos a mitad de una conversión se borraban sin convertir.** Afectaba también a
  arrastrar y soltar, y llevaba en producción desde el principio. El lote se fija al empezar.
- **Los valores por defecto pisaban los ajustes guardados** al arrancar.
- El aviso de fin de lote deja de ser un **diálogo modal** que interrumpía: ahora es un sonido y un
  parpadeo en la barra de tareas, y **solo si la ventana no está delante**.
- El registro de fallos pasa a `%AppData%\OfiConvert\`, en vez de junto al ejecutable.

### Añadido

- Archivo **`LICENSE`** (MIT), que el README prometía y no existía.

### Cambiado

- README y metadatos veraces: describían el stack **WPF** de la 1.0.

### Interno

- Corte de versión en un solo comando (`release.ps1`): valida, compila, prueba, sube las tres etiquetas
  de versión, genera instalador y `.sha256`, y publica el release. Build en 0 errores / 0 advertencias.

---

## [2.0.0] — 2026-04-03

### Cambiado

- **Migración completa de WPF a WinUI 3** (Windows App SDK): interfaz Fluent con fondo Mica y barra de
  título propia.
- La app pasa a publicarse **self-contained**: ya no hace falta instalar .NET para usarla.

### Añadido

- Progreso de descarga en el aviso de actualización.

---

## [1.0.0] — 2026-04-02

Primera versión publicada (WPF).

### Añadido

- Conversión **por lotes** de Word, Excel y PowerPoint a **PDF, HTML, CSV, PNG y JPG**.
- Cola persistente, pausa/reanudación/cancelación, conversiones en paralelo y reintentos automáticos.
- Validación previa de cada archivo: corruptos, protegidos con contraseña, vacíos o bloqueados.
- Historial exportable, minimización a la bandeja, menú contextual del Explorador.
- **8 idiomas**, tema claro/oscuro/sistema y aviso de actualización vía GitHub Releases.

---

[Sin publicar]: https://github.com/xfiberex/OfiConvert/compare/v2.6.1...HEAD
[2.6.1]: https://github.com/xfiberex/OfiConvert/compare/v2.6.0...v2.6.1
[2.6.0]: https://github.com/xfiberex/OfiConvert/compare/v2.5.0...v2.6.0
[2.5.0]: https://github.com/xfiberex/OfiConvert/compare/v2.4.0...v2.5.0
[2.4.0]: https://github.com/xfiberex/OfiConvert/compare/v2.3.0...v2.4.0
[2.3.0]: https://github.com/xfiberex/OfiConvert/compare/v2.2.0...v2.3.0
[2.2.0]: https://github.com/xfiberex/OfiConvert/compare/v2.1.0...v2.2.0
[2.1.0]: https://github.com/xfiberex/OfiConvert/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/xfiberex/OfiConvert/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/xfiberex/OfiConvert/releases/tag/v1.0.0
