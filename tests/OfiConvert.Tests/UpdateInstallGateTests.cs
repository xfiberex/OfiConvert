using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using OfiConvert.Models;
using OfiConvert.ViewModels;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Instalar una actualización y convertir no pueden solaparse (TJ-15).
/// </summary>
/// <remarks>
/// La instalación termina en <c>Application.Current.Exit()</c>, que <b>no pasa</b> por
/// <c>OnAppWindowClosing</c>: se saltaba la confirmación y la cancelación que existen precisamente para no
/// dejar procesos de Office huérfanos. El botón de instalar no estaba atado a nada.
///
/// El <c>MainViewModel</c> se crea <b>sin su constructor</b>: el constructor lee los ajustes, la cola y el
/// historial reales del usuario, y una prueba unitaria no tiene por qué tocarlos. Las reglas que se prueban
/// aquí solo dependen de las propiedades que se fijan a mano.
/// </remarks>
public sealed class UpdateInstallGateTests
{
    private static MainViewModel NewViewModel(int queuedFiles = 1)
    {
        var vm = (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel));
        vm.SelectedFiles = [.. Enumerable.Range(0, queuedFiles).Select(_ => new FileItem())];
        return vm;
    }

    [Fact]
    public void ConUnLoteEnMarcha_NoSePuedeInstalar()
    {
        var vm = NewViewModel();
        Assert.True(vm.CanInstallUpdate);

        vm.IsConverting = true;

        Assert.False(vm.CanInstallUpdate);
    }

    [Fact]
    public void MientrasSeInstala_NoSeEmpiezaAConvertir()
    {
        // La descarga tarda: sin esto, se podía empezar un lote DESPUÉS de pulsar «Instalar» y la app
        // salía por Exit() a mitad de él. La carrera se cierra por los dos lados.
        var vm = NewViewModel();
        Assert.True(vm.ConvertFilesCommand.CanExecute(null));

        vm.IsInstallingUpdate = true;

        Assert.False(vm.ConvertFilesCommand.CanExecute(null));
        Assert.False(vm.CanInstallUpdate);
    }

    /// <summary>
    /// La ventana sincroniza el botón escuchando <c>PropertyChanged(CanInstallUpdate)</c>. Si el aviso no
    /// sale al cambiar <c>IsConverting</c>, la regla sería correcta y el botón seguiría encendido igual.
    /// </summary>
    [Theory]
    [InlineData(nameof(MainViewModel.IsConverting))]
    [InlineData(nameof(MainViewModel.IsInstallingUpdate))]
    public void CambiarElEstado_AvisaDeCanInstallUpdate(string property)
    {
        var vm = NewViewModel();
        var avisos = new List<string?>();
        vm.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        typeof(MainViewModel).GetProperty(property)!.SetValue(vm, true);

        Assert.Contains(nameof(MainViewModel.CanInstallUpdate), avisos);
    }

    private static string MainWindowCode =>
        File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "MainWindow.xaml.cs"));

    /// <summary>
    /// El estado del botón es UNA regla del ViewModel. Un <c>IsEnabled = true</c> suelto en un
    /// <c>catch</c> —como los que había— lo volvería a encender con un lote en marcha.
    /// </summary>
    [Fact]
    public void ElBotonDeInstalar_SoloObedeceACanInstallUpdate()
    {
        var asignaciones = Regex.Matches(MainWindowCode, @"btnInstalarUpdate\.IsEnabled\s*=\s*([^;]+);")
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();

        Assert.NotEmpty(asignaciones);
        Assert.All(asignaciones, valor => Assert.Equal("ViewModel.CanInstallUpdate", valor));
    }

    /// <summary>Toda salida por <c>Exit()</c> hace antes la misma limpieza que el cierre de la ventana.</summary>
    [Fact]
    public void CadaExit_VienePrecedidoDeLaLimpieza()
    {
        var lineas = MainWindowCode.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        var exits = lineas.Select((l, i) => (l, i)).Where(x => x.l.Contains("Application.Current.Exit()")).ToList();

        Assert.NotEmpty(exits);
        Assert.All(exits, x => Assert.True(x.i > 0 && lineas[x.i - 1] == "ReleaseForShutdown();",
            $"Application.Current.Exit() sin ReleaseForShutdown() justo antes: «{(x.i > 0 ? lineas[x.i - 1] : "")}»"));
    }
}
