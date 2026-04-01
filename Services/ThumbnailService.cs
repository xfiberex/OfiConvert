using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

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

    private const int SIIGBF_THUMBNAILONLY = 0x02;
    private const int SIIGBF_BIGGERSIZEOK = 0x01;

    public static BitmapSource? GetThumbnail(string filePath, int width = 64, int height = 64)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return null;

            var guid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref guid, out var factory);

            var size = new NativeSize { Width = width, Height = height };
            var hr = factory.GetImage(size, SIIGBF_BIGGERSIZEOK, out var hBitmap);

            if (hr != 0) return null;

            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
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
    }

    public static async Task<BitmapSource?> GetThumbnailAsync(string filePath, int width = 64, int height = 64)
    {
        return await Task.Run(() => GetThumbnail(filePath, width, height));
    }
}
