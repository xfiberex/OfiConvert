using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// Los textos legales van embebidos en el <c>.exe</c> y <see cref="LegalText"/> es <b>defensivo</b>: si el
/// recurso faltara devuelve cadena vacía y la UI dice "texto no disponible". Eso significa que romper el
/// <c>EmbeddedResource</c> del <c>.csproj</c> —renombrar un archivo, mover el <c>LogicalName</c>— <b>no
/// rompería nada visible</b>: la app seguiría abriendo su diálogo, vacío, y dejaría de mostrar una
/// atribución que las licencias de Serilog, WebView2 y el Windows App SDK <b>obligan</b> a mostrar.
///
/// Estas pruebas son lo único que separa ese fallo silencioso de un build en rojo.
/// </summary>
public sealed class LegalTextTests
{
    [Fact]
    public void License_IsEmbeddedAndIsTheMitLicense()
    {
        var license = LegalText.License();

        Assert.False(string.IsNullOrWhiteSpace(license), "La LICENSE no está embebida en el ensamblado.");
        Assert.Contains("MIT License", license, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WITHOUT WARRANTY OF ANY KIND", license, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThirdPartyNotices_AreEmbedded()
    {
        var notices = LegalText.ThirdParty();

        Assert.False(string.IsNullOrWhiteSpace(notices), "THIRD-PARTY-NOTICES.txt no está embebido en el ensamblado.");
    }

    /// <summary>
    /// Cada componente que el instalador redistribuye tiene que estar atribuido. Si mañana entra una
    /// dependencia nueva y nadie toca el archivo de avisos, esto no lo caza — pero sí caza que alguien
    /// BORRE una atribución existente, que es el error más fácil de cometer al reorganizar el archivo.
    /// </summary>
    [Theory]
    [InlineData(".NET Runtime")]
    [InlineData("Windows App SDK")]
    [InlineData("WebView2")]
    [InlineData("CommunityToolkit.Mvvm")]
    [InlineData("Serilog")]
    [InlineData("H.NotifyIcon")]
    public void ThirdPartyNotices_AttributeEveryRedistributedComponent(string component)
        => Assert.Contains(component, LegalText.ThirdParty(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Las dos licencias que NO son MIT, y que es donde está el error fácil (el proyecto hermano lo
    /// cometió: declaró el Windows App SDK como MIT, cuando el paquete NuGet que redistribuye viene bajo
    /// los términos de licencia propietarios de Microsoft).
    /// </summary>
    [Fact]
    public void ThirdPartyNotices_DoNotClaimEverythingIsMit()
    {
        var notices = LegalText.ThirdParty();

        Assert.Contains("Apache License 2.0", notices, StringComparison.OrdinalIgnoreCase);       // Serilog
        Assert.Contains("MICROSOFT SOFTWARE LICENSE TERMS", notices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BSD 3-Clause", notices, StringComparison.OrdinalIgnoreCase);             // WebView2
    }

    /// <summary>
    /// Apache 2.0 (cláusula 4.a) exige entregar una COPIA de la licencia a quien recibe el software: no
    /// basta con nombrarla ni con enlazarla. Se comprueba que el texto viaja entero dentro del .exe.
    /// </summary>
    [Fact]
    public void ThirdPartyNotices_ShipTheFullApacheLicenseText()
    {
        var notices = LegalText.ThirdParty();

        Assert.Contains("TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION", notices, StringComparison.Ordinal);
        Assert.Contains("Version 2.0, January 2004", notices, StringComparison.Ordinal);
        Assert.Contains("END OF TERMS AND CONDITIONS", notices, StringComparison.Ordinal);
    }

    /// <summary>La versión sale del ensamblado: es la misma contra la que el updater compara el release.</summary>
    [Fact]
    public void Version_LooksLikeAVersion()
        => Assert.Matches(@"^\d+\.\d+\.\d+$", LegalText.Version());
}
