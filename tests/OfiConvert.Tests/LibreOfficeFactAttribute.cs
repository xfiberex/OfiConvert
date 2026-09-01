using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Test que <b>ejecuta LibreOffice de verdad</b>. Se OMITE salvo que
/// <c>OFICONVERT_LIBREOFFICE_TESTS=1</c>.
/// </summary>
/// <remarks>
/// Tercera puerta de este proyecto, junto a <see cref="NetworkFactAttribute"/> y
/// <see cref="OfficeFactAttribute"/>, y por el mismo motivo: <b>omitir no es fallar</b>. Estas pruebas
/// lanzan ocho procesos <c>soffice</c> y tardan medio minuto; en una máquina sin LibreOffice fallarían
/// por el entorno y no porque la app esté rota, y una suite que falla por el entorno deja de creerse.
/// Ningún corte de versión depende de ellas — <c>release.ps1</c> las ejecuta <b>omitidas</b>, y por eso
/// esta clase está en su <c>ExpectedSkipPattern</c>.
///
/// Cubren lo único que no se puede simular sin el motor: que dos <c>soffice --headless</c> a la vez
/// <b>se estorban si comparten perfil</b> (TJ-25). Se ejecutan a mano cuando se toca el motor:
/// <code>$env:OFICONVERT_LIBREOFFICE_TESTS=1; dotnet test tests\OfiConvert.Tests\OfiConvert.Tests.csproj</code>
///
/// ⚠️ <b>No uses <c>soffice --version</c> para detectar la instalación.</b> En Windows abre una ventana
/// de consola y se queda esperando un «Press Enter to continue…»: capturado desde un script devuelve
/// una cadena vacía y deja la ventana abierta. Aquí la presencia se comprueba por la ruta del ejecutable.
/// </remarks>
public sealed class LibreOfficeFactAttribute : FactAttribute
{
    public LibreOfficeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("OFICONVERT_LIBREOFFICE_TESTS") != "1")
            Skip = "Requiere LibreOffice instalado. Actívalo con OFICONVERT_LIBREOFFICE_TESTS=1.";
    }
}
