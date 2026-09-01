using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// De N archivos rechazados sale <b>UN</b> aviso, y salen <b>todos</b> nombrados.
/// </summary>
/// <remarks>
/// <c>AddFiles</c> avisaba dentro del bucle, y <c>ShowInformation</c> es <c>async void</c>. Soltar dos
/// documentos de más de 500 MB abría el segundo <c>ContentDialog</c> con el primero en pantalla; WinUI
/// solo admite uno, así que el segundo lanzaba sobre un <c>async void</c> y la excepción se perdía sin
/// dueño. El usuario no veía **ni el aviso ni el error**, solo archivos que no aparecían. (TJ-13.)
/// </remarks>
public sealed class TooBigReportTests
{
    private const string Uno = "El archivo '{0}' excede el límite de 500 MB y no se agregará.";
    private const string Varios = "Estos archivos superan el límite de 500 MB y no se agregarán:\n\n{0}";

    [Fact]
    public void SinRechazados_NoHayAviso()
        => Assert.Null(TooBigReport.Compose([], Uno, Varios));

    /// <summary>Uno solo conserva el mensaje de siempre: la forma plural con una línea suena a error.</summary>
    [Fact]
    public void UnSoloArchivo_UsaElMensajeDeSiempre()
    {
        var aviso = TooBigReport.Compose(["enorme.pptx"], Uno, Varios);

        Assert.Equal("El archivo 'enorme.pptx' excede el límite de 500 MB y no se agregará.", aviso);
    }

    /// <summary>EL CRITERIO DE TJ-13: tres archivos, un aviso, los tres nombrados.</summary>
    [Fact]
    public void TresArchivos_UnSoloAviso_ConLosTresNombres()
    {
        var aviso = TooBigReport.Compose(["uno.docx", "dos.xlsx", "tres.pptx"], Uno, Varios);

        Assert.NotNull(aviso);
        Assert.Contains("uno.docx", aviso, StringComparison.Ordinal);
        Assert.Contains("dos.xlsx", aviso, StringComparison.Ordinal);
        Assert.Contains("tres.pptx", aviso, StringComparison.Ordinal);

        // Un aviso, no tres: la plantilla de "varios" aparece una sola vez.
        Assert.StartsWith("Estos archivos superan", aviso, StringComparison.Ordinal);
        Assert.Equal(1, aviso.Split("Estos archivos superan").Length - 1);
    }

    [Fact]
    public void SeConservaElOrdenEnQueSeSoltaron()
    {
        var aviso = TooBigReport.Compose(["c.docx", "a.docx", "b.docx"], Uno, Varios)!;

        Assert.True(
            aviso.IndexOf("c.docx", StringComparison.Ordinal) <
            aviso.IndexOf("a.docx", StringComparison.Ordinal),
            $"El aviso reordena los nombres y el usuario no los reconoce en su orden: {aviso}");
    }

    /// <summary>Los ocho idiomas tienen que traer las dos claves, o el aviso sale en español ahí.</summary>
    [Fact]
    public void LasDosClavesEstanEnLosOchoIdiomas()
    {
        System.Xml.Linq.XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var faltan = new List<string>();

        foreach (var archivo in Directory.GetFiles(TestPaths.LangFolder, "*.xaml"))
        {
            var declaradas = (System.Xml.Linq.XDocument.Load(archivo).Root?.Elements() ?? [])
                .Select(e => e.Attribute(x + "Key")?.Value)
                .Where(k => k is not null)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var clave in new[] { "MsgFileTooBig", "MsgFilesTooBig" })
                if (!declaradas.Contains(clave))
                    faltan.Add($"{Path.GetFileName(archivo)}: {clave}");
        }

        Assert.True(faltan.Count == 0, "Claves del aviso que faltan:\n  " + string.Join("\n  ", faltan));
    }

    /// <summary>Y la de "varios" necesita su {0}, o se pierden los nombres.</summary>
    [Fact]
    public void LaClaveDeVariosLlevaSuHueco()
    {
        System.Xml.Linq.XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var sinHueco = new List<string>();

        foreach (var archivo in Directory.GetFiles(TestPaths.LangFolder, "*.xaml"))
        {
            var valor = (System.Xml.Linq.XDocument.Load(archivo).Root?.Elements() ?? [])
                .FirstOrDefault(e => e.Attribute(x + "Key")?.Value == "MsgFilesTooBig")?.Value;

            if (valor is null || !valor.Contains("{0}", StringComparison.Ordinal))
                sinHueco.Add(Path.GetFileName(archivo));
        }

        Assert.True(sinHueco.Count == 0,
            "Sin {0} no se listan los archivos y el aviso no dice cuáles son:\n  " + string.Join("\n  ", sinHueco));
    }
}
