using OfiConvert.Services;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// La puerta que impide que dos conversiones de PowerPoint se pisen (TJ-01).
/// </summary>
/// <remarks>
/// PowerPoint es una instancia COM <b>única</b>: activarlo dos veces devuelve el mismo proceso. Sin esta
/// puerta, dos conversiones de <c>.pptx</c> en paralelo conducían la misma aplicación y la primera en
/// terminar llamaba a <c>Quit()</c>, matando a la otra a media conversión. Aquí se comprueba lo único que
/// se puede comprobar sin Office instalado: que <b>nunca</b> hay dos dentro a la vez.
/// </remarks>
public sealed class SerialGateTests
{
    [Fact]
    public async Task RunAsync_NuncaDejaTrabajarADosALaVez()
    {
        var gate = new SerialGate();
        int actuales = 0;
        int maximoVisto = 0;

        async Task Trabajo()
        {
            int ahora = Interlocked.Increment(ref actuales);
            InterlockedMax(ref maximoVisto, ahora);
            await Task.Delay(15);        // ventana amplia para que se solapen si la puerta no cierra
            Interlocked.Decrement(ref actuales);
        }

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => gate.RunAsync(Trabajo)));

        Assert.Equal(1, maximoVisto);
        Assert.Equal(0, actuales);
    }

    /// <summary>Un fallo dentro no puede dejar la puerta cerrada: la siguiente conversión no entraría jamás.</summary>
    [Fact]
    public async Task RunAsync_SiElTrabajoFalla_LaPuertaSeAbreIgual()
    {
        var gate = new SerialGate();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gate.RunAsync(() => throw new InvalidOperationException("PowerPoint se cayó")));

        try
        {
            await gate.RunAsync(() => Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("La puerta se quedó cerrada tras un fallo: ninguna conversión de PowerPoint volvería a entrar.");
        }
    }

    /// <summary>Cancelar el lote no deja a nadie haciendo cola para nada.</summary>
    [Fact]
    public async Task RunAsync_LaEsperaEsCancelable()
    {
        var gate = new SerialGate();
        using var ocupado = new SemaphoreSlim(0, 1);
        using var cts = new CancellationTokenSource();

        var primero = gate.RunAsync(async () => await ocupado.WaitAsync(CancellationToken.None));
        var segundo = gate.RunAsync(() => Task.CompletedTask, cts.Token);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => segundo);

        ocupado.Release();
        await primero;
    }

    private static void InterlockedMax(ref int destino, int valor)
    {
        int visto;
        while (valor > (visto = Volatile.Read(ref destino)))
        {
            if (Interlocked.CompareExchange(ref destino, valor, visto) == visto) return;
        }
    }
}
