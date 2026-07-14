using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// El historial se exporta a CSV y alguien lo abre con Excel. Un archivo convertido puede llamarse como
/// le dé la gana a quien lo creó — incluido <c>=cmd|'/c calc'!A1</c> —, así que el nombre acaba siendo
/// entrada no confiable dentro de una hoja de cálculo que ejecuta fórmulas al abrirse.
/// </summary>
public sealed class CsvFieldTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@SUM(A1)")]
    [InlineData("=cmd|'/c calc'!A1")]
    [InlineData("\tcon tabulador")]
    [InlineData("\rcon retorno")]
    public void Sanitize_PrefixesAnythingExcelWouldEvaluate(string value)
    {
        var result = CsvField.Sanitize(value);

        Assert.StartsWith("'", result, StringComparison.Ordinal);
        Assert.Equal("'" + value, result);
    }

    [Theory]
    [InlineData("informe.docx")]
    [InlineData("1 + 1")]                 // el disparador solo cuenta como PRIMER carácter
    [InlineData("presupuesto (final).xlsx")]
    public void Sanitize_LeavesAnOrdinaryFieldAlone(string value)
        => Assert.Equal(value, CsvField.Sanitize(value));

    [Fact]
    public void Sanitize_DoublesQuotesSoTheFieldDoesNotBreakOut()
        => Assert.Equal("el \"\"informe\"\" final", CsvField.Sanitize("el \"informe\" final"));

    /// <summary>
    /// Un campo que empieza por comilla NO lleva prefijo, y está bien así: la celda que ve Excel empieza
    /// entonces por una comilla literal, y eso no es una fórmula para nadie. Se fija aquí porque es el
    /// caso que invita a "arreglarlo" de más.
    /// </summary>
    [Fact]
    public void Sanitize_FieldStartingWithAQuote_IsEscapedButNotPrefixed()
        => Assert.Equal("\"\"=1+1", CsvField.Sanitize("\"=1+1"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_EmptyOrNull_IsAnEmptyField(string? value)
        => Assert.Equal("", CsvField.Sanitize(value));
}
