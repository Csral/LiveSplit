using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LiveSplit.Utils;

internal static class LayeredWindowRenderer
{
    public static Bitmap CreateSurface(Size size, float opacity)
    {
        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        // Windows skips zero-alpha pixels when hit testing layered windows, before WM_NCHITTEST.
        // The smallest nonzero alpha keeps the whole rectangle interactive, including resize edges.
        // WS_EX_TRANSPARENT still enables the user's explicit mouse pass-through setting.
        byte constantAlpha = GetConstantAlpha(opacity);
        int minimumAlpha = constantAlpha == 0 ? 1 : (255 + constantAlpha - 1) / constantAlpha;
        graphics.Clear(Color.FromArgb(minimumAlpha, 0, 0, 0));
        return bitmap;
    }

    // UpdateLayeredWindow requires premultiplied BGRA pixels (Format32bppPArgb).
    public static void Draw(IntPtr window, Bitmap bitmap, float opacity)
    {
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        if (dc == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        IntPtr nativeBitmap = IntPtr.Zero;
        IntPtr previousBitmap = IntPtr.Zero;
        try
        {
            nativeBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            previousBitmap = SelectObject(dc, nativeBitmap);
            if (previousBitmap == IntPtr.Zero || previousBitmap == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var source = Point.Empty;
            Size size = bitmap.Size;
            var blend = new BlendFunction
            {
                BlendOp = 0, // AC_SRC_OVER
                BlendFlags = 0,
                SourceConstantAlpha = GetConstantAlpha(opacity),
                AlphaFormat = 1 // AC_SRC_ALPHA
            };
            const uint ULW_ALPHA = 2;
            if (!UpdateLayeredWindow(window, IntPtr.Zero, IntPtr.Zero, ref size,
                dc, ref source, 0, ref blend, ULW_ALPHA))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (previousBitmap != IntPtr.Zero && previousBitmap != new IntPtr(-1))
            {
                SelectObject(dc, previousBitmap);
            }

            if (nativeBitmap != IntPtr.Zero)
            {
                DeleteObject(nativeBitmap);
            }

            DeleteDC(dc);
        }
    }

    private static byte GetConstantAlpha(float opacity)
        => (byte)Math.Round(Math.Max(0, Math.Min(1, opacity)) * 255);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDC,
        IntPtr destination, ref Size size, IntPtr sourceDC, ref Point source,
        uint colorKey, ref BlendFunction blend, uint flags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr dc);
}
