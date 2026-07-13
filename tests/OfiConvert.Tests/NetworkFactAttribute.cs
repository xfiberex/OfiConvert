using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Test que sale a internet (a GitHub). Se OMITE salvo que <c>OFICONVERT_NETWORK_TESTS=1</c>.
/// </summary>
/// <remarks>
/// Omitir no es lo mismo que fallar: <b>un test omitido dice «aquí no hay red o no se ha pedido»; uno
/// fallido dice «la app está rota»</b>. Confundirlos es lo que hace que una suite deje de creerse.
/// El patrón viene de FormatDiskPro, que lo aprendió teniendo 6 tests que fallaban por diseño cuando su
/// USB de pruebas no estaba conectada — y que por eso no podían entrar en el pipeline.
/// </remarks>
public sealed class NetworkFactAttribute : FactAttribute
{
    public NetworkFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("OFICONVERT_NETWORK_TESTS") != "1")
            Skip = "Requiere red. Actívalo con OFICONVERT_NETWORK_TESTS=1.";
    }
}
