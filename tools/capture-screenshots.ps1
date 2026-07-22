<#
.SYNOPSIS
    Regenera las capturas de docs/screenshots/ conduciendo la app REAL por UI Automation.

.DESCRIPTION
    Las capturas del README envejecen peor que el código: nadie las rehace al cambiar la UI, y acaban
    enseñando una app que ya no existe. Esto las regenera en un comando.

    Tres cosas que no son obvias y que cuestan un rato descubrir:

    1. DATOS REALES DEL USUARIO. La app es *unpackaged*: su settings.json y su queue.json viven en
       %AppData%\OfiConvert, el MISMO sitio que usa la instalación real de quien corre esto. El script
       los RESPALDA y los RESTAURA al terminar, pase lo que pase. Sin eso, capturar te cambia el tema y
       te borra la cola que tuvieras pendiente.

    2. LA COLA SE SIEMBRA. queue.json es una simple lista de rutas, así que se le meten documentos de
       ejemplo (creados en %TEMP%) y la app abre CON archivos dentro. Una captura de la cola vacía no
       enseña el producto.

    3. SE CAPTURA LA PANTALLA, no la ventana. Un WinUI 3 con Mica no se deja capturar por PrintWindow
       (sale negro o sin backdrop): se copia el rectángulo de pantalla que ocupa la ventana. Y ese
       rectángulo se pide con DWMWA_EXTENDED_FRAME_BOUNDS, no con GetWindowRect, que en Windows 10/11
       devuelve unos píxeles de más por la sombra invisible del marco.

    NO requiere elevación (la app corre asInvoker) ni Office instalado: no se convierte nada.

.PARAMETER OutputDir
    Carpeta de salida. Por defecto docs/screenshots.

.PARAMETER ExePath
    Ruta al OfiConvert.exe. Por defecto, el más reciente de bin\ (win-x64).

.PARAMETER Accent
    Color de acento en hex para las capturas. Por defecto #0078D4 (azul por defecto de Windows). La app
    respeta el acento del sistema; esto lo NEUTRALIZA solo para el README (App.OnLaunched lee OFICONVERT_ACCENT
    únicamente para esto), para que las imágenes no salgan con el acento personal de quien las genera.

.EXAMPLE
    .\tools\capture-screenshots.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputDir,
    [string]$ExePath,
    [string]$Accent = "#0078D4"
)

$ErrorActionPreference = "Stop"

function Info($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "[OK] $m" -ForegroundColor Green }
function Warn($m) { Write-Host "[!] $m" -ForegroundColor Yellow }
function Die($m)  { Write-Host "[X] $m" -ForegroundColor Red; exit 1 }

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class Win32Capture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // El rectángulo REAL de la ventana. GetWindowRect incluye la sombra invisible del marco y mete
    // varios píxeles de fondo del escritorio en cada captura.
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int SW_RESTORE = 9;
}
'@

# ── Rutas ──────────────────────────────────────────────────────────────────
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $root "docs\screenshots" }

$dataDir      = Join-Path $env:APPDATA "OfiConvert"
$settingsPath = Join-Path $dataDir "settings.json"
$queuePath    = Join-Path $dataDir "queue.json"
$sampleDir    = Join-Path $env:TEMP "OfiConvert-capture-docs"

if (-not $ExePath) {
    $candidate = Get-ChildItem (Join-Path $root "bin") -Filter "OfiConvert.exe" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*\win-x64\*" } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if (-not $candidate) { Die "No se encontró OfiConvert.exe. Compila primero: dotnet build OfiConvert.slnx -c Release" }
    $ExePath = $candidate.FullName
}
if (-not (Test-Path $ExePath)) { Die "No existe el ejecutable: $ExePath" }

Info "Ejecutable: $ExePath"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# ── Documentos de ejemplo para la cola ─────────────────────────────────────
# Nombres realistas: la captura enseña el producto, no un lorem ipsum. El contenido da igual (no se
# convierte nada), pero el tamaño sí se ve en la UI, así que se les da un peso creíble.
function New-SampleDocs {
    New-Item -ItemType Directory -Force -Path $sampleDir | Out-Null
    $samples = [ordered]@{
        "Informe anual.docx"        = 340
        "Presupuesto 2026.xlsx"     = 128
        "Presentacion ventas.pptx"  = 890
    }
    $paths = @()
    foreach ($name in $samples.Keys) {
        $path = Join-Path $sampleDir $name
        $bytes = New-Object byte[] ($samples[$name] * 1024)
        [System.IO.File]::WriteAllBytes($path, $bytes)
        $paths += $path
    }
    return $paths
}

function Set-CaptureState([string]$theme, [string[]]$queue) {
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

    $settings = [ordered]@{
        Theme                  = $theme      # "Light" | "Dark"
        Language               = "es"
        MaxParallelConversions = 2
        AutoRetryEnabled       = $true
        MaxRetryCount          = 3
        MinimizeToTray         = $false
        ShowNotifications      = $true
        LastOutputFolder       = ""
        DefaultOutputFormat    = 0           # PDF
    }
    $settings | ConvertTo-Json | Set-Content -Path $settingsPath -Encoding utf8
    $queue    | ConvertTo-Json | Set-Content -Path $queuePath    -Encoding utf8
}

# ── UI Automation ──────────────────────────────────────────────────────────
function Find-MainWindow([int]$processId, [int]$timeoutSec = 40) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)

    while ((Get-Date) -lt $deadline) {
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children, $condition)
        if ($window) { return $window }
        Start-Sleep -Milliseconds 300
    }
    Die "La app no abrió su ventana principal en $timeoutSec s."
}

function Find-ById($parent, [string]$automationId, [int]$timeoutSec = 20) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)

    while ((Get-Date) -lt $deadline) {
        $element = $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($element) { return $element }
        Start-Sleep -Milliseconds 200
    }
    return $null
}

# El peer del PivotItem expone SelectionItem (es un TabItem para UIA). OJO: el Pivot de WinUI DESCARGA
# el contenido de la pestaña que no está delante, así que hay que seleccionarla antes de capturarla.
function Select-Tab($window, [string]$tabId) {
    $tab = Find-ById $window $tabId
    if (-not $tab) { Die "No se encontró la pestaña '$tabId'." }
    $pattern = $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $pattern.Select()
    Start-Sleep -Milliseconds 900
}

# Lleva un control a la vista dentro de su ScrollViewer. La pestaña de Ajustes es más alta que la
# ventana: sin esto, "Acerca de" (los textos legales) se queda fuera de la captura, y es justo lo que la
# captura tiene que enseñar.
function Show-Element($window, [string]$automationId) {
    $element = Find-ById $window $automationId
    if (-not $element) { Warn "No se encontró '$automationId' para desplazarlo a la vista."; return }

    try {
        $pattern = $element.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern)
        $pattern.ScrollIntoView()
        Start-Sleep -Milliseconds 700
    }
    catch {
        Warn "'$automationId' no admite ScrollItemPattern: se captura sin desplazar."
    }
}

function Save-WindowPng([IntPtr]$hwnd, [string]$path) {
    [void][Win32Capture]::ShowWindow($hwnd, [Win32Capture]::SW_RESTORE)
    [void][Win32Capture]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 700   # deja que la ventana llegue al frente y Mica se asiente

    $rect = New-Object Win32Capture+RECT
    $size = [System.Runtime.InteropServices.Marshal]::SizeOf([type]([Win32Capture+RECT]))
    [void][Win32Capture]::DwmGetWindowAttribute($hwnd, [Win32Capture]::DWMWA_EXTENDED_FRAME_BOUNDS, [ref]$rect, $size)

    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $h -le 0) { Die "La ventana devolvió un rectángulo vacío ($w x $h)." }

    $bmp = New-Object System.Drawing.Bitmap $w, $h
    try {
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        try {
            $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
        } finally { $g.Dispose() }
        $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bmp.Dispose() }

    Ok "$([System.IO.Path]::GetFileName($path))  ($w x $h)"
}

function Capture-Theme([string]$theme, [string[]]$queue) {
    $suffix = $theme.ToLowerInvariant()
    Info "Tema $theme..."

    Set-CaptureState $theme $queue

    $proc = Start-Process $ExePath -PassThru
    try {
        $window = Find-MainWindow $proc.Id
        Start-Sleep -Seconds 2   # la cola se rellena y las miniaturas del shell tardan un instante
        $hwnd = [IntPtr]$window.Current.NativeWindowHandle

        Save-WindowPng $hwnd (Join-Path $OutputDir "main-$suffix.png")

        Select-Tab $window "tabSettings"
        Show-Element $window "btnLicencia"   # deja "Acerca de" (licencia y avisos) dentro del encuadre
        Save-WindowPng $hwnd (Join-Path $OutputDir "settings-$suffix.png")
    }
    finally {
        if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Milliseconds 500
    }
}

# ── Ejecución ──────────────────────────────────────────────────────────────
# Respaldo de los datos REALES del usuario: la app es unpackaged y escribe en el mismo sitio que su
# instalación de verdad. Se restauran en el finally, aunque la captura reviente a mitad.
$backupSettings = if (Test-Path $settingsPath) { Get-Content $settingsPath -Raw } else { $null }
$backupQueue    = if (Test-Path $queuePath)    { Get-Content $queuePath -Raw }    else { $null }

Get-Process OfiConvert -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$env:OFICONVERT_ACCENT = $Accent
try {
    $queue = New-SampleDocs

    Capture-Theme "Light" $queue
    Capture-Theme "Dark"  $queue

    Write-Host ""
    Ok "Capturas regeneradas en $OutputDir"
}
finally {
    Remove-Item Env:\OFICONVERT_ACCENT -ErrorAction SilentlyContinue
    Get-Process OfiConvert -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    if ($null -ne $backupSettings) { Set-Content -Path $settingsPath -Value $backupSettings -Encoding utf8 }
    elseif (Test-Path $settingsPath) { Remove-Item $settingsPath -Force }

    if ($null -ne $backupQueue) { Set-Content -Path $queuePath -Value $backupQueue -Encoding utf8 }
    elseif (Test-Path $queuePath) { Remove-Item $queuePath -Force }

    Remove-Item $sampleDir -Recurse -Force -ErrorAction SilentlyContinue
    Info "Restaurados settings.json y queue.json del usuario."
}
