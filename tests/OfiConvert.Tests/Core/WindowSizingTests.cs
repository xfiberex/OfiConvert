using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// El tamaño con el que nace la ventana, en píxeles físicos (TJ-16).
/// </summary>
/// <remarks>
/// <c>AppWindow.Resize</c> no escala por DPI: pedirle 1050×800 a pelo daba esa ventana al 100 % y una
/// **un tercio más pequeña** al 150 %, con el contenido dibujado más grande dentro. Aquí se comprueba la
/// aritmética, que es lo que estaba mal; que la ventana abra se comprueba en los UI tests.
/// </remarks>
public sealed class WindowSizingTests
{
    [Fact]
    public void Al100PorCiento_ElTamanoEsElDeReferencia()
    {
        var (ancho, alto) = WindowSizing.Default(96);

        Assert.Equal(WindowSizing.DefaultWidth, ancho);
        Assert.Equal(WindowSizing.DefaultHeight, alto);
    }

    [Theory]
    [InlineData(120u, 1.25)]   // 125 %
    [InlineData(144u, 1.50)]   // 150 %, el caso del portátil típico
    [InlineData(192u, 2.00)]   // 200 %
    public void ConEscalado_LaVentanaCreceIgualQueElContenido(uint dpi, double factor)
    {
        var (ancho, alto) = WindowSizing.Default(dpi);

        Assert.Equal((int)Math.Round(WindowSizing.DefaultWidth * factor), ancho);
        Assert.Equal((int)Math.Round(WindowSizing.DefaultHeight * factor), alto);
    }

    /// <summary>Un DPI de 0 (handle inválido) no puede dar una ventana de tamaño cero.</summary>
    [Fact]
    public void SinDpiConocido_SeCaeAl100PorCiento()
    {
        var (ancho, alto) = WindowSizing.Default(0);

        Assert.Equal(WindowSizing.DefaultWidth, ancho);
        Assert.Equal(WindowSizing.DefaultHeight, alto);
    }

    [Theory]
    [InlineData(96u)]
    [InlineData(144u)]
    [InlineData(192u)]
    public void ElMinimo_EsMenorQueElTamanoDeApertura_YEscalaIgual(uint dpi)
    {
        var (ancho, alto) = WindowSizing.Default(dpi);
        var (anchoMin, altoMin) = WindowSizing.Minimum(dpi);

        Assert.True(anchoMin < ancho, "El mínimo no puede ser mayor que el tamaño con el que abre.");
        Assert.True(altoMin < alto);
        Assert.True(anchoMin > 0 && altoMin > 0);
    }

    /// <summary>
    /// El mínimo tiene que dejar sitio a los desplegables de ancho fijo (110 + 140 + 160 px) y a las
    /// etiquetas alemanas, que son las más largas de los ocho idiomas.
    /// </summary>
    [Fact]
    public void ElMinimo_DejaSitioAlContenidoDeAnchoFijo()
    {
        Assert.True(WindowSizing.MinimumWidth >= 800);
        Assert.True(WindowSizing.MinimumHeight >= 560);
    }
}
