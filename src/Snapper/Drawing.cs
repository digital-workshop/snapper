using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snapper;

public enum ArrowStyle { Skitch, Gerade, Dick, Doppel, Linie }

/// <summary>Baut die Form der Pfeile. Alles in Bildpixeln.</summary>
public static class ArrowGeometryFactory
{
    public static Geometry Create(ArrowStyle style, Point a, Point b, double mult)
    {
        Vector v = b - a;
        double len = v.Length;
        if (len < 1) return Geometry.Empty;
        Vector d = v / len;
        Vector n = new(-d.Y, d.X);

        // Kurze Pfeile werden gleichmäßig kleiner, damit die Spitze nicht größer als der Pfeil ist.
        double m = Math.Min(mult, len / 55.0);
        if (m < 0.15) m = 0.15;

        Point[] pts;
        switch (style)
        {
            case ArrowStyle.Skitch:
            {
                double sw = 14 * m, hw = 21 * m, hl = Math.Min(38 * m, len * 0.55);
                Point bs = b - d * hl;
                pts = new[] { a, bs - n * (sw / 2), bs - n * hw, b, bs + n * hw, bs + n * (sw / 2) };
                break;
            }
            case ArrowStyle.Gerade:
            {
                double sw = 8 * m, hw = 15 * m, hl = Math.Min(28 * m, len * 0.55);
                Point bs = b - d * hl;
                pts = new[] { a - n * (sw / 2), bs - n * (sw / 2), bs - n * hw, b, bs + n * hw, bs + n * (sw / 2), a + n * (sw / 2) };
                break;
            }
            case ArrowStyle.Dick:
            {
                double sw = 18 * m, hw = 31 * m, hl = Math.Min(46 * m, len * 0.6);
                Point bs = b - d * hl;
                pts = new[] { a - n * (sw / 2), bs - n * (sw / 2), bs - n * hw, b, bs + n * hw, bs + n * (sw / 2), a + n * (sw / 2) };
                break;
            }
            case ArrowStyle.Doppel:
            {
                double sw = 8 * m, hw = 15 * m, hl = Math.Min(28 * m, len * 0.4);
                Point ba = a + d * hl, bb = b - d * hl;
                pts = new[]
                {
                    a, ba - n * hw, ba - n * (sw / 2), bb - n * (sw / 2), bb - n * hw,
                    b, bb + n * hw, bb + n * (sw / 2), ba + n * (sw / 2), ba + n * hw
                };
                break;
            }
            default: // Linie
            {
                var line = new LineGeometry(a, b);
                var pen = new Pen(Brushes.Black, Math.Max(3, 7 * m)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                var g = line.GetWidenedPathGeometry(pen);
                g.Freeze();
                return g;
            }
        }

        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(pts[0], true, true);
            ctx.PolyLineTo(pts.Skip(1).ToArray(), true, true);
        }
        sg.Freeze();
        return sg;
    }
}

public static class Pixelator
{
    /// <summary>Verpixelt einen Bereich: Blöcke aus Durchschnittsfarben, nicht umkehrbar.</summary>
    public static BitmapSource Pixelate(BitmapSource source, Int32Rect rect, int block)
    {
        var crop = new CroppedBitmap(source, rect);
        var conv = new FormatConvertedBitmap(crop, PixelFormats.Bgra32, null, 0);
        int w = rect.Width, h = rect.Height, stride = w * 4;
        var src = new byte[stride * h];
        conv.CopyPixels(src, stride, 0);
        var dst = new byte[src.Length];

        block = Math.Max(2, block);
        for (int by = 0; by < h; by += block)
        {
            for (int bx = 0; bx < w; bx += block)
            {
                int bw = Math.Min(block, w - bx), bh = Math.Min(block, h - by);
                long sb = 0, sg = 0, sr = 0;
                for (int y = 0; y < bh; y++)
                {
                    int row = (by + y) * stride + bx * 4;
                    for (int x = 0; x < bw; x++)
                    {
                        int i = row + x * 4;
                        sb += src[i]; sg += src[i + 1]; sr += src[i + 2];
                    }
                }
                int cnt = bw * bh;
                byte b = (byte)(sb / cnt), g = (byte)(sg / cnt), r = (byte)(sr / cnt);
                for (int y = 0; y < bh; y++)
                {
                    int row = (by + y) * stride + bx * 4;
                    for (int x = 0; x < bw; x++)
                    {
                        int i = row + x * 4;
                        dst[i] = b; dst[i + 1] = g; dst[i + 2] = r; dst[i + 3] = 255;
                    }
                }
            }
        }
        var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        wb.WritePixels(new Int32Rect(0, 0, w, h), dst, stride, 0);
        wb.Freeze();
        return wb;
    }
}
