using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snapper;

/// <summary>Ein Bildschirm mit seinem eingefrorenen Bild. Alle Maße sind echte Pixel.</summary>
public sealed record ScreenShot(int X, int Y, int Width, int Height, double DpiScale, bool IsPrimary, BitmapSource Image);

internal static class ScreenCapture
{
    /// <summary>Fotografiert jeden Bildschirm einzeln, in voller Auflösung.</summary>
    public static List<ScreenShot> CaptureAll()
    {
        var result = new List<ScreenShot>();
        var infos = new List<(Native.RECT rect, uint dpi, bool primary)>();

        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref Native.RECT r, IntPtr d) =>
        {
            var mi = new Native.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFOEX>() };
            if (Native.GetMonitorInfo(hMon, ref mi))
            {
                uint dpi = 96;
                try { Native.GetDpiForMonitor(hMon, 0, out dpi, out _); } catch { }
                if (dpi == 0) dpi = 96;
                infos.Add((mi.rcMonitor, dpi, (mi.dwFlags & 1) != 0));
            }
            return true;
        }, IntPtr.Zero);

        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        try
        {
            foreach (var (rect, dpi, primary) in infos)
            {
                int w = rect.Right - rect.Left;
                int h = rect.Bottom - rect.Top;
                if (w <= 0 || h <= 0) continue;

                IntPtr memDc = Native.CreateCompatibleDC(screenDc);
                IntPtr hBmp = Native.CreateCompatibleBitmap(screenDc, w, h);
                IntPtr old = Native.SelectObject(memDc, hBmp);
                try
                {
                    Native.BitBlt(memDc, 0, 0, w, h, screenDc, rect.Left, rect.Top, Native.SRCCOPY | Native.CAPTUREBLT);
                    Native.SelectObject(memDc, old);
                    var src = Imaging.CreateBitmapSourceFromHBitmap(hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    var converted = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
                    converted.Freeze();
                    result.Add(new ScreenShot(rect.Left, rect.Top, w, h, dpi / 96.0, primary, converted));
                }
                finally
                {
                    Native.DeleteObject(hBmp);
                    Native.DeleteDC(memDc);
                }
            }
        }
        finally
        {
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
        return result;
    }
}
