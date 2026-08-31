using OfiConvert.Core;

namespace OfiConvert.Models;

public record ConversionResult
{
    public bool Success { get; init; }

    /// <summary>
    /// El fallo, como <b>clave de traducción</b> y argumentos — nunca como texto ya escrito.
    /// </summary>
    /// <remarks>
    /// Los servicios corren en hilos de fondo y no saben en qué idioma está la app: devolver aquí una
    /// cadena en español es como se colaron 18 mensajes sin traducir hasta el Tier J (TJ-06). Se traduce
    /// en el borde de la UI, con <c>MainViewModel.GetLocalizedString(UserMessage)</c>.
    /// </remarks>
    public UserMessage? Error { get; init; }
    public string OutputPath { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public bool WasRetried { get; init; }
    public int RetryCount { get; init; }

    public static ConversionResult Successful(string outputPath, TimeSpan duration = default) =>
        new() { Success = true, OutputPath = outputPath, Duration = duration };

    public static ConversionResult Failed(UserMessage error, int retryCount = 0) =>
        new() { Success = false, Error = error, WasRetried = retryCount > 0, RetryCount = retryCount };

    /// <summary>Atajo: fallo con una clave sin argumentos.</summary>
    public static ConversionResult Failed(string errorKey, int retryCount = 0) =>
        Failed(new UserMessage(errorKey), retryCount);
}