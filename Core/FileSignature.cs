namespace OfiConvert.Core;

/// <summary>Qué dice la cabecera de un archivo sobre él.</summary>
public enum FileSignatureVerdict
{
    /// <summary>La firma concuerda con la extensión: adelante.</summary>
    Ok,

    /// <summary>Menos de 8 bytes: ni siquiera hay cabecera que mirar.</summary>
    TooSmall,

    /// <summary>La firma no es ni OLE ni ZIP: el archivo no es lo que dice su extensión.</summary>
    Corrupt,

    /// <summary>Protegido con contraseña (no corrupto): hay que decírselo así al usuario.</summary>
    PasswordProtected,

    /// <summary>Cabecera ZIP en un OpenXML: correcto, pero hay que abrir el ZIP para descartar cifrado.</summary>
    NeedsZipInspection
}

/// <summary>
/// Clasifica un archivo Office por sus <b>magic bytes</b>, sin tocar el disco. La parte con E/S (abrir,
/// detectar bloqueos, mirar dentro del ZIP) se queda en <c>Services/FileValidationService</c>; aquí vive
/// la tabla de decisión, que es lo que de verdad se puede equivocar y lo que hay que poder probar.
/// </summary>
public static class FileSignature
{
    /// <summary>Documento compuesto OLE: el formato binario del Office viejo (.doc/.xls/.ppt).</summary>
    public static ReadOnlySpan<byte> Ole => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>Archivo ZIP: el envoltorio de todo OpenXML (.docx/.xlsx/.pptx).</summary>
    public static ReadOnlySpan<byte> Zip => [0x50, 0x4B, 0x03, 0x04];

    /// <summary>Bytes que hay que leer del archivo para poder clasificarlo.</summary>
    public const int HeaderLength = 8;

    /// <param name="header">Los primeros <see cref="HeaderLength"/> bytes del archivo.</param>
    /// <param name="extension">Extensión con punto (".docx"); no distingue mayúsculas.</param>
    public static FileSignatureVerdict Classify(ReadOnlySpan<byte> header, string extension)
    {
        if (header.Length < HeaderLength)
            return FileSignatureVerdict.TooSmall;

        var ext = extension.ToLowerInvariant();
        bool hasZipMagic = header[..4].SequenceEqual(Zip);
        bool hasOleMagic = header[..8].SequenceEqual(Ole);

        if (ext is ".docx" or ".xlsx" or ".pptx")
        {
            if (hasZipMagic) return FileSignatureVerdict.NeedsZipInspection;

            // Un OpenXML cifrado se guarda dentro de un documento compuesto OLE. No está corrupto:
            // está protegido, y el usuario merece que se lo digamos con esas palabras.
            if (hasOleMagic) return FileSignatureVerdict.PasswordProtected;

            return FileSignatureVerdict.Corrupt;
        }

        if (ext is ".doc" or ".xls" or ".ppt")
        {
            if (hasOleMagic) return FileSignatureVerdict.Ok;

            // Un .docx al que alguien renombró la extensión a .doc. Office lo abre igual, así que se deja
            // pasar en vez de plantarle un "corrupto" delante a un archivo perfectamente convertible.
            if (hasZipMagic) return FileSignatureVerdict.Ok;

            return FileSignatureVerdict.Corrupt;
        }

        // Extensión que no es de Office: la firma no puede decir nada útil. Quién entra y quién no es
        // decisión de OfficeFormats, no de aquí.
        return FileSignatureVerdict.Ok;
    }
}
