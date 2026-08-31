using System.IO;

namespace OfiConvert.Core;

/// <summary>
/// Dónde escribe LibreOffice y cómo llega su resultado al destino que se pidió.
/// </summary>
/// <remarks>
/// <b>LibreOffice no acepta un nombre de salida: solo una carpeta</b> (<c>--outdir</c>), y dentro de ella
/// escribe con el nombre del original — <c>informe.docx</c> → <c>informe.pdf</c>—, <b>pisando</b> lo que
/// hubiera. Apuntarlo directamente a la carpeta del usuario destruía datos: si ya existía un
/// <c>informe.pdf</c>, LibreOffice lo sobrescribía y acto seguido la app movía el recién nacido a
/// <c>informe (1).pdf</c>, así que el archivo anterior <b>desaparecía</b> — justo la garantía nº 2 de
/// <see cref="OutputPath"/> y la promesa del README («sin sobrescrituras»). (TJ-03, 2026-08-31.)
///
/// La cura es no dejarle nunca escribir en la carpeta del usuario: convierte en una carpeta temporal
/// <b>exclusiva</b> de esa conversión, y desde ahí se mueve al destino ya calculado.
/// </remarks>
public static class LibreOfficeOutput
{
    /// <summary>Crea una carpeta de trabajo exclusiva para una conversión.</summary>
    /// <remarks>
    /// Exclusiva y no compartida: dos conversiones en paralelo del mismo nombre (<c>C:\a\informe.docx</c>
    /// y <c>C:\b\informe.docx</c>) producirían ambas <c>informe.pdf</c> y se pisarían entre sí.
    /// </remarks>
    public static string CreateWorkFolder(string? tempRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(tempRoot) ? Path.GetTempPath() : tempRoot;
        var folder = Path.Combine(root, $"OfiConvert-lo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Nombre con el que LibreOffice escribirá el resultado de <paramref name="sourcePath"/>.</summary>
    public static string ExpectedFileName(string sourcePath, string outputExtension)
        => Path.ChangeExtension(Path.GetFileName(sourcePath), outputExtension);

    /// <summary>
    /// Elige, de lo que quedó en la carpeta de trabajo, el archivo que es el resultado.
    /// </summary>
    /// <returns>La ruta elegida, o <c>null</c> si LibreOffice no dejó nada utilizable.</returns>
    /// <remarks>
    /// Primero por nombre esperado; si no está pero solo hay un archivo, ese es (LibreOffice no siempre
    /// respeta el nombre: acentos, formatos que renombran). Con varios y ninguno esperado, no se adivina.
    /// </remarks>
    public static string? PickProduced(IReadOnlyList<string> produced, string expectedFileName)
    {
        foreach (var file in produced)
        {
            if (string.Equals(Path.GetFileName(file), expectedFileName, StringComparison.OrdinalIgnoreCase))
                return file;
        }

        return produced.Count == 1 ? produced[0] : null;
    }

    /// <summary>
    /// Mueve el resultado al destino pedido <b>sin sobrescribir nunca</b>.
    /// </summary>
    /// <returns>La ruta final, que puede ser «archivo (1).pdf» si el destino se ocupó por el camino.</returns>
    /// <remarks>
    /// El destino ya viene libre de colisiones (<see cref="OutputPath.GetSafe"/>), pero eso se calculó
    /// <b>antes</b> de convertir: entre medias pueden haber pasado segundos, otra conversión del lote o el
    /// propio usuario. Se vuelve a comprobar aquí, que es el único momento que importa.
    /// </remarks>
    public static string MoveToFinal(string producedFile, string outputPath)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (string.IsNullOrWhiteSpace(folder))
            throw new InvalidOperationException($"Destino sin carpeta: '{outputPath}'.");

        Directory.CreateDirectory(folder);

        var final = File.Exists(outputPath)
            ? OutputPath.GetSafe(folder, Path.GetFileName(outputPath))
            : outputPath;

        File.Move(producedFile, final);
        return final;
    }
}
