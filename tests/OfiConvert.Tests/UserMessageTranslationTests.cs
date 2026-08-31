using OfiConvert.Core;
using OfiConvert.Helpers;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// El borde donde una clave de servicio se convierte en texto para el usuario (TJ-06).
/// </summary>
/// <remarks>
/// Es el criterio de aceptación de TJ-06 comprobado donde se puede comprobar: con la app en japonés, lo
/// que devuelven los servicios —un archivo protegido con contraseña, un fallo de LibreOffice— tiene que
/// llegar <b>en japonés</b> al panel, al historial y al TXT exportado, que leen todos de aquí.
///
/// Durante cinco reincidencias el texto nacía en español dentro de los servicios y salía igual en los
/// ocho idiomas. Estas pruebas fijan lo contrario.
/// </remarks>
public sealed class UserMessageTranslationTests : IDisposable
{
    // El idioma es estado ESTÁTICO del servicio (ver CONTEXT.md §4): se restaura al terminar para no
    // dejar a las demás pruebas hablando japonés.
    private readonly string _idiomaOriginal = LocalizationService.Instance.CurrentLanguage;

    public void Dispose() => LocalizationService.Instance.LoadLanguage(_idiomaOriginal);

    [Theory]
    [InlineData("MsgPasswordProtected")]
    [InlineData("MsgFileNotFound")]
    [InlineData("MsgLibreOfficeNoOutput")]
    public void ConLaAppEnJapones_LosMensajesDeServicioLleganEnJapones(string clave)
    {
        LocalizationService.Instance.LoadLanguage("es");
        string enEspanol = LocalizationService.Translate(new UserMessage(clave));

        LocalizationService.Instance.LoadLanguage("ja");
        string enJapones = LocalizationService.Translate(new UserMessage(clave));

        Assert.NotEqual(enEspanol, enJapones);
        Assert.NotEqual(clave, enJapones);                       // ni la clave cruda
        Assert.Contains(enJapones, c => c >= 0x3000);            // kana o kanji, no español
    }

    [Fact]
    public void LosArgumentos_SeMetenEnLaPlantillaTraducida()
    {
        LocalizationService.Instance.LoadLanguage("en");

        string texto = LocalizationService.Translate(new UserMessage("MsgLibreOfficeError", 77, "boom"));

        Assert.Contains("77", texto);
        Assert.Contains("boom", texto);
        Assert.DoesNotContain("{0}", texto);
    }

    /// <summary>Una clave que no existe se ve; una cadena vacía esconde el error.</summary>
    [Fact]
    public void UnaClaveInexistente_DevuelveLaClave()
        => Assert.Equal("MsgQueNoExiste", LocalizationService.Translate(new UserMessage("MsgQueNoExiste")));

    /// <summary>
    /// Una traducción a la que le falte un hueco <c>{0}</c> no puede tumbar la conversión: se devuelve la
    /// plantilla tal cual, que es feo pero inofensivo.
    /// </summary>
    [Fact]
    public void UnaPlantillaSinHueco_NoLanza()
    {
        LocalizationService.Instance.LoadLanguage("es");

        string texto = LocalizationService.Translate(new UserMessage("MsgFileNotFound", "sobra"));

        Assert.False(string.IsNullOrWhiteSpace(texto));
    }

    [Fact]
    public void SinMensaje_NoHayTexto()
        => Assert.Equal(string.Empty, LocalizationService.Translate(null));
}
