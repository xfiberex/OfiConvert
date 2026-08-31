using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Test que <b>conduce Microsoft Office de verdad</b> por COM. Se OMITE salvo que
/// <c>OFICONVERT_OFFICE_TESTS=1</c>.
/// </summary>
/// <remarks>
/// Sigue el patrón de <see cref="NetworkFactAttribute"/>, y por el mismo motivo: <b>omitir no es
/// fallar</b>. Estas pruebas abren PowerPoint, crean presentaciones y las convierten; en una máquina sin
/// Office —o en la de alguien que esté trabajando con Office abierto— fallarían por el entorno, no porque
/// la app esté rota, y una suite que falla por el entorno deja de creerse. Por eso ningún corte de
/// versión depende de ellas: <c>release.ps1</c> las ejecuta <b>omitidas</b>.
///
/// Cubren lo único que no se puede simular: que PowerPoint es una instancia COM <b>compartida</b>
/// (TJ-01). Se ejecutan a mano cuando se toca el motor de Office:
/// <code>$env:OFICONVERT_OFFICE_TESTS=1; dotnet test tests\OfiConvert.Tests\OfiConvert.Tests.csproj</code>
/// </remarks>
public sealed class OfficeFactAttribute : FactAttribute
{
    public OfficeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("OFICONVERT_OFFICE_TESTS") != "1")
            Skip = "Requiere Microsoft Office instalado. Actívalo con OFICONVERT_OFFICE_TESTS=1.";
    }
}
