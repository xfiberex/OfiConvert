using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace OfiConvert.UiTests;

/// <summary>
/// Arranca OfiConvert una vez para toda la corrida y la conduce por UI Automation.
/// </summary>
/// <remarks>
/// <b>Sin elevación</b>, como WingetUSoft y a diferencia de FormatDiskPro: OfiConvert corre
/// <c>asInvoker</c> (ver <c>app.manifest</c>) porque nada de lo que hace necesita administrador. Estos
/// tests NO necesitan una terminal elevada, y por eso no hay ningún <c>EnsureElevated()</c>.
///
/// <b>Ninguna prueba de este proyecto convierte un archivo de verdad.</b> Convertir exige Office o
/// LibreOffice instalado y lanza procesos COM: metería una dependencia de entorno en cada corte de versión
/// (<c>release.ps1</c> corre las pruebas). Lo que se comprueba aquí es que la ventana abre y que sus
/// controles están donde deben — la lógica de conversión se cubre en <c>OfiConvert.Tests</c>.
/// </remarks>
public sealed class AppFixture : IDisposable
{
    public Application App { get; }
    public UIA3Automation Automation { get; }
    public Window MainWindow { get; }

    private readonly SettingsBackup _settingsBackup;

    public AppFixture()
    {
        // La app guarda sus datos donde los guarda la instalación real del usuario: se respaldan antes de
        // tocar nada, se deja un estado CONOCIDO (cola e historial vacíos, español) y se restaura todo al
        // terminar. Sin sembrar ese estado, las pruebas dependerían de con qué se encuentren.
        _settingsBackup = SettingsBackup.CaptureAndReset();

        App = Application.Launch(ResolveExePath());
        Automation = new UIA3Automation();

        MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(30))
            ?? throw new InvalidOperationException("OfiConvert no abrió su ventana principal a tiempo.");
    }

    public void Dispose()
    {
        try { App.Close(); } catch { /* pudo cerrarse ya dentro de un test */ }
        Automation.Dispose();
        App.Dispose();
        _settingsBackup.Restore();
    }

    /// <summary>
    /// Busca el <c>OfiConvert.exe</c> compilado. <c>OFICONVERT_EXE</c> lo fuerza (útil para apuntar a un
    /// publish o a una instalación real).
    /// </summary>
    private static string ResolveExePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("OFICONVERT_EXE");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!File.Exists(overridePath))
                throw new FileNotFoundException($"OFICONVERT_EXE apunta a una ruta que no existe: {overridePath}");
            return overridePath;
        }

        var binFolder = Path.Combine(RepoRoot(), "bin");
        if (!Directory.Exists(binFolder))
            throw new DirectoryNotFoundException(
                $"No existe {binFolder}. Compila la app antes de correr los UI tests: dotnet build OfiConvert.slnx -c Release");

        // Solo los builds con RID (win-x64): son los que llevan el .pri y los idiomas al lado del .exe,
        // o sea, los que arrancan de verdad. De esos, el más reciente.
        var candidate = Directory
            .EnumerateFiles(binFolder, "OfiConvert.exe", SearchOption.AllDirectories)
            .Where(p => p.Contains($"{Path.DirectorySeparatorChar}win-x64{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return candidate
            ?? throw new FileNotFoundException(
                "No se encontró OfiConvert.exe compilado para win-x64. Compila la app antes de correr los UI tests: " +
                "dotnet build OfiConvert.slnx -c Release");
    }

    /// <summary>Sube hasta la carpeta que contiene <c>OfiConvert.slnx</c>.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OfiConvert.slnx")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new DirectoryNotFoundException(
                $"No se encontró la raíz del repositorio (OfiConvert.slnx) desde {AppContext.BaseDirectory}");
    }
}

/// <summary>
/// Una sola instancia de la app para todos los tests: arrancar WinUI cuesta segundos, y la app es de
/// INSTANCIA ÚNICA (una segunda invocación se redirige a la primera y se cierra), así que lanzarla en
/// paralelo desde varias clases de test no funcionaría ni queriendo.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<AppFixture>
{
    public const string Name = "OfiConvert UI";
}
