using System.Globalization;
using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// <see cref="ByteSize.Format"/> formatea con la <b>cultura actual</b> a propósito (es texto que lee el
/// usuario). Estas pruebas la fijan a la invariante para no depender de la máquina que las corre: en un
/// Windows en español el separador decimal es la coma y "1.5 KB" sería "1,5 KB".
/// </summary>
public sealed class ByteSizeTests : IDisposable
{
    private readonly CultureInfo _original = CultureInfo.CurrentCulture;

    public ByteSizeTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _original;

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    [InlineData(1099511627776, "1 TB")]
    public void Format_PicksTheUnitAndRoundsToTwoDecimals(long bytes, string expected)
        => Assert.Equal(expected, ByteSize.Format(bytes));

    /// <summary>
    /// TB es la unidad más grande: por encima, el número crece en vez de inventarse un "PB" que la tabla
    /// no tiene. Es justo el caso en el que las dos implementaciones duplicadas discrepaban.
    /// </summary>
    [Fact]
    public void Format_AboveTerabytes_KeepsGrowingInTerabytes()
        => Assert.Equal("2048 TB", ByteSize.Format(2048L * 1024 * 1024 * 1024 * 1024));

    /// <summary>
    /// El historial se quedaba en GB: un archivo de 2 TB salía como "2 TB" en la cola y como "2048 GB" en
    /// el historial exportado. La razón de que exista esta clase es que ese número sea uno solo.
    /// </summary>
    [Fact]
    public void Format_TwoTerabytes_IsNotReportedInGigabytes()
    {
        var formatted = ByteSize.Format(2L * 1024 * 1024 * 1024 * 1024);

        Assert.Equal("2 TB", formatted);
        Assert.DoesNotContain("GB", formatted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Format_NegativeSize_IsClampedToZero(long bytes)
        => Assert.Equal("0 B", ByteSize.Format(bytes));

    /// <summary>La cultura manda: en español el separador decimal es la coma, y así se ve en la UI.</summary>
    [Fact]
    public void Format_UsesTheCurrentCultureDecimalSeparator()
    {
        CultureInfo.CurrentCulture = new CultureInfo("es-ES");

        Assert.Equal("1,5 KB", ByteSize.Format(1536));
    }
}
