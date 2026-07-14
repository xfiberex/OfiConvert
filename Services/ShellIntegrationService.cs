using Microsoft.Win32;
using System.Diagnostics;
using OfiConvert.Core;
using Serilog;

namespace OfiConvert.Services;

public static class ShellIntegrationService
{
    private const string MenuName = "OfiConvert";
    private const string MenuText = "Convertir con OfiConvert";

    private static readonly string[] Extensions = OfficeFormats.SupportedExtensions;

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\.docx\shell\{MenuName}");
            return key is not null;
        }
        catch { return false; }
    }

    public static void Register()
    {
        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath)) return;

        foreach (var ext in Extensions)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\shell\{MenuName}");
                key.SetValue("", MenuText);
                key.SetValue("Icon", $"\"{exePath}\"");

                using var cmdKey = key.CreateSubKey("command");
                cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error registrando menú contextual para {Extension}", ext);
            }
        }

        Log.Information("Menú contextual registrado para extensiones Office");
    }

    public static void Unregister()
    {
        foreach (var ext in Extensions)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ext}\shell\{MenuName}", false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error desregistrando menú contextual para {Extension}", ext);
            }
        }

        Log.Information("Menú contextual desregistrado");
    }
}
