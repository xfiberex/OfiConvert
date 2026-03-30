# OfiConvert

Aplicación de escritorio para convertir documentos de Microsoft Office (Word, Excel, PowerPoint) a formato PDF.

Construida con WPF + WPF UI (Fluent Design) y patrón MVVM.

---

## Requisitos del Sistema

### Microsoft Office (obligatorio)

OfiConvert utiliza COM Interop para automatizar la conversión, por lo que necesita Office instalado localmente:

| Componente | Formatos soportados |
|------------|-------------------|
| Microsoft Word | `.doc`, `.docx` |
| Microsoft Excel | `.xls`, `.xlsx` |
| Microsoft PowerPoint | `.ppt`, `.pptx` |

**Versiones compatibles:** Office 2013+, Microsoft 365 (instalación de escritorio).
Office Online / Office web **no es compatible**.

### Otros requisitos

- Windows 10/11 (x64)
- Al publicar como self-contained no se requiere .NET Runtime adicional.

---

## Características

- Conversión por lotes de múltiples archivos a PDF.
- Drag & Drop de archivos sobre la ventana.
- Selección de carpeta de destino personalizada.
- Barra de progreso con porcentaje.
- Cancelación de conversión en curso.
- Indicadores visuales de estado por archivo (pendiente, convirtiendo, completado, error).
- Protección contra macros: documentos abiertos en modo solo lectura con macros deshabilitadas.
- Prevención de sobrescritura de archivos existentes (renombrado automático).
- Interfaz moderna Fluent Design con tema oscuro.

---

## Cómo Usar

1. **Seleccionar archivos**: haz clic en `Archivo` o arrastra documentos Office a la ventana.
2. **Elegir destino** *(opcional)*: selecciona una carpeta de salida. Si no se elige, se pedirá al convertir.
3. **Convertir**: haz clic en `Convertir`. Los PDFs se generarán en la carpeta elegida.
4. **Cancelar**: si la conversión está en curso, usa el botón `Cancelar` para detenerla.

---

## Compilar desde Código Fuente

```bash
# Clonar repositorio
git clone <url-repositorio>
cd OfiConvert

# Restaurar paquetes y compilar
dotnet build -c Release

# Publicar (self-contained, single-file, win-x64)
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=true `
    -o ./publish/win-x64
```

---

## Crear Instalador (InnoSetup)

1. Instala [Inno Setup 6+](https://jrsoftware.org/isinfo.php).
2. Publica el proyecto (ver sección anterior).
3. Abre `installer/OfiConvert.iss` con Inno Setup.
4. Compila el script (Ctrl+F9).
5. El instalador se genera en `installer/Output/`.

---

## Estructura del Proyecto

```
OfiConvert/
+-- Behaviors/          # Comportamientos XAML (Drag & Drop)
+-- Converters/         # Value Converters para bindings
+-- Models/             # Modelos y enumeraciones
+-- Properties/         # Recursos embebidos
+-- Resources/          # Iconos de la aplicación
+-- Services/           # Interfaces e implementaciones de servicios
+-- ViewModels/         # ViewModels (MVVM)
+-- installer/          # Script InnoSetup
+-- App.xaml(.cs)       # Punto de entrada, recursos globales
+-- MainWindow.xaml(.cs)# Ventana principal
```

---

## Paquetes NuGet

| Paquete | Versión | Propósito |
|---------|---------|-----------|
| CommunityToolkit.Mvvm | 8.4.0 | MVVM source generators |
| WPF-UI | 4.1.0 | Fluent Design controls |
| Microsoft.Xaml.Behaviors.Wpf | 1.1.135 | Behaviors para XAML |

---

## Solución de Problemas

| Problema | Solución |
|----------|---------|
| *Office no encontrado* | Verifica que Office esté instalado localmente y funcione. |
| *Error al abrir documento* | Repara la instalación de Office desde Panel de Control. |
| *Permisos insuficientes* | Verifica permisos de escritura en la carpeta de destino. |
| *Office Online no compatible* | Se requiere Office de escritorio, no la versión web. |

---

## Licencia

MIT License

## Autor

**Ricky Angel Jimenez Bueno**