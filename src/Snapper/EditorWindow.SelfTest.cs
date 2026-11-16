using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snapper;

public partial class EditorWindow
{
    /// <summary>Spielt eine feste Zeichen-Abfolge ab und speichert Ergebnis und Fenster als PNG.</summary>
    internal void RunScript(string outDir)
    {
        static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        void Arrow(ArrowStyle style, string color, int size, Point a, Point b)
        {
            SetTool(Tool.Arrow);
            _arrowStyle = style;
            SetColor(C(color));
            _sizeIdx = size;
            BeginDraw(a);
            UpdatePreview(new Point(a.X + (b.X - a.X) * 0.4, a.Y + (b.Y - a.Y) * 0.4));
            UpdatePreview(b);
            EndDraw(b);
        }

        // Pfeile in allen Stilen
        Arrow(ArrowStyle.Skitch, "#FF2D5F", 1, new Point(250, 350), new Point(520, 250));
        Arrow(ArrowStyle.Doppel, "#3B82F6", 1, new Point(640, 360), new Point(930, 360));
        Arrow(ArrowStyle.Dick, "#22C55E", 1, new Point(640, 300), new Point(930, 290));
        Arrow(ArrowStyle.Gerade, "#1B1D26", 1, new Point(580, 150), new Point(500, 160));
        Arrow(ArrowStyle.Linie, "#FF2D5F", 1, new Point(940, 160), new Point(990, 100));
        Arrow(ArrowStyle.Skitch, "#FFC53D", 2, new Point(960, 20), new Point(780, 60));

        // Rechteck um die erste Karte
        SetTool(Tool.Rect);
        SetColor(C("#FF2D5F"));
        _sizeIdx = 1;
        BeginDraw(new Point(248, 28));
        UpdatePreview(new Point(300, 80));
        UpdatePreview(new Point(422, 142));
        EndDraw(new Point(422, 142));

        // Verpixeln der "Kontonummer"
        SetTool(Tool.Pixelate);
        _sizeIdx = 1;
        BeginDraw(new Point(590, 184));
        UpdatePreview(new Point(700, 210));
        UpdatePreview(new Point(840, 228));
        EndDraw(new Point(840, 228));

        // Text
        SetTool(Tool.Text);
        SetColor(C("#FF2D5F"));
        _sizeIdx = 1;
        BeginDraw(new Point(270, 330));
        if (_edit != null) _edit.Text = "Hier klicken!";
        CommitText();

        // Rückgängig-Test: Extra-Rechteck zeichnen und wieder entfernen
        int before = _undo.Count;
        SetTool(Tool.Rect);
        BeginDraw(new Point(10, 10)); UpdatePreview(new Point(100, 100)); EndDraw(new Point(100, 100));
        Undo();
        bool undoOk = _undo.Count == before && _redo.Count == 1;
        Redo(); Undo();

        File.WriteAllText(Path.Combine(outDir, "info.txt"),
            $"Elemente: {Anno.Children.Count}\nUndo-Stack: {_undo.Count}\nUndo-Test: {(undoOk ? "ok" : "FEHLER")}\n");

        // Ergebnis in voller Auflösung
        var result = RenderResult();
        using (var fs = File.Create(Path.Combine(outDir, "result.png")))
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(result));
            enc.Save(fs);
        }

        // Datei für "Ziehen"
        string dragFile = WriteDragFile(result);
        File.AppendAllText(Path.Combine(outDir, "info.txt"),
            $"Ziehen-Datei: {(File.Exists(dragFile) ? "ok" : "FEHLT")} {dragFile}\n");

        // Fenster
        UpdateLayout();
        double dpi = Dpi;
        int pw = (int)Math.Round(ActualWidth * dpi), ph = (int)Math.Round(ActualHeight * dpi);
        var rtb = new RenderTargetBitmap(pw, ph, 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
        rtb.Render(this);
        using (var fs = File.Create(Path.Combine(outDir, "window.png")))
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            enc.Save(fs);
        }
    }
}
