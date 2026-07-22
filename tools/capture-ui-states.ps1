<#
.SYNOPSIS
    Galería de revisión de UI: captura TODOS los estados de la app (no solo los dos del README)
    conduciendo el .exe REAL por UI Automation, en tema claro y oscuro, con un acento NEUTRO.

.DESCRIPTION
    capture-screenshots.ps1 genera las 4 imágenes curadas del README. Este script es su primo para
    REVISAR el diseño: siembra cada estado y lo fotografía, para poder mirar la app entera de un vistazo
    antes de refinarla.

    Estados cubiertos (× claro/oscuro):
      · main-empty          Cola vacía (lo primero que ve un usuario nuevo)
      · main-queue          Cola con documentos
      · history-empty       Historial vacío
      · history             Historial con conversiones (éxitos y un fallo)
      · settings-appearance Ajustes, arriba (tema / idioma)
      · settings-about      Ajustes, abajo (Acerca de: licencia y avisos)
      · dialog-license      El diálogo legal de la Licencia, abierto

    Hereda las tres trampas ya resueltas de capture-screenshots.ps1:
      1. RESPALDA Y RESTAURA los datos reales del usuario (settings/queue/history viven en %AppData% y la
         app es unpackaged: escribe donde la instalación de verdad).
      2. SIEMBRA el estado por JSON (la cola y el historial son simples archivos que la app lee al arrancar).
      3. Captura la PANTALLA con DWMWA_EXTENDED_FRAME_BOUNDS (un WinUI con Mica no se deja capturar por
         PrintWindow, y GetWindowRect mete la sombra invisible del marco).

    ACENTO NEUTRO: la app respeta el acento de Windows, así que en un equipo con acento rojo las capturas
    salen rojas. Aquí se fija OFICONVERT_ACCENT (que App.OnLaunched solo lee para esto) para que las
    imágenes muestren la experiencia por defecto, no la personal de quien las genera.

    NO requiere elevación ni Office: no se convierte nada, todo el estado se siembra.

.PARAMETER OutputDir
    Carpeta de salida. Por defecto %TEMP%\OfiConvert-ui-gallery.

.PARAMETER ExePath
    Ruta al OfiConvert.exe. Por defecto, el más reciente de bin\ (win-x64).

.PARAMETER Accent
    Color de acento neutro en hex. Por defecto #0078D4 (azul por defecto de Windows).

.EXAMPLE
    .\tools\capture-ui-states.ps1
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

    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int SW_RESTORE = 9;
}
'@

# ── Rutas ──────────────────────────────────────────────────────────────────
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $env:TEMP "OfiConvert-ui-gallery" }

$dataDir      = Join-Path $env:APPDATA "OfiConvert"
$settingsPath = Join-Path $dataDir "settings.json"
$queuePath    = Join-Path $dataDir "queue.json"
$historyPath  = Join-Path $dataDir "history.json"
$sampleDir    = Join-Path $env:TEMP "OfiConvert-gallery-docs"

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
Info "Acento neutro: $Accent"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# ── Estado sembrado ─────────────────────────────────────────────────────────
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

# Historial de ejemplo. Format es el enum OutputFormat serializado como número (0=PDF,1=HTML,2=CSV,3=PNG,
# 4=JPG). Timestamp va como cadena ISO: System.Text.Json la lee a DateTime sin líos. Se mete UN fallo a
# propósito, para ver cómo (o si) la lista distingue un error de un éxito.
function New-SampleHistory {
    $out = "C:\Users\Usuario\Documentos\Convertidos"
    return @(
        [ordered]@{ Id="h1"; Timestamp="2026-07-20T14:32:11"; SourcePath="C:\docs\Informe anual.docx";       SourceFileName="Informe anual.docx";      OutputPath="$out\Informe anual.pdf";      Format=0; Success=$true;  ErrorMessage=$null;                              DurationSeconds=2.4; FileSizeBytes=348160 }
        [ordered]@{ Id="h2"; Timestamp="2026-07-20T14:31:03"; SourcePath="C:\docs\Presupuesto 2026.xlsx";     SourceFileName="Presupuesto 2026.xlsx";   OutputPath="$out\Presupuesto 2026.csv";   Format=2; Success=$true;  ErrorMessage=$null;                              DurationSeconds=0.8; FileSizeBytes=131072 }
        [ordered]@{ Id="h3"; Timestamp="2026-07-20T14:29:47"; SourcePath="C:\docs\Presentacion ventas.pptx";  SourceFileName="Presentacion ventas.pptx";OutputPath="$out\Presentacion ventas";    Format=3; Success=$true;  ErrorMessage=$null;                              DurationSeconds=5.1; FileSizeBytes=911360 }
        [ordered]@{ Id="h4"; Timestamp="2026-07-20T14:27:12"; SourcePath="C:\docs\Contrato firmado.docx";     SourceFileName="Contrato firmado.docx";   OutputPath="";                            Format=0; Success=$false; ErrorMessage="El archivo está protegido con contraseña."; DurationSeconds=0.3; FileSizeBytes=51200 }
        [ordered]@{ Id="h5"; Timestamp="2026-07-20T14:24:55"; SourcePath="C:\docs\Balance Q2.xlsx";           SourceFileName="Balance Q2.xlsx";         OutputPath="$out\Balance Q2.pdf";         Format=0; Success=$true;  ErrorMessage=$null;                              DurationSeconds=1.2; FileSizeBytes=98304 }
    )
}

function Write-JsonArray($items, [string]$path) {
    if ($null -eq $items -or @($items).Count -eq 0) {
        Set-Content -Path $path -Value "[]" -Encoding utf8
        return
    }
    # @(...) fuerza que un solo elemento no se serialice como escalar.
    $json = ConvertTo-Json -InputObject @($items) -Depth 6
    if ($json -notmatch '^\s*\[') { $json = "[$json]" }   # ConvertTo-Json desenvuelve arrays de 1 elemento
    Set-Content -Path $path -Value $json -Encoding utf8
}

function Set-CaptureState([string]$theme, [string[]]$queue, $history) {
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
    Write-JsonArray $queue   $queuePath
    Write-JsonArray $history $historyPath
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

# El Pivot de WinUI DESCARGA la pestaña que no está delante: hay que seleccionarla antes de capturarla.
function Select-Tab($window, [string]$tabId) {
    $tab = Find-ById $window $tabId
    if (-not $tab) { Die "No se encontró la pestaña '$tabId'." }
    $pattern = $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $pattern.Select()
    Start-Sleep -Milliseconds 900
}

# Lleva un control a la vista dentro de su ScrollViewer (la pestaña de Ajustes es más alta que la ventana).
function Show-Element($window, [string]$automationId) {
    $element = Find-ById $window $automationId
    if (-not $element) { Warn "No se encontró '$automationId' para desplazarlo a la vista."; return }
    try {
        $pattern = $element.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern)
        $pattern.ScrollIntoView()
        Start-Sleep -Milliseconds 700
    }
    catch { Warn "'$automationId' no admite ScrollItemPattern: se captura sin desplazar." }
}

function Invoke-Element($window, [string]$automationId) {
    $element = Find-ById $window $automationId
    if (-not $element) { Warn "No se encontró '$automationId' para invocarlo."; return $false }
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
    return $true
}

function Save-WindowPng([IntPtr]$hwnd, [string]$path) {
    [void][Win32Capture]::ShowWindow($hwnd, [Win32Capture]::SW_RESTORE)
    [void][Win32Capture]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 700

    $rect = New-Object Win32Capture+RECT
    $size = [System.Runtime.InteropServices.Marshal]::SizeOf([type]([Win32Capture+RECT]))
    [void][Win32Capture]::DwmGetWindowAttribute($hwnd, [Win32Capture]::DWMWA_EXTENDED_FRAME_BOUNDS, [ref]$rect, $size)

    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $h -le 0) { Die "La ventana devolvió un rectángulo vacío ($w x $h)." }

    $bmp = New-Object System.Drawing.Bitmap $w, $h
    try {
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        try { $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $w, $h)) }
        finally { $g.Dispose() }
        $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bmp.Dispose() }

    Ok "$([System.IO.Path]::GetFileName($path))  ($w x $h)"
}

# ── Captura por tema ────────────────────────────────────────────────────────
function Capture-Theme([string]$theme, [string[]]$queue, $history) {
    $suffix = $theme.ToLowerInvariant()
    Info "Tema $theme..."

    # --- Lanzamiento A: con cola y con historial ---
    Set-CaptureState $theme $queue $history
    $proc = Start-Process $ExePath -PassThru
    try {
        $window = Find-MainWindow $proc.Id
        Start-Sleep -Seconds 2   # la cola se rellena y las miniaturas del shell tardan un instante
        $hwnd = [IntPtr]$window.Current.NativeWindowHandle

        Save-WindowPng $hwnd (Join-Path $OutputDir "main-queue-$suffix.png")

        Select-Tab $window "tabHistory"
        Save-WindowPng $hwnd (Join-Path $OutputDir "history-$suffix.png")

        Select-Tab $window "tabSettings"
        Show-Element $window "cmbTheme"      # arriba: apariencia (tema / idioma)
        Save-WindowPng $hwnd (Join-Path $OutputDir "settings-appearance-$suffix.png")

        Show-Element $window "btnLicencia"   # abajo: acerca de (licencia / avisos)
        Save-WindowPng $hwnd (Join-Path $OutputDir "settings-about-$suffix.png")

        if (Invoke-Element $window "btnLicencia") {
            Start-Sleep -Milliseconds 1100   # el ContentDialog aparece y se asienta
            Save-WindowPng $hwnd (Join-Path $OutputDir "dialog-license-$suffix.png")
        }
    }
    finally {
        if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Milliseconds 500
    }

    # --- Lanzamiento B: vacío (cola e historial) ---
    Set-CaptureState $theme @() @()
    $proc = Start-Process $ExePath -PassThru
    try {
        $window = Find-MainWindow $proc.Id
        Start-Sleep -Seconds 2
        $hwnd = [IntPtr]$window.Current.NativeWindowHandle

        Save-WindowPng $hwnd (Join-Path $OutputDir "main-empty-$suffix.png")

        Select-Tab $window "tabHistory"
        Save-WindowPng $hwnd (Join-Path $OutputDir "history-empty-$suffix.png")
    }
    finally {
        if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Milliseconds 500
    }
}

# ── Ejecución ──────────────────────────────────────────────────────────────
$backupSettings = if (Test-Path $settingsPath) { Get-Content $settingsPath -Raw } else { $null }
$backupQueue    = if (Test-Path $queuePath)    { Get-Content $queuePath -Raw }    else { $null }
$backupHistory  = if (Test-Path $historyPath)  { Get-Content $historyPath -Raw }  else { $null }

Get-Process OfiConvert -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$env:OFICONVERT_ACCENT = $Accent
try {
    $queue   = New-SampleDocs
    $history = New-SampleHistory

    Capture-Theme "Light" $queue $history
    Capture-Theme "Dark"  $queue $history

    Write-Host ""
    Ok "Galería generada en $OutputDir"
}
finally {
    Remove-Item Env:\OFICONVERT_ACCENT -ErrorAction SilentlyContinue
    Get-Process OfiConvert -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    if ($null -ne $backupSettings) { Set-Content -Path $settingsPath -Value $backupSettings -Encoding utf8 }
    elseif (Test-Path $settingsPath) { Remove-Item $settingsPath -Force }

    if ($null -ne $backupQueue) { Set-Content -Path $queuePath -Value $backupQueue -Encoding utf8 }
    elseif (Test-Path $queuePath) { Remove-Item $queuePath -Force }

    if ($null -ne $backupHistory) { Set-Content -Path $historyPath -Value $backupHistory -Encoding utf8 }
    elseif (Test-Path $historyPath) { Remove-Item $historyPath -Force }

    Remove-Item $sampleDir -Recurse -Force -ErrorAction SilentlyContinue
    Info "Restaurados settings.json, queue.json e history.json del usuario."
}
