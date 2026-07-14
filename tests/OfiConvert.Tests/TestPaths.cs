using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Localiza el código FUENTE desde el directorio de salida de las pruebas. Los tests de localización leen
/// los <c>Lang/*.xaml</c> y los <c>.cs</c>/<c>.xaml</c> del repositorio, no del <c>bin</c>: la relación
/// "clave usada" solo existe en el texto del código.
/// </summary>
internal static class TestPaths
{
    /// <summary>Raíz del repositorio: la carpeta que contiene <c>OfiConvert.slnx</c>.</summary>
    internal static string RepoRoot { get; } = FindRepoRoot();

    internal static string LangFolder => Path.Combine(RepoRoot, "Lang");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OfiConvert.slnx")))
            dir = dir.Parent;

        // Falla con un mensaje claro: un test que "pasa" porque no encontró el código no prueba nada.
        Assert.True(dir is not null, $"No se encontró la raíz del repositorio (OfiConvert.slnx) desde {AppContext.BaseDirectory}");
        return dir!.FullName;
    }

    /// <summary>Archivos de código de la app, sin <c>bin</c>, <c>obj</c>, <c>tests</c> ni <c>publish</c>.</summary>
    internal static IEnumerable<string> AppSourceFiles(string pattern)
    {
        var excluded = new[]
        {
            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}publish{Path.DirectorySeparatorChar}",
        };

        return Directory
            .EnumerateFiles(RepoRoot, pattern, SearchOption.AllDirectories)
            .Where(f => !excluded.Any(e => f.Contains(e, StringComparison.OrdinalIgnoreCase)));
    }
}
