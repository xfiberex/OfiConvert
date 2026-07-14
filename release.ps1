<#
.SYNOPSIS
    Corta una versión de OfiConvert de principio a fin.

.DESCRIPTION
    Flujo completo en un paso:
      1. Valida la versión, el árbol de trabajo y que el tag no exista ya.
      2. Compila y ejecuta las pruebas (si las hay; ver -SkipTests).
      3. Sube <Version>, <AssemblyVersion> y <FileVersion> en el .csproj.
      4. Compila el instalador (publish self-contained + Inno Setup) y su .sha256.
      5. Commit del bump + tag anotado vX.Y.Z.
      6. Push de la rama y el tag a origin.
      7. Crea el GitHub Release adjuntando el instalador Y su .sha256.

    LAS TRES ETIQUETAS DE VERSIÓN, no solo <Version>: el updater compara el tag del release contra
    Assembly.GetExecutingAssembly().GetName().Version, que sale de <AssemblyVersion>. Si esa se queda
    atrás, la app publicada se cree más vieja de lo que es y se ofrece a sí misma la actualización que
    ya tiene, en bucle. (WingetUSoft lo cazó antes de su primer corte.)

    EL ASSET .sha256 ES OBLIGATORIO, y desde el Tier C (v2.2.0) no es una precaución: es un REQUISITO DE
    FUNCIONAMIENTO. La app verifica el instalador antes de ejecutarlo (Authenticode -> SHA-256) y, sin
    ninguna de las dos cosas, BORRA la descarga y ABORTA. Un release sin .sha256 y sin firmar sería un
    release que TODOS los clientes rechazan. Por eso este script aborta si el hash no está.

.PARAMETER Version
    Versión a publicar (X.Y.Z). Si se omite, usa la del .csproj.

.PARAMETER NotesFile
    Archivo Markdown con las notas del release. Si se omite, se genera una plantilla.

.PARAMETER SkipTests
    Omite la compilación y las pruebas. Desde el Tier D hay 170 (unitarias + UI): usarlo es renunciar a
    la única red de seguridad del corte.

.PARAMETER AllowDirty
    Continúa aunque haya archivos nuevos sin rastrear.

.PARAMETER DryRun
    Valida, compila el instalador y muestra el plan, pero NO toca git ni GitHub.

.EXAMPLE
    .\release.ps1 -Version 2.1.0 -DryRun
    .\release.ps1 -Version 2.1.0
    .\release.ps1 -Version 2.1.0 -NotesFile notas.md
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$NotesFile,
    [switch]$SkipTests,
    [switch]$AllowDirty,
    [switch]$DryRun,
    # Firma de código (opcional): se reenvían a build-installer.ps1.
    [string]$CertThumbprint,
    [string]$CertFile,
    [string]$CertPassword,
    [string]$TimestampUrl
)

$ErrorActionPreference = "Stop"

function Info($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "[OK] $m" -ForegroundColor Green }
function Warn($m) { Write-Host "[!] $m" -ForegroundColor Yellow }
function Die($m)  { Write-Host "[X] $m" -ForegroundColor Red; exit 1 }

<#
.SYNOPSIS
    Ejecuta git de forma segura aunque la salida del script esté capturada. Devuelve el código de salida.

.DESCRIPTION
    git escribe por stderr en su operación NORMAL, sin que nada haya fallado: el resumen del push
    ("To https://github.com/..."), los avisos de finales de línea ("LF will be replaced by CRLF")...

    Lanzado a pelo eso es inocuo. PERO si alguien captura la salida (`| Tee-Object release.log`, un
    `2>&1 |`, un wrapper que la recoja), Windows PowerShell 5.1 convierte cada línea de stderr de un exe
    nativo en un NativeCommandError y, con $ErrorActionPreference = "Stop", ABORTA el script aunque git
    haya devuelto 0.

    En un `git push` eso es especialmente malo: el script muere DESPUÉS de empujar la rama y deja el
    release a medias (rama subida, sin tag ni GitHub Release). Les pasó a los dos proyectos hermanos.

    Aquí se baja la preferencia solo mientras corre git y se decide por $LASTEXITCODE, que es el único
    indicador fiable de si git falló.
#>
function Invoke-Git {
    $eap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & git @args 2>&1 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
        return $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $eap }
}

# ── Rutas ──────────────────────────────────────────────────────────────────
$root        = $PSScriptRoot
$csproj      = Join-Path $root "OfiConvert.csproj"
$solution    = Join-Path $root "OfiConvert.slnx"
$buildScript = Join-Path $root "installer\build-installer.ps1"
$outputDir   = Join-Path $root "installer\Output"
$testsDir    = Join-Path $root "tests"

if (-not (Test-Path $csproj))      { Die "No se encontró el proyecto: $csproj" }
if (-not (Test-Path $buildScript)) { Die "No se encontró el script del instalador: $buildScript" }

# ── Versión ────────────────────────────────────────────────────────────────
# OJO con la codificación: NO usar `Get-Content -Raw`. En PS 5.1 lee con la página de códigos ANSI del
# sistema, así que los bytes UTF-8 de un acento (é = C3 A9) se leen como dos caracteres (Ã©) y, al
# reescribir el archivo, la corrupción queda GRABADA. Como el bump ocurre en CADA release, el daño se
# acumula capa sobre capa: a FormatDiskPro le destrozó el nombre del autor en <Authors>/<Copyright>
# —y, de ahí, en las propiedades del .exe publicado— a lo largo de 14 versiones, sin que nadie lo viera.
# ReadAllText detecta el BOM (y asume UTF-8 si no lo hay); se reescribe CONSERVÁNDOLO.
$csprojRaw = [System.IO.File]::ReadAllText($csproj)
$currentVersion = $null
if ($csprojRaw -match '<Version>(.*?)</Version>') { $currentVersion = $Matches[1] }

if (-not $Version) {
    if (-not $currentVersion) { Die "No hay <Version> en el .csproj y no se pasó -Version." }
    $Version = $currentVersion
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Die "Versión inválida '$Version'. Usa el formato X.Y.Z (p. ej. 2.1.0)."
}
$tag = "v$Version"
Info "Versión a publicar: $Version  (tag $tag)"
if ($currentVersion -and $currentVersion -ne $Version) {
    Info "Bump de versión: $currentVersion -> $Version"
}

Push-Location $root
try {
    # ── Validaciones de git ──────────────────────────────────────────────────
    & git rev-parse --is-inside-work-tree *> $null
    if ($LASTEXITCODE -ne 0) { Die "Este directorio no es un repositorio git." }

    $branch = (& git rev-parse --abbrev-ref HEAD).Trim()
    Info "Rama: $branch"

    if (& git tag --list $tag) { Die "El tag $tag ya existe localmente. Usa otra versión o bórralo antes." }
    if (& git ls-remote --tags origin $tag 2>$null) { Die "El tag $tag ya existe en origin. Usa otra versión." }

    # Los archivos NUEVOS sin rastrear no entran en el commit del release (solo se hace `git add -u`),
    # así que un release podría salir sin ellos. Se avisa y se aborta salvo -AllowDirty.
    $untracked = (& git status --porcelain) | Where-Object { $_ -match '^\?\?' }
    if ($untracked -and -not $AllowDirty) {
        Warn "Hay archivos nuevos sin rastrear (NO se incluirían en el release):"
        $untracked | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
        Die "Añádelos con 'git add <archivo>' y reintenta, o usa -AllowDirty para ignorarlos a propósito."
    } elseif ($untracked) {
        Warn "Archivos sin rastrear ignorados (-AllowDirty):"
        $untracked | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    }

    # ── Compilación y pruebas ────────────────────────────────────────────────
    if ($SkipTests) {
        Warn "Compilación y pruebas omitidas (-SkipTests)."
    } else {
        Info "Compilando ($solution)..."
        & dotnet build $solution -c Release --nologo
        if ($LASTEXITCODE -ne 0) { Die "La compilación falló. Release abortado." }
        Ok "Compilación correcta."

        # Todo .csproj bajo tests\ se ejecuta, sin listarlos aquí: hoy son OfiConvert.Tests (unitarias) y
        # OfiConvert.UiTests (FlaUI, Tier D).
        #
        # OJO: los UI tests ARRANCAN LA APP de verdad y la conducen — necesitan un escritorio interactivo,
        # y por eso este script corre en la máquina del desarrollador y no en un runner de CI (ver
        # ROADMAP, "Decisiones cerradas"). Verán aparecer y desaparecer la ventana unos segundos: es
        # normal. No necesitan Office ni LibreOffice instalado: ninguno convierte un archivo.
        $testProjects = @(Get-ChildItem $testsDir -Filter *.csproj -Recurse -ErrorAction SilentlyContinue)
        if ($testProjects.Count -gt 0) {
            foreach ($proj in $testProjects) {
                Info "Ejecutando pruebas: $($proj.Name)"
                & dotnet test $proj.FullName --nologo
                if ($LASTEXITCODE -ne 0) { Die "Las pruebas de $($proj.Name) fallaron. Release abortado." }
            }
            Ok "Pruebas correctas."
        } else {
            Warn "NO se encontró ningún proyecto de pruebas bajo tests\: este release saldría sin ninguna prueba automatizada. Solo se ha comprobado que compila."
        }
    }

    # ── Notas del release ────────────────────────────────────────────────────
    $notesPath = $NotesFile
    $tempNotes = $null
    if (-not $notesPath) {
        $tempNotes = Join-Path $env:TEMP "oficonvert_release_$Version.md"
        @(
            "## OfiConvert v$Version",
            "",
            "Instalador self-contained para Windows x64 (no requiere instalar .NET).",
            "",
            "Descarga ``OfiConvert_Setup_$Version.exe`` y ejecútalo. Se instala para el usuario actual,",
            "sin pedir permisos de administrador.",
            "",
            "Requiere **Microsoft Office** de escritorio o **LibreOffice** para convertir.",
            "",
            "El asset ``OfiConvert_Setup_$Version.exe.sha256`` es el hash SHA-256 del instalador: sirve para",
            "comprobar que la descarga es íntegra."
        ) | Out-File -FilePath $tempNotes -Encoding utf8
        $notesPath = $tempNotes
    }
    if (-not (Test-Path $notesPath)) { Die "No se encontró el archivo de notas: $notesPath" }

    # ── 1. Bump de versión ───────────────────────────────────────────────────
    # En dry run también se bumpea: el instalador que se compila a continuación debe llevar la versión
    # real que se publicaría. Al final del dry run se revierte el .csproj.
    $csprojBumped = $false
    if ($currentVersion -ne $Version) {
        Info "Actualizando <Version>, <AssemblyVersion> y <FileVersion> en el .csproj..."
        $newRaw = $csprojRaw `
            -replace '<Version>.*?</Version>',                 "<Version>$Version</Version>" `
            -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>" `
            -replace '<FileVersion>.*?</FileVersion>',         "<FileVersion>$Version.0</FileVersion>"
        # CON BOM ($true): es lo que hace que la próxima lectura —la del siguiente release, o la de
        # MSBuild— sepa con certeza que el archivo es UTF-8. Ver la nota de codificación de arriba.
        [System.IO.File]::WriteAllText($csproj, $newRaw, (New-Object System.Text.UTF8Encoding($true)))
        $csprojBumped = $true
    }

    # ── 2. Compilar instalador ───────────────────────────────────────────────
    Info "Compilando el instalador..."
    $buildArgs = @{ Version = $Version }
    if ($CertThumbprint) { $buildArgs.CertThumbprint = $CertThumbprint }
    if ($CertFile)       { $buildArgs.CertFile       = $CertFile }
    if ($CertPassword)   { $buildArgs.CertPassword   = $CertPassword }
    if ($TimestampUrl)   { $buildArgs.TimestampUrl   = $TimestampUrl }
    & $buildScript @buildArgs
    if ($LASTEXITCODE -ne 0) { Die "La compilación del instalador falló." }

    $setup = Join-Path $outputDir "OfiConvert_Setup_$Version.exe"
    if (-not (Test-Path $setup)) { Die "No se encontró el instalador esperado: $setup" }
    $sizeMB = [math]::Round((Get-Item $setup).Length / 1MB, 1)
    Ok "Instalador: $setup ($sizeMB MB)"

    # Lo genera build-installer.ps1. Se aborta si falta: sin el hash, un instalador sin firmar no es
    # verificable de ninguna manera (ver la nota del encabezado).
    $setupHash = "$setup.sha256"
    if (-not (Test-Path $setupHash)) { Die "No se encontró el checksum esperado: $setupHash" }
    Ok "Checksum: $setupHash"

    # ── DRY RUN: mostrar el plan, revertir el bump y salir ───────────────────
    if ($DryRun) {
        Write-Host ""
        Warn "DRY RUN — no se ha tocado git ni GitHub. Lo que haría un corte real desde aquí:"
        $signNote = if ($CertThumbprint -or $CertFile) { "firmado con Authenticode" } else { "SIN firmar (se publica el .sha256)" }
        Write-Host "    1. <Version>/<AssemblyVersion>/<FileVersion> -> $Version   [YA hecho, se revierte al salir]" -ForegroundColor DarkGray
        Write-Host "    2. Instalador $signNote                                    [YA compilado, queda en Output\]" -ForegroundColor DarkGray
        Write-Host "    3. git add -u; git commit -m 'release: v$Version'; git tag -a $tag" -ForegroundColor DarkGray
        Write-Host "    4. git push origin $branch; git push origin $tag" -ForegroundColor DarkGray
        Write-Host "    5. gh release create $tag  (assets: OfiConvert_Setup_$Version.exe + .sha256)" -ForegroundColor DarkGray

        if ($csprojBumped) {
            [System.IO.File]::WriteAllText($csproj, $csprojRaw, (New-Object System.Text.UTF8Encoding($true)))
            Info "Revertido el bump del .csproj (seguía en $currentVersion)."
        }
        if ($tempNotes) { Remove-Item $tempNotes -Force -ErrorAction SilentlyContinue }
        Ok "Dry run completado."
        return
    }

    # ── 3. Commit + tag ──────────────────────────────────────────────────────
    # `git add -u` = solo archivos YA rastreados. Los nuevos se añaden a mano antes (se avisó arriba).
    Info "Preparando el commit del release..."
    if ((Invoke-Git add -u) -ne 0) { Die "git add -u falló." }
    $staged = (& git diff --cached --name-only)
    if ($staged) {
        Info "Archivos incluidos en el commit:"
        $staged | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
        if ((Invoke-Git commit -m "release: v$Version") -ne 0) { Die "git commit falló." }
        Ok "Commit del release creado."
    } else {
        Info "Sin cambios que commitear; se etiqueta el HEAD actual."
    }

    Info "Creando el tag $tag..."
    if ((Invoke-Git tag -a $tag -m "OfiConvert $tag") -ne 0) { Die "git tag falló." }

    # ── 4. Push ──────────────────────────────────────────────────────────────
    Info "Push de la rama y el tag a origin..."
    if ((Invoke-Git push origin $branch) -ne 0) { Die "git push de la rama falló." }
    if ((Invoke-Git push origin $tag) -ne 0) { Die "git push del tag falló. La rama YA está subida; reintenta." }
    Ok "Rama y tag publicados."

    # ── 5. GitHub Release ────────────────────────────────────────────────────
    $gh = @(
        "C:\Program Files\GitHub CLI\gh.exe",
        "C:\Program Files (x86)\GitHub CLI\gh.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $gh) {
        $cmd = Get-Command gh -ErrorAction SilentlyContinue
        if ($cmd) { $gh = $cmd.Source }
    }
    if (-not $gh) { Die "gh (GitHub CLI) no está instalado: winget install GitHub.cli — el tag YA está publicado; crea el release a mano o reintenta." }

    # Si gh no está autenticado, se reutiliza la credencial de git ya cacheada (la misma del push).
    # PS 5.1: con ErrorActionPreference=Stop, un `2>$null` sobre un exe nativo genera NativeCommandError;
    # se baja a SilentlyContinue solo durante las llamadas que necesitan silenciar stderr.
    $eap = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    & $gh auth status 2>$null
    $authOk = $LASTEXITCODE -eq 0
    $ErrorActionPreference = $eap

    if (-not $authOk) {
        Warn "gh no está autenticado; reutilizando la credencial de git cacheada (local, no se imprime)."
        $eap = $ErrorActionPreference
        $ErrorActionPreference = "SilentlyContinue"
        $cred = "protocol=https`nhost=github.com`n`n" | & git credential fill 2>$null
        $ErrorActionPreference = $eap
        $pwdLine = $cred | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
        if ($pwdLine) { $env:GH_TOKEN = $pwdLine.Substring(9) }
        if (-not $env:GH_TOKEN) { Die "No se pudo obtener una credencial para gh. Ejecuta 'gh auth login' y reintenta (el tag ya está publicado)." }
    }

    Info "Creando el GitHub Release..."
    & $gh release create $tag --title "OfiConvert $tag" --notes-file $notesPath $setup $setupHash
    if ($LASTEXITCODE -ne 0) { Die "gh release create falló (el tag ya está publicado; puedes reintentar solo este paso)." }

    if ($tempNotes) { Remove-Item $tempNotes -Force -ErrorAction SilentlyContinue }
    Write-Host ""
    Ok "Release $tag publicado: https://github.com/xfiberex/OfiConvert/releases/tag/$tag"
}
finally {
    Pop-Location
}
