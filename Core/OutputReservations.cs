namespace OfiConvert.Core;

/// <summary>
/// Los nombres de salida <b>ya repartidos</b> en un lote, para que dos conversiones simultáneas no se
/// asignen el mismo.
/// </summary>
/// <remarks>
/// 🔴 <b>El agujero que cierra (TJ-11): dos archivos distintos que se llaman igual.</b>
///
/// <see cref="OutputPath.GetSafe"/> decide con <c>File.Exists</c>, y eso solo sabe lo que hay <b>escrito
/// en disco ahora mismo</b>. Con una carpeta de destino común y dos orígenes homónimos —
/// <c>C:\ventas\informe.docx</c> y <c>C:\compras\informe.docx</c>, que es de lo más corriente— pasaba
/// esto:
///
/// <list type="number">
///   <item>Las dos conversiones arrancan a la vez (paralelismo 2 por defecto).</item>
///   <item>Las dos preguntan si existe <c>informe.pdf</c>. Ninguna ha escrito todavía: <b>no existe</b>.</item>
///   <item>Las dos se quedan con <c>informe.pdf</c>. La segunda en terminar <b>pisa a la primera</b>.</item>
/// </list>
///
/// El usuario ve dos conversiones correctas y **un** archivo. No hay error, no hay aviso, y el historial
/// apunta las dos como buenas. Es la garantía nº 2 de <see cref="OutputPath"/> rota por una carrera:
/// <c>GetSafe</c> no mentía, es que a solas no puede saberlo.
///
/// La cura es que el nombre se <b>reserve</b> al calcularlo, no al escribirlo, y que la reserva sea
/// visible para las demás conversiones del lote.
///
/// <b>El alcance es el lote, a propósito.</b> Un objeto nuevo por conversión no arreglaría nada, y uno
/// global para toda la vida del programa haría que reconvertir el mismo documento dos veces fuera a
/// <c>informe (1).pdf</c> sin motivo. Un lote es exactamente el tiempo en que dos conversiones pueden
/// solaparse.
/// </remarks>
public sealed class OutputReservations
{
    private readonly HashSet<string> _taken = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    /// <summary>Cuántos nombres se han repartido. Para pruebas y diagnóstico.</summary>
    public int Count
    {
        get { lock (_gate) return _taken.Count; }
    }

    /// <summary>
    /// Reserva una ruta de <b>archivo</b> libre dentro de <paramref name="outputFolder"/> y la devuelve.
    /// </summary>
    /// <remarks>
    /// Toda la operación —mirar y apuntar— va bajo el mismo candado: si se soltara entre las dos, dos
    /// hilos podrían mirar a la vez y llevarse el mismo nombre, que es justo el fallo que esto cierra.
    /// </remarks>
    public string ReserveFile(string outputFolder, string fileName)
    {
        lock (_gate)
        {
            var path = OutputPath.GetSafe(outputFolder, fileName, EstaReservado);
            _taken.Add(path);
            return path;
        }
    }

    /// <summary>
    /// Reserva la <b>carpeta</b> de salida de una presentación (que son N imágenes y van juntas).
    /// </summary>
    /// <remarks>
    /// Aquí también hace falta, y por lo mismo: dos presentaciones distintas llamadas <c>ventas.pptx</c>
    /// exportarían sus diapositivas <b>a la misma carpeta</b>, mezcladas y pisándose por número de
    /// diapositiva.
    ///
    /// Ojo con lo que NO cambia: reconvertir <b>la misma</b> presentación sigue reescribiendo su carpeta,
    /// porque cada lote empieza con las reservas vacías. Lo que se separa son dos orígenes distintos
    /// <b>dentro del mismo lote</b>.
    /// </remarks>
    public string ReserveFolder(string outputFolder, string folderName)
    {
        lock (_gate)
        {
            var path = OutputPath.GetSafeFolder(outputFolder, folderName, EstaReservado);
            _taken.Add(path);
            return path;
        }
    }

    /// <summary>Se llama con el candado ya tomado.</summary>
    private bool EstaReservado(string candidate) => _taken.Contains(candidate);
}
