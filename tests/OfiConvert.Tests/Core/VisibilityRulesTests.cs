using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// La regla que <c>CountToVisibilityConverter</c> llevaba <b>ignorando desde siempre</b>: el XAML le pasaba
/// <c>ConverterParameter=Invert</c> al contador de reintentos de cada archivo y el converter nunca miraba
/// el parámetro. Resultado en la app publicada: <c>↻ 0</c> visible en TODAS las filas (un cero que no dice
/// nada) y el contador <b>escondido justo cuando un archivo había reintentado</b>, que es el único momento
/// en que ese número importa.
///
/// Un converter mal escrito no rompe el build: solo enseña, o esconde, lo que no debe. Por eso la regla
/// vive fuera del converter y se prueba.
/// </summary>
public sealed class VisibilityRulesTests
{
    [Theory]
    [InlineData("Invert", true)]
    [InlineData("invert", true)]   // no distingue mayúsculas
    [InlineData("INVERT", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("otra cosa", false)]
    public void IsInverted_OnlyInvertMeansSomething(string? parameter, bool expected)
        => Assert.Equal(expected, VisibilityRules.IsInverted(parameter));

    /// <summary>Sin invertir: es un estado vacío ("no hay archivos seleccionados"), se ve cuando no hay nada.</summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(9, false)]
    public void WithoutInvert_TheEmptyStateShowsWhenThereIsNothing(int count, bool expected)
        => Assert.Equal(expected, VisibilityRules.ShowForCount(count, invert: false));

    /// <summary>Invertido: es el contador de reintentos, se ve SOLO cuando hay reintentos que contar.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    public void WithInvert_TheRetryCounterShowsOnlyWhenThereAreRetries(int count, bool expected)
        => Assert.Equal(expected, VisibilityRules.ShowForCount(count, invert: true));

    /// <summary>Las dos reglas son opuestas para cualquier contador: si no, una de las dos está mal.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    public void TheTwoRulesAreAlwaysOpposite(int count)
        => Assert.NotEqual(
            VisibilityRules.ShowForCount(count, invert: false),
            VisibilityRules.ShowForCount(count, invert: true));
}
