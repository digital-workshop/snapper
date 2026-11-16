using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Snapper;

/// <summary>
/// Eingebauter Selbsttest für Entwickler: "Snapper.exe --selftest ORDNER".
/// Zeichnet per Code in einen Beispiel-Ausschnitt und legt zwei Bilder ab:
/// das fertige Ergebnis und das Editor-Fenster. Es werden keine Mausklicks simuliert.
/// </summary>
internal static class SelfTest
{
    public static void Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        var sample = MakeSample(1000, 400);
        // Fenster weit außerhalb des Bildschirms, damit nichts aufblitzt.
        var fake = new ScreenShot(-32000, 0, 5120, 1440, 1.25, true, sample);
        var ed = new EditorWindow(sample, fake) { ShowActivated = false, ShowInTaskbar = false };
        ed.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                ed.RunScript(outDir);
                App.Log("Selbsttest ok: " + outDir);
            }
            catch (Exception ex)
            {
                App.Log("Selbsttest Fehler: " + ex);
            }
            Application.Current.Shutdown();
        };
        timer.Start();
    }

    private static BitmapSource MakeSample(int w, int h)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0xF4, 0xF6, 0xFB), Color.FromRgb(0xDD, 0xE6, 0xF5), 45), null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x23, 0x27, 0x36)), null, new Rect(0, 0, 220, h));
            for (int i = 0; i < 7; i++)
            {
                var ft = new FormattedText($"Menüpunkt {i + 1}", System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 17, Brushes.White, 1.25);
                dc.DrawText(ft, new Point(24, 22 + i * 48));
            }
            var cols = new[] { "#3B82F6", "#22C55E", "#FFC53D", "#FF2D5F" };
            for (int i = 0; i < 4; i++)
                dc.DrawRoundedRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(cols[i])), null,
                    new Rect(260 + i * 180, 40, 150, 90), 12, 12);
            var title = new FormattedText("Kundenliste: Max Muster, Kontonummer 1234 5678", System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
                FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 22, new SolidColorBrush(Color.FromRgb(0x1B, 0x1D, 0x26)), 1.25);
            dc.DrawText(title, new Point(260, 190));
            var sub = new FormattedText("Das ist ein Beispiel-Ausschnitt für den Selbsttest.", System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, new SolidColorBrush(Color.FromRgb(0x5B, 0x60, 0x70)), 1.25);
            dc.DrawText(sub, new Point(260, 240));
        }
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var conv = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);
        conv.Freeze();
        return conv;
    }
}
