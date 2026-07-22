using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// El historial pintaba un tilde verde FIJO para TODAS las filas (el <c>FontIcon</c> tenía el glifo y el
/// color en duro, sin mirar <c>Success</c>): una conversión fallida se veía idéntica a una correcta. La
/// decisión de glifo vive ahora en <see cref="HistoryStatus"/>, y lo que de verdad importa es que éxito y
/// fallo NO compartan icono — que es justo lo que rompía el bug.
/// </summary>
public sealed class HistoryStatusTests
{
    [Fact]
    public void Success_AndFailure_UseDifferentGlyphs()
        => Assert.NotEqual(HistoryStatus.Glyph(success: true), HistoryStatus.Glyph(success: false));

    [Fact]
    public void Success_UsesTheCheckmark()
        => Assert.Equal(HistoryStatus.SuccessGlyph, HistoryStatus.Glyph(success: true));

    [Fact]
    public void Failure_UsesTheErrorGlyph()
        => Assert.Equal(HistoryStatus.ErrorGlyph, HistoryStatus.Glyph(success: false));

    /// <summary>Ningún glifo puede ser vacío: un icono en blanco no dice nada.</summary>
    [Fact]
    public void NeitherGlyphIsEmpty()
    {
        Assert.False(string.IsNullOrEmpty(HistoryStatus.SuccessGlyph));
        Assert.False(string.IsNullOrEmpty(HistoryStatus.ErrorGlyph));
    }
}
