using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace com.edvardcode.AmazingMarketScanner;

public static class ScreenCapture
{
    public static Bitmap CaptureWindow(IntPtr hwnd)
    {
        GetClientRect(hwnd, out var rect);
        if (rect.Width <= 0 || rect.Height <= 0)
            throw new InvalidOperationException("Окно свёрнуто или не найдено");

        var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);

        var topLeft = new POINT { X = rect.Left, Y = rect.Top };
        ClientToScreen(hwnd, ref topLeft);

        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(topLeft.X, topLeft.Y, 0, 0, new Size(bmp.Width, bmp.Height), CopyPixelOperation.SourceCopy);
        return bmp;
    }

    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }
}