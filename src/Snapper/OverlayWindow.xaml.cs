using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Snapper;

/// <summary>Eingefrorener Bildschirm. Man zieht ein Rechteck auf.</summary>
public partial class OverlayWindow : Window
{
    private bool _dragging;
    private Point _start;
    private Rect _sel = Rect.Empty;

    public ScreenShot Shot { get; }
    public event Action<ScreenShot, Int32Rect>? Selected;
    public event Action? Cancelled;

    public OverlayWindow(ScreenShot shot)
    {
        Shot = shot;
        InitializeComponent();
        ShotImage.Source = shot.Image;
        SizeChanged += (_, _) => UpdateDim();
        Loaded += (_, _) => { ApplyBounds(); UpdateDim(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Cancelled?.Invoke(); };
        MouseRightButtonDown += (_, _) => Cancelled?.Invoke();
        ContentRendered += (_, _) =>
        {
            if (Environment.GetEnvironmentVariable("SNAPPER_TIMING") == "1")
            {
                var ms = (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds;
                App.Log($"Auswahl sichtbar nach {ms:F0} ms");
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyBounds();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(new Action(ApplyBounds));
    }

    /// <summary>Fenster exakt auf die echten Pixel des Bildschirms legen.</summary>
    private void ApplyBounds()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, Shot.X, Shot.Y, Shot.Width, Shot.Height, Native.SWP_SHOWWINDOW);
    }

    private double PixelsPerDip => Root.ActualWidth > 0 ? Shot.Width / Root.ActualWidth : 1;

    private void UpdateDim()
    {
        double w = Root.ActualWidth, h = Root.ActualHeight;
        if (w <= 0 || h <= 0) return;
        Geometry full = new RectangleGeometry(new Rect(0, 0, w, h));
        if (_sel.IsEmpty)
        {
            Dim.Data = full;
            return;
        }
        Dim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, new RectangleGeometry(_sel));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _dragging = true;
        _start = e.GetPosition(Root);
        _sel = Rect.Empty;
        Hint.Visibility = Visibility.Collapsed;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        UpdateSelection(e.GetPosition(Root));
    }

    /// <summary>Für Beispielbilder: eine Auswahl ohne Maus setzen.</summary>
    internal void DemoSelect(Point a, Point b)
    {
        _start = a;
        Hint.Visibility = Visibility.Collapsed;
        UpdateSelection(b);
    }

    private void UpdateSelection(Point p)
    {
        p.X = Math.Clamp(p.X, 0, Root.ActualWidth);
        p.Y = Math.Clamp(p.Y, 0, Root.ActualHeight);
        _sel = new Rect(_start, p);

        SelBorder.Visibility = Visibility.Visible;
        SelBorder.Margin = new Thickness(_sel.X, _sel.Y, 0, 0);
        SelBorder.Width = Math.Max(1, _sel.Width);
        SelBorder.Height = Math.Max(1, _sel.Height);

        double k = PixelsPerDip;
        SizeText.Text = $"{Math.Round(_sel.Width * k)} × {Math.Round(_sel.Height * k)}";
        SizeBadge.Visibility = Visibility.Visible;
        SizeBadge.UpdateLayout();
        double bx = _sel.Right - SizeBadge.ActualWidth;
        double by = _sel.Bottom + 10;
        if (by + SizeBadge.ActualHeight > Root.ActualHeight - 4) by = _sel.Top - SizeBadge.ActualHeight - 10;
        if (by < 4) by = _sel.Top + 10;
        SizeBadge.Margin = new Thickness(Math.Max(4, bx), by, 0, 0);

        UpdateDim();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();

        if (_sel.IsEmpty) return;
        double k = PixelsPerDip;
        int x = (int)Math.Round(_sel.X * k);
        int y = (int)Math.Round(_sel.Y * k);
        int w = (int)Math.Round(_sel.Width * k);
        int h = (int)Math.Round(_sel.Height * k);
        x = Math.Clamp(x, 0, Shot.Width - 1);
        y = Math.Clamp(y, 0, Shot.Height - 1);
        w = Math.Min(w, Shot.Width - x);
        h = Math.Min(h, Shot.Height - y);

        if (w < 5 || h < 5)
        {
            // Zu klein: Auswahl zurücksetzen, nochmal versuchen.
            _sel = Rect.Empty;
            SelBorder.Visibility = Visibility.Collapsed;
            SizeBadge.Visibility = Visibility.Collapsed;
            UpdateDim();
            return;
        }
        Selected?.Invoke(Shot, new Int32Rect(x, y, w, h));
    }
}
