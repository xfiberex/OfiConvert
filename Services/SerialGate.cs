namespace OfiConvert.Services;

/// <summary>
/// Deja pasar <b>una sola</b> operación a la vez.
/// </summary>
/// <remarks>
/// Existe por PowerPoint (TJ-01, 2026-08-31). <c>Type.GetTypeFromProgID("PowerPoint.Application")</c> +
/// <c>Activator.CreateInstance</c> <b>no crea un proceso nuevo</b>: devuelve el PowerPoint que ya está
/// corriendo — medido en esta máquina (Office 16 ClickToRun), dos activaciones seguidas dejan
/// <b>un</b> <c>POWERPNT.EXE</c>, mientras que Word y Excel sí crean dos.
///
/// Con <c>MaxParallelConversions &gt; 1</c>, eso significaba que N conversiones de <c>.pptx</c> conducían
/// <b>la misma</b> instancia: la primera en terminar llamaba a <c>Quit()</c> y <b>mataba a las demás</b> a
/// media conversión. Serializar no es una optimización pendiente: es la única forma correcta de usar una
/// aplicación COM que no se puede instanciar dos veces.
///
/// Word y Excel <b>no</b> pasan por aquí: cada activación les crea su propio proceso, y ahí el
/// paralelismo es real y deseado.
/// </remarks>
public sealed class SerialGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>La puerta de PowerPoint. Una para todo el proceso, porque el PowerPoint también es uno.</summary>
    public static SerialGate PowerPoint { get; } = new();

    /// <summary>Ejecuta <paramref name="work"/> con la garantía de que nadie más lo está haciendo.</summary>
    public async Task RunAsync(Func<Task> work, CancellationToken cancellationToken = default)
    {
        // La espera es cancelable: un lote cancelado no debe quedarse haciendo cola para nada.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await work();
        }
        finally
        {
            _gate.Release();
        }
    }
}
