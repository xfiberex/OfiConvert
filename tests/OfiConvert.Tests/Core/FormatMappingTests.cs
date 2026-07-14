using OfiConvert.Core;
using OfiConvert.Models;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// El mapeo que decide qué se le ofrece al usuario en el desplegable de formatos. Un error aquí no rompe
/// nada visiblemente: le ofrece una conversión que el motor no sabe hacer, y el fallo aparece a mitad del
/// lote.
/// </summary>
public sealed class FormatMappingTests
{
    [Theory]
    [InlineData("doc")]
    [InlineData("docx")]
    public void WordDocuments_GoToPdfOrHtml(string extension)
        => Assert.Equal([OutputFormat.PDF, OutputFormat.HTML], OutputFormatHelper.GetFormatsForExtension(extension));

    [Theory]
    [InlineData("xls")]
    [InlineData("xlsx")]
    public void Spreadsheets_GoToPdfOrCsv(string extension)
        => Assert.Equal([OutputFormat.PDF, OutputFormat.CSV], OutputFormatHelper.GetFormatsForExtension(extension));

    [Theory]
    [InlineData("ppt")]
    [InlineData("pptx")]
    public void Presentations_AlsoGoToImages(string extension)
        => Assert.Equal(
            [OutputFormat.PDF, OutputFormat.PNG, OutputFormat.JPG],
            OutputFormatHelper.GetFormatsForExtension(extension));

    /// <summary>
    /// Los llamantes pasan la extensión SIN punto (<c>FileItem.Extension</c>), pero el resto del código la
    /// maneja con punto. Que dé igual evita el bug tonto: ".docx" cayendo en el "_ => [PDF]" y perdiendo
    /// la opción de HTML sin que nadie lo note.
    /// </summary>
    [Theory]
    [InlineData("docx")]
    [InlineData(".docx")]
    [InlineData("DOCX")]
    [InlineData(".DocX")]
    public void Extension_WorksWithOrWithoutDotAndInAnyCase(string extension)
        => Assert.Equal([OutputFormat.PDF, OutputFormat.HTML], OutputFormatHelper.GetFormatsForExtension(extension));

    [Fact]
    public void UnknownExtension_FallsBackToPdf()
        => Assert.Equal([OutputFormat.PDF], OutputFormatHelper.GetFormatsForExtension("txt"));

    [Theory]
    [InlineData(OutputFormat.PDF, ".pdf")]
    [InlineData(OutputFormat.HTML, ".html")]
    [InlineData(OutputFormat.CSV, ".csv")]
    [InlineData(OutputFormat.PNG, ".png")]
    [InlineData(OutputFormat.JPG, ".jpg")]
    public void EveryFormat_HasItsFileExtension(OutputFormat format, string expected)
        => Assert.Equal(expected, OutputFormatHelper.GetFileExtension(format));

    /// <summary>Añadir un formato al enum y olvidar su extensión lo dejaría saliendo como ".pdf" en silencio.</summary>
    [Fact]
    public void NoFormatIsLeftWithoutAnExtensionOrADisplayName()
    {
        Assert.All(Enum.GetValues<OutputFormat>(), format =>
        {
            Assert.False(string.IsNullOrWhiteSpace(OutputFormatHelper.GetFileExtension(format)));
            Assert.False(string.IsNullOrWhiteSpace(OutputFormatHelper.GetDisplayName(format)));
        });
    }

    [Theory]
    [InlineData(".docx", true)]
    [InlineData(".DOCX", true)]
    [InlineData(".ppt", true)]
    [InlineData(".pdf", false)]
    [InlineData(".exe", false)]
    [InlineData("", false)]
    public void OfficeFormats_DecidesWhatTheAppAccepts(string extension, bool expected)
        => Assert.Equal(expected, OfficeFormats.IsSupported(extension));

    /// <summary>
    /// La lista es la fuente única: la usan los motores, el menú contextual del Explorador y el filtro de
    /// los argumentos de activación. Toda extensión admitida tiene que tener a dónde convertirse.
    /// </summary>
    [Fact]
    public void EverySupportedExtension_HasSomewhereToConvertTo()
    {
        Assert.All(OfficeFormats.SupportedExtensions, extension =>
        {
            Assert.StartsWith(".", extension, StringComparison.Ordinal);
            Assert.NotEmpty(OutputFormatHelper.GetFormatsForExtension(extension));
        });
    }
}
