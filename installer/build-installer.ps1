<#
.SYNOPSIS
    Publica OfiConvert (self-contained, win-x64) y compila el instalador con Inno Setup.

.DESCRIPTION
    1. Lee la versión del .csproj (o usa -Version).
    2. dotnet publish -c Release -r win-x64 --self-contained true  → a %TEMP% (ver MAX_PATH abajo).
    3. Compila OfiConvert.iss con ISCC, inyectando la versión y la carpeta del publish.
    4. Genera el .sha256 del instalador.

    El .sha256 NO es decorativo: es el asset con el que la app verificará la descarga antes de
    ejecutarla mientras los instaladores se publiquen sin firmar. release.ps1 lo sube como segundo
    asset del release y aborta si falta.

.PARAMETER Version
    Versión a estampar (por defecto: la del .csproj — que es la fuente única).

.PARAMETER CertThumbprint
    Huella (SHA-1) de un certificado de firma instalado en el almacén de Windows. Si se indica
    (o -CertFile), se firman el ejecutable publicado y el instalador con Authenticode.

.PARAMETER CertFile
    Ruta a un .pfx para firmar (alternativa a -CertThumbprint).

.PARAMETER CertPassword
    Contraseña del .pfx, como SecureString. Tambien se puede dejar en la variable de entorno
    OFICONVERT_CERT_PASSWORD, que es lo comodo para automatizar sin teclearla.

    NUNCA como [string]: se quedaria en ConsoleHost_history.txt y viajaria en claro entre scripts.

.PARAMETER TimestampUrl
    Servidor de sellado de tiempo RFC3161 (por defecto, el de DigiCert).

.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -Version 2.1.0
    .\build-installer.ps1 -Version 2.1.0 -CertThumbprint A1B2C3...
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64",
    [string]$CertThumbprint,
    [string]$CertFile,
    # SecureString y NO [string]: ver la nota de TJ-24 sobre Invoke-Sign.
    [SecureString]$CertPassword,
    [string]$TimestampUrl  = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

# Igual que en release.ps1: la contrasena puede llegar por entorno en vez de por la linea de comandos.
if (-not $CertPassword -and $env:OFICONVERT_CERT_PASSWORD) {
    $CertPassword = ConvertTo-SecureString $env:OFICONVERT_CERT_PASSWORD -AsPlainText -Force
}

# --- Firma de código (opcional) --------------------------------------------
$signEnabled = [bool]($CertThumbprint -or $CertFile)
$signtool = $null

function Find-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $fixed = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\signtool.exe",
        "$env:ProgramFiles\Windows Kits\10\App Certification Kit\signtool.exe",
        "${env:ProgramFiles(x86)}\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"
    )
    foreach ($f in $fixed) { if (Test-Path $f) { return $f } }

    $arch  = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }
    $bases = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin", "$env:ProgramFiles\Windows Kits\10\bin"
    )
    foreach ($b in $bases) {
        if (-not (Test-Path $b)) { continue }
        $found = Get-ChildItem $b -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "$arch\signtool.exe" } |
            Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($found) { return $found }
        $direct = Join-Path $b "$arch\signtool.exe"
        if (Test-Path $direct) { return $direct }
    }
    return $null
}

<#
.SYNOPSIS
    Firma los archivos indicados, SIN que la contrasena llegue nunca a una linea de comandos.
.NOTES
    TJ-24. Antes se hacia asi:

        signtool sign /f cert.pfx /p <CONTRASENA> ...

    La linea de comandos de un proceso la puede leer CUALQUIER proceso del equipo mientras dura
    (Get-CimInstance Win32_Process), sin permisos especiales. Y como el parametro era [string], la
    contrasena ademas se tecleaba en la consola -- quedandose en ConsoleHost_history.txt, en claro y
    para siempre -- y se reenviaba entre release.ps1 y este script igual de desnuda.

    Ahora, con un .pfx: se IMPORTA al almacen de certificados del usuario usando el SecureString (que
    Import-PfxCertificate acepta directamente, sin convertirlo a texto), se firma por HUELLA -- que no
    es secreta -- y se BORRA el certificado del almacen al terminar, pase lo que pase.

    Con -CertThumbprint no hay nada que hacer: nunca hubo contrasena.
#>
function Invoke-Sign([string[]]$files) {
    if (-not $signEnabled) { return }

    $base = @("sign", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256")
    $importado = $null

    try {
        if ($CertThumbprint) {
            $base += @("/sha1", $CertThumbprint)
        }
        elseif ($CertFile) {
            if (-not (Test-Path $CertFile)) { throw "No existe el certificado: $CertFile" }

            $importArgs = @{
                FilePath          = $CertFile
                CertStoreLocation = "Cert:\CurrentUser\My"
            }
            if ($CertPassword) { $importArgs.Password = $CertPassword }

            $importado = Import-PfxCertificate @importArgs
            if (-not $importado) { throw "No se pudo importar $CertFile al almacen de certificados." }

            # La huella no es secreta: identifica al certificado, no lo desbloquea.
            $base += @("/sha1", $importado.Thumbprint)
        }

        foreach ($f in $files) {
            if (-not (Test-Path $f)) { continue }
            Write-Host "==> Firmando: $f" -ForegroundColor Cyan
            & $signtool @base $f
            if ($LASTEXITCODE -ne 0) { throw "signtool falló al firmar $f (código $LASTEXITCODE)" }
        }
    }
    finally {
        # Se borra SIEMPRE. Dejar la clave privada en el almacen del usuario porque la firma fallo a
        # medias seria cambiar una fuga por otra.
        if ($importado) {
            $ruta = "Cert:\CurrentUser\My\$($importado.Thumbprint)"
            try { Remove-Item $ruta -DeleteKey -Force -ErrorAction Stop }
            catch { Write-Host "[!] No se pudo retirar el certificado importado ($ruta). Borralo a mano." -ForegroundColor Yellow }
        }
    }
}

if ($signEnabled) {
    $signtool = Find-SignTool
    if (-not $signtool) { throw "Se pidió firmar pero no se encontró signtool.exe. Instala el Windows SDK o añádelo al PATH." }
    Write-Host "==> Firma de código habilitada (signtool: $signtool)" -ForegroundColor Cyan
} else {
    Write-Warning "Firma de código DESHABILITADA (sin -CertThumbprint/-CertFile). El instalador NO estará firmado, asi que SmartScreen mostrará 'editor desconocido'. La verificación de las actualizaciones se apoyará en el .sha256 que se genera aquí y que release.ps1 sube como asset. Firmar sigue siendo lo deseable: es una garantía más fuerte que el hash."
}

# --- Rutas -----------------------------------------------------------------
$installerDir = $PSScriptRoot
$projectDir   = Split-Path $installerDir -Parent          # raíz del repo
$csproj       = Join-Path $projectDir "OfiConvert.csproj"

if (-not (Test-Path $csproj)) { throw "No se encontró el proyecto: $csproj" }

# --- Versión (fuente única: el .csproj) ------------------------------------
$csprojXml = [xml](Get-Content $csproj)
if (-not $Version) {
    $Version = ($csprojXml.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
    if (-not $Version) { throw "No hay <Version> en el .csproj y no se pasó -Version." }
}

# La publicación NO va dentro del repo, sino a una ruta corta y fija bajo %TEMP%.
#
# Motivo (heredado de FormatDiskPro, que lo pagó): Inno Setup no usa las APIs de rutas largas de
# Windows, así que no puede comprimir un archivo cuya ruta pase de MAX_PATH (260). El publish
# self-contained del Windows App SDK trae nombres larguísimos —el peor hoy es
# WindowsAppSdk.AppxDeploymentExtensions.Desktop-EventLog-Instrumentation.dll, 76 caracteres— y,
# sumados a la ruta del repo, se pasan del límite en cuanto el checkout no cuelga de una carpeta
# corta. ISCC entonces aborta con «El sistema no puede encontrar la ruta especificada», SIN decir de
# qué archivo habla. Publicando a %TEMP%, la ruta base baja a ~30 caracteres.
$publishDir = Join-Path $env:TEMP "OfiConvert-publish"

Write-Host "==> Versión: $Version" -ForegroundColor Cyan
Write-Host "==> Publicando en: $publishDir" -ForegroundColor Cyan

# --- Localizar ISCC --------------------------------------------------------
$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}
if (-not $iscc) { throw "No se encontró ISCC.exe. Instala Inno Setup 6: winget install JRSoftware.InnoSetup" }

# --- Publicar (self-contained) ---------------------------------------------
Write-Host "==> Publicando ($Configuration / $Runtime, self-contained)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

& dotnet publish $csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló (código $LASTEXITCODE)" }

# Guardas del publish. Los dos targets del .csproj (CopyXamlResourcesToPublish y CopyLangFilesToPublish)
# existen porque el tooling de WinUI 3 unpackaged NO copia estos archivos solo. Si algún día un cambio
# de SDK los rompe, la app se publicaría "bien" y luego CRASHEARÍA AL INICIAR en el equipo del usuario
# (sin el .pri, WinUI no resuelve el XAML) o abriría sin traducciones. Mejor romper el corte aquí.
$exePath = Join-Path $publishDir "OfiConvert.exe"
if (-not (Test-Path $exePath)) { throw "El publish no generó OfiConvert.exe: $exePath" }

$priPath = Join-Path $publishDir "OfiConvert.pri"
if (-not (Test-Path $priPath)) {
    throw "Falta OfiConvert.pri en el publish. Sin él, WinUI no puede resolver el XAML y la app CRASHEA al iniciar. Revisa el target CopyXamlResourcesToPublish del .csproj."
}

$langCount = @(Get-ChildItem (Join-Path $publishDir "Lang") -Filter *.xaml -ErrorAction SilentlyContinue).Count
if ($langCount -lt 8) {
    throw "El publish llevó $langCount archivos de idioma (se esperaban 8). Revisa el target CopyLangFilesToPublish del .csproj."
}
Write-Host "==> Publish verificado: OfiConvert.exe + .pri + $langCount idiomas" -ForegroundColor Cyan

# --- Firmar el ejecutable publicado (antes de empaquetar) ------------------
if ($signEnabled) {
    Invoke-Sign @($exePath, (Join-Path $publishDir "OfiConvert.dll"))
}

# --- Compilar instalador ---------------------------------------------------
$iss = Join-Path $installerDir "OfiConvert.iss"
Write-Host "==> Compilando instalador con Inno Setup..." -ForegroundColor Cyan
& $iscc "/DMyAppVersion=$Version" "/DPublishDir=$publishDir" $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC falló (código $LASTEXITCODE)" }

$setup = Join-Path $installerDir "Output\OfiConvert_Setup_$Version.exe"
if (-not (Test-Path $setup)) { throw "ISCC terminó pero no se encontró el instalador esperado: $setup" }

# Firmar el instalador (lo que comprueban SmartScreen y la verificación Authenticode del updater).
if ($signEnabled) { Invoke-Sign @($setup) }

# SHA-256 del instalador YA FIRMADO: firmar cambia el binario, así que el hash va DESPUÉS.
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
$sha256File = "$setup.sha256"
"$hash *$(Split-Path $setup -Leaf)" | Out-File -FilePath $sha256File -Encoding ascii -NoNewline
Write-Host "==> SHA-256: $hash" -ForegroundColor Cyan

$sizeMB = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host ""
Write-Host "[OK] Instalador generado: $setup ($sizeMB MB)" -ForegroundColor Green
Write-Host "[OK] Checksum generado:   $sha256File" -ForegroundColor Green
