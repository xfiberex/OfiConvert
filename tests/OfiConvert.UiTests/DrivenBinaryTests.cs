namespace OfiConvert.UiTests;

/// <summary>
/// Comprueba QUÉ binario están conduciendo estos tests.
///
/// Suena a test sobre el andamio, y lo es: hasta el Tier J (TJ-05) el corte de versión compilaba en
/// Release y luego corría <c>dotnet test</c> <b>sin</b> <c>-c Release</c>, así que MSBuild reconstruía la
/// app en Debug por el <c>ProjectReference</c> y <c>AppFixture</c> —que cogía el <c>.exe</c> más
/// reciente— conducía el binario Debug. El instalador, mientras tanto, empaqueta un publish Release:
/// las 30 pruebas de UI validaban un binario que no era el que se publica, y nada lo decía.
///
/// Es la misma familia que el bug del Tier G (los UI tests conducían un <c>.exe</c> VIEJO): aquel
/// garantizó que el binario fuera <b>fresco</b>; este, que sea <b>el que se publica</b>.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class DrivenBinaryTests(AppFixture fixture)
{
    [Fact]
    public void ElExeConducido_EsElDeLaConfiguracionCompilada()
    {
        // Con OFICONVERT_EXE el usuario ha dicho explícitamente qué conducir (un publish, una
        // instalación real): ahí no hay nada que adivinar ni que vigilar.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OFICONVERT_EXE")))
            return;

        Assert.False(string.IsNullOrWhiteSpace(fixture.Configuration));

        string expected = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}{fixture.Configuration}{Path.DirectorySeparatorChar}";
        Assert.True(fixture.ExePath.Contains(expected, StringComparison.OrdinalIgnoreCase),
            $"Los UI tests están conduciendo '{fixture.ExePath}', que no es el binario de la configuración " +
            $"compilada ({fixture.Configuration}). El corte de versión estaría validando un binario distinto " +
            "del que empaqueta el instalador.");

        // publish\ es una copia del mismo build, pero no es lo que se acaba de compilar: si aparece aquí,
        // es que la resolución volvió a elegir por fecha.
        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}publish{Path.DirectorySeparatorChar}",
            fixture.ExePath, StringComparison.OrdinalIgnoreCase);

        Assert.True(File.Exists(fixture.ExePath));
    }
}
