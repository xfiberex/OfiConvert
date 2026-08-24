<#
.SYNOPSIS
    Fotografía los desplegables ABIERTOS de la app y COMPRUEBA que su fondo es opaco (no acrílico),
    contando colores. En claro y oscuro, con acento neutro.

.DESCRIPTION
    Tercer primo de capture-screenshots.ps1 (README) y capture-ui-states.ps1 (galería de revisión).
    Aquellos fotografían la ventana; este abre cada ComboBox por UI Automation y MIDE el popup.

    POR QUÉ EXISTE (2026-08-24): los popups de WinUI son ACRÍLICOS por defecto —generic.xaml alias
    `ComboBoxDropDownBackground` → `AcrylicInAppFillColorDefaultBrush`—, y sobre el backdrop Mica de la
    ventana se ven BORROSOS: transparentan la tarjeta de debajo y encima pintan la textura de RUIDO del
    acrílico. Se arregló forzándolos opacos en App.xaml... salvo que el primer intento NO HIZO NADA (las
    ThemeDictionaries iban en la raíz de Application.Resources, que ya tenía MergedDictionaries: se ignoran
    en silencio, sin una sola advertencia). El desplegable salía idéntico y el build seguía en 0/0.

    De ahí la parte de MEDIR, que es lo que este script aporta sobre una captura a ojo:

      · Acrílico → el fondo es RUIDO: decenas de valores vecinos (#2B2B2B…#303030).
      · Sólido   → UN valor, exactamente el que se puso en App.xaml.

    La métrica es la "cuota de ruido": el % de píxeles del fondo cuyo color queda a ±3 del dominante SIN
    ser el dominante. Con acrílico se dispara (~66%); con un SolidColorBrush es 0. El resalte del elemento
    seleccionado no la contamina: está mucho más lejos de ±3, solo resta dominancia.

    Hereda las trampas ya resueltas de sus primos:
      1. RESPALDA Y RESTAURA settings.json (la app es unpackaged: escribe en el %AppData% de verdad).
      2. Fija OFICONVERT_ACCENT para no capturar el acento personal de quien lo ejecuta.
      3. Captura con DWMWA_EXTENDED_FRAME_BOUNDS (un WinUI con Mica no se deja capturar por PrintWindow).

    Y una suya: el popup NO está dentro del árbol visual de la ventana, así que su rectángulo se saca del
    propio elemento de UI Automation (BoundingRectangle), no del de la ventana. Se captura de PANTALLA.

    NO requiere elevación ni Office: no se convierte nada.

.PARAMETER OutputDir
    Carpeta de salida. Por defecto %TEMP%\OfiConvert-dropdowns.

.PARAMETER ExePath
    Ruta al OfiConvert.exe. Por defecto, el más reciente de bin\ (win-x64).
    OJO: "el más reciente" puede ser el Debug de un `dotnet run` viejo. Compila antes.

.PARAMETER Accent
    Color de acento neutro en hex. Por defecto #0078D4 (azul por defecto de Windows).

.EXAMPLE
    .\tools\capture-dropdown.ps1
    Comprueba los cuatro ComboBox en claro y oscuro. Sale con código 1 si alguno sigue acrílico.
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
function Bad($m)  { Write-Host "[X] $m" -ForegroundColor Red }
function Die($m)  { Write-Host "[X] $m" -ForegroundColor Red; exit 1 }

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class Win32Dropdown
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
if (-not $OutputDir) { $OutputDir = Join-Path $env:TEMP "OfiConvert-dropdowns" }

$dataDir      = Join-Path $env:APPDATA "OfiConvert"
$settingsPath = Join-Path $dataDir "settings.json"

if (-not $ExePath) {
    $candidate = Get-ChildItem (Join-Path $root "bin") -Filter "OfiConvert.exe" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*\win-x64\*" } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if (-not $candidate) { Die "No se encontró OfiConvert.exe. Compila primero: dotnet build OfiConvert.slnx -c Release" }
    $ExePath = $candidate.FullName
}
if (-not (Test-Path $ExePath)) { Die "No existe el ejecutable: $ExePath" }

$exeInfo = Get-Item $ExePath
Info "Ejecutable: $($exeInfo.FullName)"
Info "Compilado:  $($exeInfo.LastWriteTime)   <- si esto es viejo, estás midiendo un binario viejo"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Los cuatro ComboBox de la app, con la pestaña en la que vive cada uno.
$targets = @(
    [pscustomobject]@{ Tab = "tabConversion"; Id = "cmbFormat";         Label = "formato (conversión)" }
    [pscustomobject]@{ Tab = "tabSettings";   Id = "cmbTheme";          Label = "tema" }
    [pscustomobject]@{ Tab = "tabSettings";   Id = "cmbLanguage";       Label = "idioma" }
    [pscustomobject]@{ Tab = "tabSettings";   Id = "cmbDefaultFormat";  Label = "formato predeterminado" }
)

# ── Estado sembrado ─────────────────────────────────────────────────────────
function Set-CaptureTheme([string]$theme) {
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
    [ordered]@{
        Theme                  = $theme      # "Light" | "Dark"
        Language               = "es"
        MaxParallelConversions = 2
        AutoRetryEnabled       = $true
        MaxRetryCount          = 3
        MinimizeToTray         = $false
        ShowNotifications      = $true
        LastOutputFolder       = ""
        DefaultOutputFormat    = 0           # PDF
    } | ConvertTo-Json | Set-Content -Path $settingsPath -Encoding utf8
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

function Find-ById($parent, [string]$automationId, [int]$timeoutSec = 15) {
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

# El Pivot de WinUI DESCARGA la pestaña que no está delante: hay que seleccionarla antes de tocarla.
function Select-Tab($window, [string]$tabId) {
    $tab = Find-ById $window $tabId
    if (-not $tab) { Die "No se encontró la pestaña '$tabId'." }
    $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 900
}

function Show-Element($element) {
    try {
        $element.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern).ScrollIntoView()
        Start-Sleep -Milliseconds 700
    }
    catch { }   # no todos admiten ScrollItemPattern; si ya se ve, da igual
}

# ── Captura y medida ────────────────────────────────────────────────────────
function Get-ScreenBitmap([int]$x, [int]$y, [int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try { $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $w, $h)) }
    finally { $g.Dispose() }
    return $bmp
}

<#
    Mide la "cuota de ruido" del fondo del popup.

    Se muestrea una banda vertical del LADO DERECHO del popup (el texto de los elementos va alineado a la
    izquierda, así que ahí solo hay fondo). De esa banda se saca el color DOMINANTE y se cuenta qué % de
    píxeles cae a ±3 de él sin ser él: eso es exactamente el ruido del acrílico. Un SolidColorBrush da 0.
#>
function Measure-PopupBackground($bmp) {
    $x0 = [int]($bmp.Width * 0.62)
    $x1 = [int]($bmp.Width - 8)
    $y0 = [int]($bmp.Height * 0.15)
    $y1 = [int]($bmp.Height * 0.85)
    if ($x1 -le $x0 -or $y1 -le $y0) { return $null }

    $counts = @{}
    for ($x = $x0; $x -lt $x1; $x++) {
        for ($y = $y0; $y -lt $y1; $y++) {
            $c = $bmp.GetPixel($x, $y)
            $key = "{0},{1},{2}" -f $c.R, $c.G, $c.B
            $counts[$key] = $counts[$key] + 1
        }
    }

    $total = ($counts.Values | Measure-Object -Sum).Sum
    $top = $counts.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1
    $dom = $top.Key -split ","

    $noise = 0
    foreach ($entry in $counts.GetEnumerator()) {
        if ($entry.Key -eq $top.Key) { continue }
        $c = $entry.Key -split ","
        if ([Math]::Abs([int]$c[0] - [int]$dom[0]) -le 3 -and
            [Math]::Abs([int]$c[1] - [int]$dom[1]) -le 3 -and
            [Math]::Abs([int]$c[2] - [int]$dom[2]) -le 3) {
            $noise += $entry.Value
        }
    }

    return [pscustomobject]@{
        Dominant     = "#{0:X2}{1:X2}{2:X2}" -f [int]$dom[0], [int]$dom[1], [int]$dom[2]
        DominantPct  = [Math]::Round(100.0 * $top.Value / $total, 1)
        NoisePct     = [Math]::Round(100.0 * $noise / $total, 1)
        DistinctCount= $counts.Count
        Pixels       = $total
    }
}

function Test-Dropdown($window, [IntPtr]$hwnd, $target, [string]$suffix) {
    $cmb = Find-ById $window $target.Id
    if (-not $cmb) { Warn "No se encontró '$($target.Id)': se omite."; return $null }

    Show-Element $cmb
    [void][Win32Dropdown]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 300

    $expand = $cmb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    Start-Sleep -Milliseconds 1100

    # El popup NO cuelga de la ventana, y ADEMÁS no expone ningún elemento `List`: en el árbol de UIA solo
    # aparecen sus `ListItem` sueltos (y por duplicado: los del ComboBox y los del popup). Así que el
    # rectángulo del desplegable se reconstruye como la UNIÓN de los rects de sus elementos.
    #
    # NADA DE FALLBACK AL PROPIO ComboBox SI NO APARECEN. Se intentó y da un OK FALSO: el ComboBox cerrado
    # tiene fondo sólido (#383838 en oscuro), así que la medida salía limpia y el script decía "opaco"
    # sin haber mirado el popup ni una vez. Si no hay elementos, esto FALLA y lo dice.
    $itemCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)

    $left = $null; $top = $null; $right = $null; $bottom = $null
    $deadline = (Get-Date).AddSeconds(4)
    while ($null -eq $left -and (Get-Date) -lt $deadline) {
        foreach ($item in $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $itemCond)) {
            $b = $item.Current.BoundingRectangle
            if ($b.Width -le 0 -or $b.Height -le 0) { continue }
            if ($null -eq $left) { $left = $b.Left; $top = $b.Top; $right = $b.Right; $bottom = $b.Bottom; continue }
            if ($b.Left   -lt $left)   { $left   = $b.Left }
            if ($b.Top    -lt $top)    { $top    = $b.Top }
            if ($b.Right  -gt $right)  { $right  = $b.Right }
            if ($b.Bottom -gt $bottom) { $bottom = $b.Bottom }
        }
        if ($null -eq $left) { Start-Sleep -Milliseconds 250 }
    }
    if ($null -eq $left) {
        $expand.Collapse()
        Bad "El popup de '$($target.Id)' no expuso sus elementos: no se pudo medir."
        return "no-medido"
    }

    $r = [pscustomobject]@{ X = $left; Y = $top; Width = $right - $left; Height = $bottom - $top }
    $cr = $cmb.Current.BoundingRectangle

    # El popup de un ComboBox con 3+ elementos es MÁS ALTO que el control. Si no lo es, o no llegó a
    # abrirse, o se están midiendo los ListItem del control cerrado: la medida no vale.
    if ($r.Height -le $cr.Height + 4) {
        $expand.Collapse()
        Bad "El popup de '$($target.Id)' mide lo mismo que el control ($([int]$r.Height)px): no se abrió."
        return "no-medido"
    }

    $result = $null
    if ($r.Width -ge 8 -and $r.Height -ge 8) {
        $bmp = Get-ScreenBitmap ([int]$r.X) ([int]$r.Y) ([int]$r.Width) ([int]$r.Height)
        try {
            $bmp.Save((Join-Path $OutputDir "$($target.Id)-$suffix.png"), [System.Drawing.Imaging.ImageFormat]::Png)
            $result = Measure-PopupBackground $bmp
        }
        finally { $bmp.Dispose() }
    }
    else { Warn "El popup de '$($target.Id)' devolvió un rectángulo vacío." }

    $expand.Collapse()
    Start-Sleep -Milliseconds 500
    return $result
}

# ── Ejecución ──────────────────────────────────────────────────────────────
$backupSettings = if (Test-Path $settingsPath) { Get-Content $settingsPath -Raw } else { $null }
Get-Process OfiConvert -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$env:OFICONVERT_ACCENT = $Accent
$failures = @()
try {
    foreach ($theme in @("Light", "Dark")) {
        $suffix = $theme.ToLowerInvariant()
        Info "Tema $theme..."
        Set-CaptureTheme $theme

        $proc = Start-Process $ExePath -PassThru
        try {
            $window = Find-MainWindow $proc.Id
            Start-Sleep -Seconds 2
            $hwnd = [IntPtr]$window.Current.NativeWindowHandle
            [void][Win32Dropdown]::ShowWindow($hwnd, [Win32Dropdown]::SW_RESTORE)

            $currentTab = ""
            foreach ($t in $targets) {
                if ($t.Tab -ne $currentTab) { Select-Tab $window $t.Tab; $currentTab = $t.Tab }

                $m = Test-Dropdown $window $hwnd $t $suffix
                if ($m -is [string] -and $m -eq "no-medido") {
                    $failures += "$($t.Label) [$theme]: no se pudo medir el popup"
                    continue
                }
                if (-not $m) { continue }

                $line = "{0,-24} fondo {1}  dominante {2,5}%  ruido {3,5}%  ({4} colores)" -f
                    $t.Label, $m.Dominant, $m.DominantPct, $m.NoisePct, $m.DistinctCount
                if ($m.NoisePct -gt 5) {
                    Bad "$line  <- ACRILICO"
                    $failures += "$($t.Label) [$theme]: ruido $($m.NoisePct)%"
                }
                else { Ok $line }
            }
        }
        finally {
            if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
            Start-Sleep -Milliseconds 500
        }
    }

    Write-Host ""
    Info "Capturas en $OutputDir"
    if ($failures.Count -gt 0) {
        Bad "Hay desplegables ACRILICOS (el fondo es ruido, no un color):"
        $failures | ForEach-Object { Write-Host "     - $_" -ForegroundColor Red }
        Write-Host "     Revisa App.xaml: las ThemeDictionaries van DENTRO de MergedDictionaries," -ForegroundColor Yellow
        Write-Host "     despues de XamlControlsResources. En la raiz se ignoran EN SILENCIO." -ForegroundColor Yellow
        exit 1
    }
    Ok "Todos los desplegables son opacos."
}
finally {
    Remove-Item Env:\OFICONVERT_ACCENT -ErrorAction SilentlyContinue
    Get-Process OfiConvert -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    if ($null -ne $backupSettings) { Set-Content -Path $settingsPath -Value $backupSettings -Encoding utf8 }
    elseif (Test-Path $settingsPath) { Remove-Item $settingsPath -Force }
    Info "Restaurado settings.json del usuario."
}
