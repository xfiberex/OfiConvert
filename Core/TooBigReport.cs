namespace OfiConvert.Core;

/// <summary>
/// El aviso de «archivos demasiado grandes»: <b>uno solo, nombrándolos todos</b>.
/// </summary>
/// <remarks>
/// 🔴 <b>El fallo que cierra (TJ-13): un diálogo por archivo, dentro del bucle.</b>
///
/// <c>AddFiles</c> avisaba <b>dentro</b> del bucle que recorre lo que se acaba de soltar, y
/// <c>IDialogService.ShowInformation</c> es <c>async void</c> — dispara y olvida. Soltar dos documentos
/// de más de 500 MB abría el segundo <c>ContentDialog</c> con el primero todavía en pantalla, y WinUI
/// solo admite uno: el segundo <b>lanza</b>. Sobre un <c>async void</c> esa excepción sale sin dueño, la
/// recoge <c>App.UnhandledException</c> y <b>el usuario no ve nada</b> — ni el aviso ni el error. Se
/// quedaba sin saber por qué faltaban archivos.
///
/// Vive aquí, y no en el ViewModel, para poder comprobar lo único que de verdad importa: que de N
/// rechazados sale <b>un</b> mensaje y que están <b>todos</b> nombrados. En el ViewModel eso solo se
/// podría comprobar arrancando WinUI.
/// </remarks>
public static class TooBigReport
{
    /// <summary>
    /// Compone el aviso, o <c>null</c> si no hay nada que decir.
    /// </summary>
    /// <param name="names">Nombres de los archivos rechazados, en el orden en que se soltaron.</param>
    /// <param name="oneTemplate">Plantilla de un solo archivo, con <c>{0}</c> para su nombre.</param>
    /// <param name="manyTemplate">Plantilla de varios, con <c>{0}</c> para la lista ya formada.</param>
    /// <remarks>
    /// Se conserva el mensaje de uno solo —el que el usuario ya conocía— en vez de mandar siempre por la
    /// forma plural: «Estos archivos superan el límite:» seguido de una única línea se lee como un error
    /// de la propia aplicación.
    /// </remarks>
    public static string? Compose(IReadOnlyList<string> names, string oneTemplate, string manyTemplate)
    {
        ArgumentNullException.ThrowIfNull(names);

        return names.Count switch
        {
            0 => null,
            1 => string.Format(oneTemplate, names[0]),
            _ => string.Format(manyTemplate, string.Join(Environment.NewLine, names.Select(n => "• " + n))),
        };
    }
}
