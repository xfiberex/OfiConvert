using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Las pruebas que cambian el idioma <b>no pueden correr en paralelo entre sí</b>.
/// </summary>
/// <remarks>
/// 🔴 <b>Estas dos clases se estaban peleando, y el rojo salía cuando le apetecía.</b>
///
/// El idioma es <b>estado ESTÁTICO</b> en <c>LocalizationService</c>, y tiene que serlo: hay dos
/// instancias vivas —el singleton y la que construye el XAML— y con estado de instancia la interfaz se
/// queda en español en los ocho idiomas. Está en <c>CONTEXT.md</c> §4 como invariante.
///
/// La consecuencia para las pruebas no estaba escrita en ninguna parte: xUnit corre <b>cada clase en su
/// propia colección y las colecciones en paralelo</b>, así que <c>UserMessageTranslationTests</c> ponía
/// «ja» mientras <c>LocalizationTests</c> ponía «es», sobre la <b>misma</b> variable estática. El que
/// perdía la carrera afirmaba sobre el idioma del otro.
///
/// <b>Medido:</b> ejecutando solo esas dos clases, <b>6 rojos de 6</b>. En la suite completa el reparto
/// las separa casi siempre y el fallo asomaba muy de vez en cuando — que es la peor forma de fallar:
/// apareció una sola vez, durante un corte de versión, y lo fácil habría sido repetir y seguir.
///
/// Compartir colección las serializa. No se pierde nada apreciable: son milisegundos.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class LocalizationCollection
{
    public const string Name = "Idioma (estado estático compartido)";
}
