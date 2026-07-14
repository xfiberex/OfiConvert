# OfiConvert

Conversor de escritorio **por lotes** de documentos de Microsoft Office (Word, Excel, PowerPoint) a
**PDF, HTML, CSV, PNG y JPG**.

Construido con **WinUI 3** (Windows App SDK) y patrón MVVM.

---

## Requisitos del Sistema

- Windows 10 (2004 / build 19041) o superior, x64.
- **Microsoft Office** de escritorio *o* **LibreOffice**. Sin ninguno de los dos, la app no puede convertir.
- No requiere instalar .NET: la app se publica *self-contained*.

### Motores de conversión

OfiConvert automatiza un motor ya instalado en el equipo; no reimplementa los formatos de Office.

| Motor | Cómo se usa | Formatos de entrada |
|-------|-------------|---------------------|
| **Microsoft Office** *(principal)* | COM Interop (Word, Excel, PowerPoint) | `.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx` |
| **LibreOffice** *(alternativo)* | `soffice --headless --convert-to` | los mismos |

**Office** es el motor preferente. **LibreOffice** entra automáticamente en dos casos: si Office no está
instalado, o si Office falla tras agotar los reintentos.

**Versiones de Office compatibles:** 2013+ y Microsoft 365 (instalación **de escritorio**).
Office Online / Office web **no sirve**: no expone COM.

### Formatos de salida por tipo de documento

| Documento | Salidas |
|-----------|---------|
| Word (`.doc`, `.docx`) | PDF, HTML |
| Excel (`.xls`, `.xlsx`) | PDF, CSV *(una hoja)* |
| PowerPoint (`.ppt`, `.pptx`) | PDF, PNG, JPG *(una imagen por diapositiva)* |

---

## Instalación

Descarga el instalador (`OfiConvert_Setup_X.Y.Z.exe`) desde la
[página de Releases](https://github.com/xfiberex/OfiConvert/releases) y ejecútalo.

Se instala **para el usuario actual** y no pide permisos de administrador (puedes elegir instalar para
todos los equipos desde el propio instalador). Si no detecta Office, avisa pero deja continuar —
LibreOffice puede cubrirlo.

> El instalador **no está firmado**, así que SmartScreen mostrará "editor desconocido" la primera vez.

---

## Características

- **Conversión por lotes** con cola: la cola sobrevive al cierre de la app.
- **Pausar, reanudar y cancelar** una conversión en curso.
- **Conversiones en paralelo**, con límite configurable (1–8).
- **Reintentos automáticos** ante fallos, con espera creciente entre intentos.
- **Validación previa** de cada archivo: detecta corruptos, protegidos con contraseña, vacíos o
  bloqueados por otro proceso — antes de abrir Office.
- **Arrastrar y soltar** archivos sobre la ventana.
- **Menú contextual del Explorador**: clic derecho sobre documentos Office → *Convertir con OfiConvert*;
  los archivos se encolan en la ventana ya abierta.
- **Protección contra macros**: los documentos se abren en solo lectura y con las macros deshabilitadas.
- **Sin sobrescrituras**: si el destino ya existe, se renombra (`informe (1).pdf`).
- Barra de progreso, estado por archivo y aviso al terminar (sonido + parpadeo en la barra de tareas)
  cuando la ventana no está en primer plano.
- **Historial** de conversiones, exportable a CSV o TXT.
- Minimización a la **bandeja del sistema**.
- Interfaz **Fluent** (Mica) con tema claro, oscuro o el del sistema.
- **8 idiomas**: español, inglés, portugués, francés, alemán, italiano, chino y japonés.
- **Aviso de actualización** vía GitHub Releases, con instalación en un clic.

### Actualizaciones: el modelo de confianza

Antes de ejecutar un instalador descargado, OfiConvert **lo verifica**: si trae una firma Authenticode
válida, la acepta; si no (hoy los instaladores se publican **sin firmar**), comprueba su **SHA-256**
contra el hash publicado como asset del release. Si no supera ninguna de las dos, **borra el archivo y
cancela la actualización**.

Alcance honesto: el instalador y su hash salen del mismo release, así que esto detecta corrupción y
manipulación **en tránsito**, pero no protegería frente a un compromiso de la propia cuenta de GitHub.

---

## Cómo usar

1. **Añade archivos**: botón `Archivo`, arrastrándolos a la ventana, o desde el menú contextual del
   Explorador.
2. **Elige el formato** de salida (según el tipo de documento).
3. **Elige la carpeta de destino** *(opcional)*: si no la eliges, se te pedirá al convertir.
4. **Convertir**. Puedes pausar, reanudar o cancelar mientras corre.

---

## Datos y privacidad

Todo se guarda en `%AppData%\OfiConvert\`: `settings.json` (preferencias), `history.json` (historial),
`queue.json` (la cola pendiente) y `logs\` (registro diario, se conservan 30 días).

La app **no envía telemetría**. La única conexión de red que hace es consultar la
[API de Releases de GitHub](https://api.github.com/repos/xfiberex/OfiConvert/releases/latest) para
comprobar si hay una versión nueva.

---

## Compilar desde código fuente

```powershell
git clone https://github.com/xfiberex/OfiConvert.git
cd OfiConvert

# Compilar
dotnet build OfiConvert.slnx -c Release
dotnet build OfiConvert.slnx -c Debug

# Ejecutar
dotnet run --project OfiConvert.csproj

# Pruebas
dotnet test tests\OfiConvert.Tests\OfiConvert.Tests.csproj

# Pruebas UI
dotnet test tests\OfiConvert.UiTests\OfiConvert.UiTests.csproj

# Publicar (self-contained, win-x64)
dotnet publish OfiConvert.csproj -c Release -r win-x64 --self-contained -o ./publish
```

### Crear el instalador

Requiere [Inno Setup 6+](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
.\installer\build-installer.ps1
```

Publica, compila el instalador y genera su `.sha256` en `installer/Output/`. La versión sale del
`<Version>` del `.csproj` — **fuente única**; no se edita el `.iss` a mano.

### Publicar una versión

```powershell
.\release.ps1 -Version 2.1.0 -DryRun   # simula: compila el instalador, no toca git ni GitHub
.\release.ps1 -Version 2.1.0           # corte real
```

Valida → compila y pasa las pruebas → sube las tres etiquetas de versión del `.csproj` → compila el
instalador → commit + tag `vX.Y.Z` → push → crea el GitHub Release con el instalador **y su `.sha256`**.

> `release.ps1` solo hace `git add -u` (archivos ya rastreados): los **archivos nuevos** hay que
> `git add`earlos **antes**, o el release saldría sin ellos.

---

## Estructura del proyecto

```
OfiConvert/
├── Assets/          Icono de la aplicación
├── Behaviors/       Arrastrar y soltar
├── Converters/      Converters de binding (XAML)
├── Helpers/         Localización, rutas de datos, aviso al terminar, argumentos de activación
├── installer/       Script de Inno Setup
├── Lang/            Diccionarios de los 8 idiomas (se leen en tiempo de ejecución)
├── Models/          Modelos y enumeraciones
├── Services/        Conversión (Office/LibreOffice), validación, historial, ajustes, updater…
├── tests/           Pruebas (xUnit)
├── ViewModels/      MainViewModel (MVVM)
├── App.xaml(.cs)    Arranque, activaciones
├── Program.cs       Punto de entrada, instancia única
└── MainWindow.xaml(.cs)
```

Documentación para desarrollo: [`CONTEXT.md`](CONTEXT.md) (arquitectura, decisiones y por qué) y
[`ROADMAP.md`](ROADMAP.md) (qué falta).

---

## Paquetes NuGet

| Paquete | Versión | Propósito |
|---------|---------|-----------|
| Microsoft.WindowsAppSDK | 1.8.260317003 | WinUI 3 |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM (source generators) |
| H.NotifyIcon.WinUI | 2.2.0 | Icono de bandeja |
| Serilog + Serilog.Sinks.File | 4.2.0 / 6.0.0 | Registro |

---

## Solución de problemas

| Problema | Solución |
|----------|----------|
| *No hay ningún motor de conversión* | Instala Microsoft Office de escritorio o LibreOffice. |
| *Error al abrir el documento* | Repara la instalación de Office desde Configuración → Aplicaciones. |
| *El archivo está protegido con contraseña* | OfiConvert no descifra documentos: quita la contraseña y reinténtalo. |
| *Permisos insuficientes* | Comprueba que puedes escribir en la carpeta de destino. |
| *Office Online no funciona* | Se necesita Office **de escritorio**; la versión web no expone COM. |

¿Algo falló? El detalle está en `%AppData%\OfiConvert\logs\`.

---

## Licencia

[MIT](LICENSE)

## Autor

**Ricky Angel Jimenez Bueno**
