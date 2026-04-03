using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media.Imaging;

namespace OfiConvert.Services;

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

    public static async Task<BitmapImage?> GetThumbnailAsync(string filePath, int width = 64, int height = 64)
    {
        return await Task.Run(() =>
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
                    // Save HBITMAP to a temp file, then load as BitmapImage
                    var tempPath = Path.Combine(Path.GetTempPath(), $"oficonvert_thumb_{Guid.NewGuid():N}.png");
                    SaveHBitmapToFile(hBitmap, tempPath);
                    return tempPath;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            catch
            {
                return null;
            }
        }).ContinueWith(async task =>
        {
            var tempPath = task.Result;
            if (tempPath is null) return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(tempPath);
                return bitmap;
            }
            catch
            {
                return null;
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }, TaskScheduler.Default).Unwrap();
    }

    [DllImport("ole32.dll")]
    private static extern int CreateStreamOnHGlobal(IntPtr hGlobal, bool fDeleteOnRelease, out IStream ppstm);

    [ComImport, Guid("0000000c-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IStream
    {
        void Read(byte[] pv, int cb, IntPtr pcbRead);
        void Write(byte[] pv, int cb, IntPtr pcbWritten);
    }

    private static void SaveHBitmapToFile(IntPtr hBitmap, string filePath)
    {
        // Use GDI+ to save the HBITMAP as PNG
        using var bitmap = System.Drawing.Image.FromHbitmap(hBitmap);
        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
    }
}
