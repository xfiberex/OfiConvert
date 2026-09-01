using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace OfiConvert.Services;

/// <summary>
/// Miniatura de un documento, tal y como la ve el Explorador (shell).
/// </summary>
/// <remarks>
/// La versión anterior guardaba un PNG temporal, se lo pasaba a <c>BitmapImage.UriSource</c> —que carga
/// de forma <b>asíncrona</b>— y borraba el archivo en el <c>finally</c> inmediato. Una carrera que se
/// perdía en los dos sentidos: si ganaba el borrado, la imagen no cargaba; si ganaba la carga, el borrado
/// fallaba y los <c>oficonvert_thumb_*.png</c> se acumulaban en <c>%TEMP%</c> para siempre. Encima el
/// <c>BitmapImage</c> se construía en un <c>ContinueWith(..., TaskScheduler.Default)</c>, o sea **fuera
/// del hilo de UI**, donde WinUI no permite crearlo — y el <c>catch { return null; }</c> se tragaba el
/// fallo, así que la lista se quedaba sin miniaturas y nadie se enteraba. (TJ-14, 2026-09-01.)
///
/// Ahora el disco no se toca: el trabajo pesado (shell + GDI+) devuelve **bytes**, y el
/// <c>BitmapImage</c> se crea en el hilo que llama —la UI— sobre un flujo en memoria.
/// </remarks>
public static class ThumbnailService
{
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid, out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    private const int SIIGBF_BIGGERSIZEOK = 0x01;

    /// <summary>
    /// Miniatura lista para pintar. <b>Llamar desde el hilo de UI</b>: es donde se construye el
    /// <see cref="BitmapImage"/>.
    /// </summary>
    public static async Task<BitmapImage?> GetThumbnailAsync(string filePath, int width = 64, int height = 64)
    {
        // ConfigureAwait(true) explícito, aunque sea el valor por defecto: que se vuelva al hilo de UI no
        // es un detalle de estilo aquí, es la condición para que el BitmapImage se pueda crear.
        byte[]? png = await GetThumbnailBytesAsync(filePath, width, height).ConfigureAwait(true);
        if (png is null || png.Length == 0) return null;

        try
        {
            using var stream = new InMemoryRandomAccessStream();

            var writer = new DataWriter(stream.GetOutputStreamAt(0));
            writer.WriteBytes(png);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();

            stream.Seek(0);

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception ex)
        {
            // Se registra: el catch mudo de antes es lo que dejó pasar meses sin miniaturas y sin señal.
            Serilog.Log.Warning(ex, "Miniatura: no se pudo construir la imagen de {File}", filePath);
            return null;
        }
    }

    /// <summary>
    /// El trabajo pesado, sin nada de UI: pide la miniatura al shell y la devuelve como PNG en memoria.
    /// </summary>
    /// <remarks>Separado para poder probarlo sin hilo de UI, que es donde estaba el fallo invisible.</remarks>
    public static Task<byte[]?> GetThumbnailBytesAsync(string filePath, int width = 64, int height = 64)
    {
        return Task.Run<byte[]?>(() =>
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                var guid = typeof(IShellItemImageFactory).GUID;
                SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref guid, out var factory);

                var size = new NativeSize { Width = width, Height = height };
                var hr = factory.GetImage(size, SIIGBF_BIGGERSIZEOK, out var hBitmap);
                if (hr != 0) return null;

                try
                {
                    using var bitmap = System.Drawing.Image.FromHbitmap(hBitmap);
                    using var memory = new MemoryStream();
                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    return memory.ToArray();
                }
                finally
                {
                    // El HBITMAP es nuestro desde GetImage: si no se libera, se va la memoria de GDI.
                    DeleteObject(hBitmap);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Miniatura: el shell no dio imagen de {File}", filePath);
                return null;
            }
        });
    }
}
