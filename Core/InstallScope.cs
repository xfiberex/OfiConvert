using System.IO;

namespace OfiConvert.Core;

/// <summary>
/// Con qué alcance está instalada la app: <b>para todos los usuarios</b> (bajo <c>Program Files</c>, exige
/// administrador) o <b>solo para este usuario</b> (bajo <c>%LocalAppData%\Programs</c>, sin elevación).
/// </summary>
/// <remarks>
/// Existe por un bug que solo se vio probando el instalador de punta a punta: <c>/VERYSILENT</c>
/// <b>NO es silencioso</b> si el <c>.iss</c> lleva <c>PrivilegesRequiredOverridesAllowed=dialog</c>. Inno
/// planta el diálogo «Seleccione el modo de instalación» **incluso en modo silencioso**, y ahí se queda,
/// bloqueado, esperando un clic que en una actualización automática no va a llegar — con la app ya cerrada.
///
/// La cura es decirle el modo por línea de comandos (<c>/ALLUSERS</c> o <c>/CURRENTUSER</c>), y para eso
/// hay que saber cómo está instalada la app <b>ahora</b>: la actualización debe respetar el alcance que
/// eligió el usuario, no cambiárselo por sorpresa.
/// </remarks>
public static class InstallScope
{
    /// <summary>Modificador de Inno Setup que conserva el alcance de la instalación actual.</summary>
    public static string InnoSwitch(string installFolder, params string[] machineRoots) =>
        IsPerMachine(installFolder, machineRoots) ? "/ALLUSERS" : "/CURRENTUSER";

    /// <summary>¿La app vive bajo una carpeta de máquina (<c>Program Files</c>)?</summary>
    /// <param name="installFolder">Carpeta donde corre la app.</param>
    /// <param name="machineRoots">Raíces "de máquina". Se inyectan para poder probar esto sin depender del equipo.</param>
    public static bool IsPerMachine(string installFolder, params string[] machineRoots)
    {
        if (string.IsNullOrWhiteSpace(installFolder)) return false;

        string full;
        try
        {
            full = Path.GetFullPath(installFolder);
        }
        catch
        {
            return false;   // Una ruta imposible no es una instalación de máquina.
        }

        foreach (var root in machineRoots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;

            var prefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;

            // Con separador final: sin él, "C:\Program Files" daría por buena "C:\Program FilesX\...".
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Las raíces de máquina de ESTE equipo (<c>Program Files</c> y su variante de 32 bits).</summary>
    public static string[] MachineRoots() =>
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        ];

    /// <summary>Modificador para la instalación en la que corre la app ahora mismo.</summary>
    public static string InnoSwitchForCurrentInstall() =>
        InnoSwitch(AppContext.BaseDirectory, MachineRoots());

    /// <summary>
    /// Línea de comandos completa con la que la auto-actualización lanza el instalador.
    /// </summary>
    /// <param name="scopeSwitch"><c>/ALLUSERS</c> o <c>/CURRENTUSER</c> (ver <see cref="InnoSwitch"/>).</param>
    /// <remarks>
    /// Está aquí, y no incrustada en el code-behind, porque cada modificador de esta cadena existe por un
    /// cuelgue ya sufrido y hay que poder probarla:
    /// <list type="bullet">
    ///   <item><c>/VERYSILENT /NORESTART</c> — instalación desatendida, sin reiniciar el equipo.</item>
    ///   <item><b><c>/SUPPRESSMSGBOXES</c></b> — <c>/VERYSILENT</c> <b>no</b> silencia los <c>MsgBox</c>
    ///   del script de Inno. El <c>.iss</c> planta uno cuando no detecta Office, así que el usuario que
    ///   solo tiene <b>LibreOffice</b> —al que la app dice dar soporte— se quedaba con un diálogo que no
    ///   pidió, o con la actualización colgada, y la app ya cerrada (TJ-04, 2026-08-31).</item>
    ///   <item><c>/ALLUSERS</c> o <c>/CURRENTUSER</c> — sin esto, Inno muestra «Seleccione el modo de
    ///   instalación» incluso en silencioso (ver el <c>remarks</c> de esta clase).</item>
    ///   <item><c>/autoinstall=1</c> — se lo lee el <c>[Run]</c> del <c>.iss</c> para relanzar la app.</item>
    /// </list>
    /// El <c>.iss</c> también guarda su propio lado (el aviso va dentro de <c>if not WizardSilent()</c>):
    /// cinturón y tirantes, porque el instalador también se lanza a mano y desde otros sitios.
    /// </remarks>
    public static string SilentInstallArguments(string scopeSwitch) =>
        $"/VERYSILENT /NORESTART /SUPPRESSMSGBOXES {scopeSwitch} /autoinstall=1";
}
