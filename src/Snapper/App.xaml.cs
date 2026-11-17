using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Snapper;

public partial class App : Application
{
    private readonly List<OverlayWindow> _overlays = new();
    private EditorWindow? _editor;
    private EditorWindow? _hiddenEditor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception.ToString());
            args.Handled = true;
            Shutdown(1);
        };
        int lg = Array.IndexOf(e.Args, "--lang");
        if (lg >= 0 && lg + 1 < e.Args.Length) Loc.Override(e.Args[lg + 1]);
        CleanDragFolder();
        int i = Array.IndexOf(e.Args, "--selftest");
        if (i >= 0 && i + 1 < e.Args.Length)
        {
            int b = Array.IndexOf(e.Args, "--bild");
            SelfTest.Run(e.Args[i + 1], b >= 0 && b + 1 < e.Args.Length ? e.Args[b + 1] : null);
            return;
        }
        BeginCapture();
    }

    /// <summary>Bildschirm einfrieren und Auswahl-Fenster zeigen.</summary>
    public void BeginCapture()
    {
        if (_overlays.Count > 0) return;

        // Zuerst fotografieren, bevor irgendein Fenster von uns auftaucht.
        var shots = ScreenCapture.CaptureAll();
        if (shots.Count == 0) { Shutdown(1); return; }

        foreach (var shot in shots)
        {
            var overlay = new OverlayWindow(shot);
            overlay.Selected += OnSelected;
            overlay.Cancelled += OnCancelled;
            _overlays.Add(overlay);
        }

        Native.GetCursorPos(out var cursor);
        OverlayWindow? active = null;
        foreach (var o in _overlays)
        {
            o.Show();
            var s = o.Shot;
            if (cursor.X >= s.X && cursor.X < s.X + s.Width && cursor.Y >= s.Y && cursor.Y < s.Y + s.Height)
                active = o;
        }
        (active ?? _overlays[0]).Activate();
    }

    /// <summary>Aus dem Editor heraus: neuen Ausschnitt aufnehmen.</summary>
    public void NewCapture(EditorWindow from)
    {
        _hiddenEditor = from;
        from.Hide();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        timer.Tick += (_, _) => { timer.Stop(); BeginCapture(); };
        timer.Start();
    }

    private void OnSelected(ScreenShot shot, Int32Rect rect)
    {
        CloseOverlays();
        var crop = new CroppedBitmap(shot.Image, rect);
        crop.Freeze();

        var old = _editor;
        var editor = new EditorWindow(crop, shot);
        editor.Closed += (s, _) =>
        {
            if (ReferenceEquals(_editor, s))
            {
                _editor = null;
                if (_overlays.Count == 0) Shutdown();
            }
        };
        _editor = editor;
        _hiddenEditor = null;
        editor.Show();
        editor.Activate();
        old?.Close();
    }

    private void OnCancelled()
    {
        CloseOverlays();
        if (_hiddenEditor != null)
        {
            _hiddenEditor.Show();
            _hiddenEditor.Activate();
            _hiddenEditor = null;
        }
        else if (_editor == null)
        {
            Shutdown();
        }
    }

    private void CloseOverlays()
    {
        var list = _overlays.ToList();
        _overlays.Clear();
        foreach (var o in list) o.Close();
    }

    /// <summary>Löscht gezogene Bilder im Temp-Ordner, die älter als 7 Tage sind.</summary>
    private static void CleanDragFolder()
    {
        try
        {
            var dir = EditorWindow.DragFolder;
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "Screenshot_*.png"))
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(f) > TimeSpan.FromDays(7))
                    File.Delete(f);
        }
        catch { }
    }

    public static void Log(string text)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snapper");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "log.txt"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}");
        }
        catch { }
    }
}
