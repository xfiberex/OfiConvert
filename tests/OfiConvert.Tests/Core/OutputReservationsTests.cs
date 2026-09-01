using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// Dos archivos distintos que se llaman igual no pueden acabar siendo uno solo.
/// </summary>
/// <remarks>
/// <c>OutputPath.GetSafe</c> decidía con <c>File.Exists</c>, que solo ve lo <b>ya escrito</b>. Con una
/// carpeta de destino común y dos orígenes homónimos —<c>ventas\informe.docx</c> y
/// <c>compras\informe.docx</c>, de lo más corriente— las dos conversiones preguntaban a la vez por
/// <c>informe.pdf</c>, las dos oían que no existía, y la segunda en terminar pisaba a la primera. Sin
/// error, sin aviso, y con las dos apuntadas como correctas en el historial. (TJ-11.)
/// </remarks>
public sealed class OutputReservationsTests : IDisposable
{
    private readonly string _destino = Directory.CreateTempSubdirectory("OfiConvertTests_").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_destino, recursive: true); } catch { /* limpieza best-effort */ }
    }

    /// <summary>EL CASO QUE IMPORTA: dos orígenes homónimos, ningún archivo escrito todavía.</summary>
    [Fact]
    public void DosArchivosHomonimos_ObtienenRutasDISTINTAS()
    {
        var reservas = new OutputReservations();

        var primero = reservas.ReserveFile(_destino, "informe.pdf");
        var segundo = reservas.ReserveFile(_destino, "informe.pdf");

        Assert.NotEqual(primero, segundo);
        Assert.Equal(Path.Combine(_destino, "informe.pdf"), primero);
        Assert.Equal(Path.Combine(_destino, "informe (1).pdf"), segundo);
    }

    /// <summary>
    /// Y con el lote entero pidiendo a la vez, que es como ocurre de verdad.
    /// </summary>
    /// <remarks>
    /// Sin el candado, dos hilos pueden mirar antes de que ninguno apunte y llevarse el mismo nombre. Con
    /// 32 peticiones simultáneas eso salta enseguida; con dos, casi nunca.
    /// </remarks>
    [Fact]
    public void TreintaYDosPeticionesSIMULTANEAS_NoRepitenNingunNombre()
    {
        var reservas = new OutputReservations();
        var rutas = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 32, _ => rutas.Add(reservas.ReserveFile(_destino, "informe.pdf")));

        var distintas = rutas.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        Assert.True(distintas == 32,
            $"De 32 reservas simultáneas solo {distintas} son distintas: hay conversiones que se van a "
                + "pisar el archivo entre ellas.");
    }

    /// <summary>Lo ya escrito en disco sigue contando: la reserva SUMA a File.Exists, no lo sustituye.</summary>
    [Fact]
    public void UnArchivoQueYaEXISTE_SigueRespetandose()
    {
        File.WriteAllText(Path.Combine(_destino, "informe.pdf"), "el archivo del usuario");
        var reservas = new OutputReservations();

        var ruta = reservas.ReserveFile(_destino, "informe.pdf");

        Assert.Equal(Path.Combine(_destino, "informe (1).pdf"), ruta);
        Assert.Equal("el archivo del usuario", File.ReadAllText(Path.Combine(_destino, "informe.pdf")));
    }

    /// <summary>Nombres distintos no se estorban: reservar no puede volverse renombrar a lo tonto.</summary>
    [Fact]
    public void NombresDISTINTOS_NoSeRenombran()
    {
        var reservas = new OutputReservations();

        Assert.Equal(Path.Combine(_destino, "a.pdf"), reservas.ReserveFile(_destino, "a.pdf"));
        Assert.Equal(Path.Combine(_destino, "b.pdf"), reservas.ReserveFile(_destino, "b.pdf"));
        Assert.Equal(2, reservas.Count);
    }

    /// <summary>
    /// Dos presentaciones distintas del mismo nombre exportarían sus diapositivas a la MISMA carpeta,
    /// mezcladas y pisándose por número.
    /// </summary>
    [Fact]
    public void DosPresentacionesHomonimas_VanACarpetasDISTINTAS()
    {
        var reservas = new OutputReservations();

        var primera = reservas.ReserveFolder(_destino, "ventas");
        var segunda = reservas.ReserveFolder(_destino, "ventas");

        Assert.NotEqual(primera, segunda);
        Assert.Equal(Path.Combine(_destino, "ventas"), primera);
        Assert.Equal(Path.Combine(_destino, "ventas (1)"), segunda);
    }

    /// <summary>
    /// Lo que NO cambia: reconvertir la MISMA presentación en otro lote reescribe su carpeta, que es lo
    /// que el usuario espera. Cada lote empieza con las reservas vacías.
    /// </summary>
    [Fact]
    public void OtroLote_VuelveAUsarLaMismaCarpeta()
    {
        var carpeta = new OutputReservations().ReserveFolder(_destino, "ventas");
        Directory.CreateDirectory(carpeta);

        var enOtroLote = new OutputReservations().ReserveFolder(_destino, "ventas");

        Assert.Equal(carpeta, enOtroLote);
    }

    /// <summary>La garantía de contención no se pierde por el camino.</summary>
    [Fact]
    public void SigueSinPoderEscAPARSeDeLaCarpeta()
    {
        var reservas = new OutputReservations();
        var ruta = reservas.ReserveFile(_destino, @"..\..\fuera.pdf");

        Assert.Equal(Path.Combine(_destino, "fuera.pdf"), ruta);
    }
}
