using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// El modificador que le decimos a Inno Setup al auto-actualizar.
/// </summary>
/// <remarks>
/// Nace de un bug que <b>solo</b> se vio instalando de verdad: con
/// <c>PrivilegesRequiredOverridesAllowed=dialog</c>, Inno planta el cuadro «Seleccione el modo de
/// instalación» <b>aunque se le pase /VERYSILENT</b>, y ahí se queda esperando un clic — con la app ya
/// cerrada por el propio updater. La cura es mandarle el modo por línea de comandos, y el modo correcto es
/// <b>el que el usuario ya eligió</b>: una actualización no puede mover la app de sitio por sorpresa.
/// </remarks>
public sealed class InstallScopeTests
{
    private const string ProgramFiles = @"C:\Program Files";
    private const string ProgramFilesX86 = @"C:\Program Files (x86)";

    [Theory]
    [InlineData(@"C:\Program Files\OfiConvert")]
    [InlineData(@"C:\Program Files\OfiConvert\")]
    [InlineData(@"C:\Program Files (x86)\OfiConvert")]
    [InlineData(@"c:\program files\oficonvert")]              // no distingue mayúsculas
    public void InstalledUnderProgramFiles_IsPerMachine(string folder)
        => Assert.True(InstallScope.IsPerMachine(folder, ProgramFiles, ProgramFilesX86));

    [Theory]
    [InlineData(@"C:\Users\User\AppData\Local\Programs\OfiConvert")]
    [InlineData(@"D:\Portable\OfiConvert")]
    [InlineData(@"C:\Users\User\Desktop\OfiConvert\bin\Release")]
    public void InstalledAnywhereElse_IsPerUser(string folder)
        => Assert.False(InstallScope.IsPerMachine(folder, ProgramFiles, ProgramFilesX86));

    /// <summary>
    /// El prefijo se compara CON separador final: sin él, una carpeta que solo empieza igual pasaría por
    /// «dentro de Program Files» y la actualización pediría UAC sin necesitarlo — o peor, movería la app.
    /// </summary>
    [Fact]
    public void AFolderThatMerelyStartsTheSame_IsNotProgramFiles()
        => Assert.False(InstallScope.IsPerMachine(@"C:\Program FilesX\OfiConvert", ProgramFiles, ProgramFilesX86));

    [Theory]
    [InlineData(@"C:\Program Files\OfiConvert", "/ALLUSERS")]
    [InlineData(@"C:\Users\User\AppData\Local\Programs\OfiConvert", "/CURRENTUSER")]
    public void TheSwitchPreservesTheScopeTheUserChose(string folder, string expected)
        => Assert.Equal(expected, InstallScope.InnoSwitch(folder, ProgramFiles, ProgramFilesX86));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutAFolder_ItFallsBackToPerUser(string folder)
        => Assert.Equal("/CURRENTUSER", InstallScope.InnoSwitch(folder, ProgramFiles, ProgramFilesX86));

    /// <summary>Nunca puede devolver vacío: sin modificador, vuelve el diálogo que bloquea la instalación.</summary>
    [Fact]
    public void TheSwitchIsNeverEmpty()
    {
        var actual = InstallScope.InnoSwitchForCurrentInstall();

        Assert.True(
            actual is "/ALLUSERS" or "/CURRENTUSER",
            $"Modificador inesperado: '{actual}'. Sin uno de los dos, vuelve el diálogo que bloquea la instalación silenciosa.");
    }
}
