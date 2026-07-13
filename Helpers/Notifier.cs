using System.Runtime.InteropServices;

namespace OfiConvert.Helpers;

/// <summary>
/// Aviso al terminar un lote: sonido del sistema y parpadeo del botón en la barra de tareas.
/// No hace nada si la ventana ya está en primer plano (el panel de resultados es suficiente y
/// no hay a quién avisar).
/// </summary>
public static class Notifier
{
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint uType);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public nint hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_TRAY = 0x00000002;
    private const uint FLASHW_TIMERNOFG = 0x0000000C;
    private const uint MB_ICONASTERISK = 0x00000040;
    private const uint MB_ICONEXCLAMATION = 0x00000030;

    /// <summary>
    /// Avisa de que el lote terminó, salvo que la ventana esté en primer plano.
    /// </summary>
    /// <param name="hWnd">Ventana principal.</param>
    /// <param name="hasErrors">Cambia el sonido: asterisco si todo fue bien, exclamación si hubo fallos.</param>
    public static void NotifyCompleted(nint hWnd, bool hasErrors)
    {
        if (hWnd == nint.Zero || GetForegroundWindow() == hWnd)
            return;

        MessageBeep(hasErrors ? MB_ICONEXCLAMATION : MB_ICONASTERISK);

        // FLASHW_TIMERNOFG parpadea hasta que el usuario atienda la ventana; uCount se ignora con él.
        var info = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = hWnd,
            dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG,
            uCount = 0,
            dwTimeout = 0
        };

        FlashWindowEx(ref info);
    }
}
