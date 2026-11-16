using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace Snapper;

public enum Tool { Arrow, Text, Rect, Pixelate }

public partial class EditorWindow : Window
{
    // Stärken: dünn, mittel, dick
    private static readonly double[] ArrowMult = { 0.7, 1.0, 1.5 };
    private static readonly double[] StrokeW = { 2.5, 4, 7 };
    private static readonly double[] FontSz = { 20, 28, 44 };
    private static readonly int[] PixBlock = { 8, 14, 24 };

    private static readonly (string Name, string Hex)[] Palette =
    {
        ("Rot", "#FF2D5F"), ("Gelb", "#FFC53D"), ("Grün", "#22C55E"),
        ("Blau", "#3B82F6"), ("Schwarz", "#1B1D26"), ("Weiß", "#FFFFFF"),
    };

    private static readonly DropShadowEffect ArrowShadow = MakeShadow();

    private readonly BitmapSource _base;
    private readonly ScreenShot _monitor;

    private Tool _tool = Tool.Arrow;
    private ArrowStyle _arrowStyle = ArrowStyle.Skitch;
    private readonly SolidColorBrush _brush = new((Color)ColorConverter.ConvertFromString("#FF2D5F"));
    private int _sizeIdx = 1;

    private readonly Stack<UIElement> _undo = new();
    private readonly Stack<UIElement> _redo = new();

    // Zeichnen
    private bool _drawing;
    private Point _start;
    private UIElement? _preview;

    // Text
    private TextBox? _edit;

    // Ansicht
    private double _scale = 1, _tx, _ty;
    private bool _fit = true;
    private bool _panning;
    private Point _panStart;
    private double _panTx, _panTy;

    // Meldung
    private string? _lastSavedPath;

    public EditorWindow(BitmapSource image, ScreenShot monitor)
    {
        _base = image;
        _monitor = monitor;
        InitializeComponent();

        BaseImage.Source = image;
        ContentGrid.Width = image.PixelWidth;
        ContentGrid.Height = image.PixelHeight;
        ZoomLayer.Width = image.PixelWidth;
        ZoomLayer.Height = image.PixelHeight;
        SizeChip.Text = $"{image.PixelWidth} × {image.PixelHeight} px";
        DragSize.Text = $"({image.PixelWidth}×{image.PixelHeight})";

        BuildColors();
        BuildStyles();
        SetTool(Tool.Arrow);
        UpdateUndoButtons();

        PreviewKeyDown += OnWindowKeyDown;
        StateChanged += (_, _) => OnStateChanged();
        CenterCard.SizeChanged += (_, _) =>
            Host.Clip = new RectangleGeometry(new Rect(0, 0, CenterCard.ActualWidth - 2, CenterCard.ActualHeight - 2), 27, 27);

        // Fenstergröße passend zum Ausschnitt
        double dpi = monitor.DpiScale;
        double imgW = image.PixelWidth / dpi, imgH = image.PixelHeight / dpi;
        double monW = monitor.Width / dpi, monH = monitor.Height / dpi;
        Width = Math.Clamp(imgW + 72 + 300 + 14 * 4 + 80, 1000, Math.Max(1000, monW * 0.92));
        Height = Math.Clamp(imgH + 58 + 14 + 110, 660, Math.Max(660, monH * 0.92));

        Opacity = 0;
        Loaded += (_, _) => BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    private static DropShadowEffect MakeShadow()
    {
        var e = new DropShadowEffect { Color = Colors.Black, BlurRadius = 7, ShadowDepth = 2, Direction = 270, Opacity = 0.32 };
        e.Freeze();
        return e;
    }

    // ---------- Fenster ----------

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        // Runde Ecken unter Windows 11
        int round = 2;
        try { Native.DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int)); } catch { }

        // Fenster mittig auf den Bildschirm setzen, auf dem der Ausschnitt entstand
        double dpi = _monitor.DpiScale;
        int w = (int)Math.Round(Width * dpi), h = (int)Math.Round(Height * dpi);
        int x = _monitor.X + (_monitor.Width - w) / 2;
        int y = _monitor.Y + (_monitor.Height - h) / 2;
        Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    private void OnStateChanged()
    {
        bool max = WindowState == WindowState.Maximized;
        MaxBtn.Content = max ? "" : "";
        // Beim Maximieren ragt ein Fenster mit WindowChrome über den Rand. Das gleichen wir aus.
        RootGrid.Margin = max ? new Thickness(8) : new Thickness(0);
    }

    private void Min_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Max_Click(object s, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object s, RoutedEventArgs e) => Close();
    private void New_Click(object s, RoutedEventArgs e) { CommitText(); ((App)Application.Current).NewCapture(this); }

    // ---------- Aufbau der rechten Karte ----------

    private void BuildColors()
    {
        bool first = true;
        foreach (var (name, hex) in Palette)
        {
            var rb = new RadioButton
            {
                Style = (Style)FindResource("SwatchButton"),
                GroupName = "color",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                ToolTip = name,
                Tag = hex,
                IsChecked = first,
            };
            rb.Click += (_, _) => SetColor((Color)ColorConverter.ConvertFromString(hex));
            ColorPanel.Children.Add(rb);
            first = false;
        }
    }

    private void BuildStyles()
    {
        var items = new (ArrowStyle Style, string Name)[]
        {
            (ArrowStyle.Skitch, "Klassisch"), (ArrowStyle.Gerade, "Gerade"), (ArrowStyle.Dick, "Dick"),
            (ArrowStyle.Doppel, "Doppelt"), (ArrowStyle.Linie, "Linie"),
        };
        foreach (var (style, name) in items)
        {
            var geo = ArrowGeometryFactory.Create(style, new Point(10, 36), new Point(58, 14), 0.5);
            var path = new Shapes.Path { Data = geo, Fill = _brush, Width = 68, Height = 48, Stretch = Stretch.None };
            var rb = new RadioButton
            {
                Style = (Style)FindResource("TileButton"),
                GroupName = "arrowstyle",
                Content = new Canvas { Width = 68, Height = 48, Children = { path } },
                ToolTip = name,
                IsChecked = style == ArrowStyle.Skitch,
            };
            rb.Click += (_, _) => _arrowStyle = style;
            StylePanel.Children.Add(rb);
        }
    }

    private void SetColor(Color c)
    {
        _brush.Color = c;
        if (_edit != null)
        {
            _edit.Foreground = new SolidColorBrush(c);
            _edit.CaretBrush = new SolidColorBrush(c);
        }
    }

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && Enum.TryParse<Tool>((string)rb.Tag, out var t)) SetTool(t);
    }

    private void Size_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && int.TryParse((string)rb.Tag, out var i))
        {
            _sizeIdx = i;
            if (_edit != null) _edit.FontSize = FontSz[i];
        }
    }

    private void SetTool(Tool t)
    {
        CommitText();
        _tool = t;
        StyleSection.Visibility = t == Tool.Arrow ? Visibility.Visible : Visibility.Collapsed;
        ColorSection.Visibility = t == Tool.Pixelate ? Visibility.Collapsed : Visibility.Visible;
        SizeLabel.Text = t == Tool.Pixelate ? "KÖRNUNG" : "DICKE";
        ToolTitle.Text = t switch { Tool.Arrow => "Pfeil", Tool.Text => "Text", Tool.Rect => "Rechteck", _ => "Verpixeln" };
        HintText.Text = t switch
        {
            Tool.Arrow => "Maustaste halten und ziehen. Der Pfeil wächst mit. Umschalt = gerade Winkel.",
            Tool.Text => "Ins Bild klicken und tippen. Enter setzt den Text, Umschalt+Enter macht eine neue Zeile.",
            Tool.Rect => "Ziehen, um einen Rahmen zu zeichnen.",
            _ => "Ziehen. Der Bereich wird verpixelt, z. B. für Namen oder Passwörter.",
        };
        Host.Cursor = t == Tool.Text ? Cursors.IBeam : Cursors.Cross;

        // Zeichen auf der Werkzeugleiste mitführen (z. B. bei Tastenkürzel)
        foreach (var rb in FindTools())
            rb.IsChecked = (string)rb.Tag == t.ToString();
    }

    private IEnumerable<RadioButton> FindTools()
    {
        var card = ToolArrow.Parent as Panel;
        return card?.Children.OfType<RadioButton>() ?? Enumerable.Empty<RadioButton>();
    }

    // ---------- Ansicht (Zoom, Verschieben) ----------

    private double Dpi => VisualTreeHelper.GetDpi(this).DpiScaleX;

    private void ApplyView()
    {
        ZoomLayer.RenderTransform = new MatrixTransform(_scale, 0, 0, _scale, _tx, _ty);
        double physical = _scale * Dpi;
        ZoomLabel.Text = $"{Math.Round(physical * 100)} %";
        RenderOptions.SetBitmapScalingMode(BaseImage,
            physical >= 1.5 ? BitmapScalingMode.NearestNeighbor : physical < 1 ? BitmapScalingMode.Fant : BitmapScalingMode.Linear);
    }

    private void FitView()
    {
        double w = Host.ActualWidth, h = Host.ActualHeight;
        if (w <= 0 || h <= 0) return;
        double imgW = _base.PixelWidth, imgH = _base.PixelHeight;
        const double pad = 30;
        double s = Math.Min((w - 2 * pad) / imgW, (h - 2 * pad - 36) / imgH);
        s = Math.Min(s, 1.0 / Dpi);
        s = Math.Max(s, 0.02);
        _scale = s;
        _tx = (w - imgW * s) / 2;
        _ty = (h - imgH * s) / 2 - 8;
        _fit = true;
        ApplyView();
    }

    private void ZoomAt(Point p, double factor)
    {
        double ns = Math.Clamp(_scale * factor, 0.02, 32);
        _tx = p.X - (p.X - _tx) * ns / _scale;
        _ty = p.Y - (p.Y - _ty) * ns / _scale;
        _scale = ns;
        _fit = false;
        ApplyView();
    }

    private void Host_SizeChanged(object s, SizeChangedEventArgs e) { if (_fit) FitView(); }
    private void Fit_Click(object s, RoutedEventArgs e) => FitView();
    private void ZoomIn_Click(object s, RoutedEventArgs e) => ZoomAt(new Point(Host.ActualWidth / 2, Host.ActualHeight / 2), 1.25);
    private void ZoomOut_Click(object s, RoutedEventArgs e) => ZoomAt(new Point(Host.ActualWidth / 2, Host.ActualHeight / 2), 1 / 1.25);

    private void Host_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            ZoomAt(e.GetPosition(Host), Math.Pow(1.0015, e.Delta));
        }
        else
        {
            _fit = false;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) _tx += e.Delta * 0.6;
            else _ty += e.Delta * 0.6;
            ApplyView();
        }
        e.Handled = true;
    }

    private void Host_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        _panning = true;
        _panStart = e.GetPosition(Host);
        _panTx = _tx; _panTy = _ty;
        Host.CaptureMouse();
        Host.Cursor = Cursors.SizeAll;
    }

    private void Host_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || !_panning) return;
        _panning = false;
        Host.ReleaseMouseCapture();
        Host.Cursor = _tool == Tool.Text ? Cursors.IBeam : Cursors.Cross;
    }

    // ---------- Zeichnen ----------

    private Brush NewBrush() => new SolidColorBrush(_brush.Color).GetAsFrozen() as Brush ?? new SolidColorBrush(_brush.Color);

    private void Host_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginDraw(e.GetPosition(Anno));
        e.Handled = true;
    }

    /// <summary>Anfang einer Zeichnung. Punkt in Bildpixeln.</summary>
    internal void BeginDraw(Point p)
    {
        if (_tool == Tool.Text)
        {
            CommitText();
            BeginText(p);
            return;
        }
        CommitText();

        _drawing = true;
        _start = p;
        Host.CaptureMouse();

        switch (_tool)
        {
            case Tool.Arrow:
                _preview = new Shapes.Path { Fill = NewBrush(), Effect = ArrowShadow, IsHitTestVisible = false };
                break;
            case Tool.Rect:
                _preview = new Shapes.Rectangle
                {
                    Stroke = NewBrush(), StrokeThickness = StrokeW[_sizeIdx], RadiusX = 4, RadiusY = 4, IsHitTestVisible = false,
                };
                break;
            default:
                _preview = new Shapes.Rectangle
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x2D, 0x5F)), StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 }, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0x2D, 0x5F)),
                    IsHitTestVisible = false,
                };
                break;
        }
        Anno.Children.Add(_preview);
        UpdatePreview(p);
    }

    private void Host_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panning)
        {
            var cur = e.GetPosition(Host);
            _tx = _panTx + (cur.X - _panStart.X);
            _ty = _panTy + (cur.Y - _panStart.Y);
            _fit = false;
            ApplyView();
            return;
        }
        if (_drawing) UpdatePreview(e.GetPosition(Anno));
    }

    internal void UpdatePreview(Point p)
    {
        if (_preview == null) return;
        switch (_preview)
        {
            case Shapes.Path path:
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    var v = p - _start;
                    double ang = Math.Round(Math.Atan2(v.Y, v.X) / (Math.PI / 4)) * (Math.PI / 4);
                    p = _start + new Vector(Math.Cos(ang), Math.Sin(ang)) * v.Length;
                }
                path.Data = ArrowGeometryFactory.Create(_arrowStyle, _start, p, ArrowMult[_sizeIdx]);
                break;
            }
            case Shapes.Rectangle rect:
            {
                var r = new Rect(_start, p);
                Canvas.SetLeft(rect, r.X);
                Canvas.SetTop(rect, r.Y);
                rect.Width = r.Width;
                rect.Height = r.Height;
                break;
            }
        }
    }

    private void Host_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndDraw(e.GetPosition(Anno));

    /// <summary>Ende einer Zeichnung. Punkt in Bildpixeln.</summary>
    internal void EndDraw(Point end)
    {
        if (!_drawing) return;
        _drawing = false;
        Host.ReleaseMouseCapture();
        var el = _preview;
        _preview = null;
        if (el == null) return;

        switch (el)
        {
            case Shapes.Path path:
                if (path.Data == null || path.Data.IsEmpty() || (end - _start).Length < 8) Anno.Children.Remove(path);
                else RegisterAdded(path);
                break;

            case Shapes.Rectangle rect when _tool == Tool.Rect:
                if (rect.Width < 3 || rect.Height < 3) Anno.Children.Remove(rect);
                else RegisterAdded(rect);
                break;

            case Shapes.Rectangle rect:
                Anno.Children.Remove(rect);
                AddPixelate(new Rect(_start, end));
                break;
        }
    }

    private void AddPixelate(Rect area)
    {
        int x = Math.Max(0, (int)Math.Floor(area.X));
        int y = Math.Max(0, (int)Math.Floor(area.Y));
        int r = Math.Min(_base.PixelWidth, (int)Math.Ceiling(area.Right));
        int b = Math.Min(_base.PixelHeight, (int)Math.Ceiling(area.Bottom));
        int w = r - x, h = b - y;
        if (w < 4 || h < 4) return;

        var bmp = Pixelator.Pixelate(_base, new Int32Rect(x, y, w, h), PixBlock[_sizeIdx]);
        var img = new Image { Source = bmp, Width = w, Height = h, Stretch = Stretch.Fill, IsHitTestVisible = false };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
        Canvas.SetLeft(img, x);
        Canvas.SetTop(img, y);
        Anno.Children.Add(img);
        RegisterAdded(img);
    }

    // ---------- Text ----------

    private void BeginText(Point p)
    {
        double size = FontSz[_sizeIdx];
        var brush = new SolidColorBrush(_brush.Color);
        var box = new TextBox
        {
            FontSize = size,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            Foreground = brush,
            CaretBrush = brush,
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x70, 0x60, 0x68, 0x80)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(3),
            AcceptsReturn = true,
            MinWidth = 60,
            TextWrapping = TextWrapping.NoWrap,
        };
        Canvas.SetLeft(box, p.X - 4);
        Canvas.SetTop(box, p.Y - size * 0.75);
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { e.Handled = true; CommitText(); }
            else if (e.Key == Key.Escape) { e.Handled = true; CancelText(); }
        };
        box.LostKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(new Action(CommitText), DispatcherPriority.Background);

        _edit = box;
        Anno.Children.Add(box);
        Dispatcher.BeginInvoke(new Action(() => { box.Focus(); Keyboard.Focus(box); }), DispatcherPriority.Input);
    }

    private void CommitText()
    {
        var box = _edit;
        if (box == null) return;
        _edit = null;
        if (string.IsNullOrWhiteSpace(box.Text))
        {
            Anno.Children.Remove(box);
            return;
        }
        box.BorderThickness = new Thickness(0);
        box.Padding = new Thickness(4);
        box.Focusable = false;
        box.IsHitTestVisible = false;
        box.Cursor = Cursors.Arrow;
        Keyboard.ClearFocus();
        RegisterAdded(box);
    }

    private void CancelText()
    {
        var box = _edit;
        if (box == null) return;
        _edit = null;
        Anno.Children.Remove(box);
        Keyboard.ClearFocus();
    }

    // ---------- Rückgängig / Wiederholen ----------

    private void RegisterAdded(UIElement el)
    {
        _undo.Push(el);
        _redo.Clear();
        UpdateUndoButtons();
    }

    private void UpdateUndoButtons()
    {
        UndoBtn.IsEnabled = _undo.Count > 0;
        RedoBtn.IsEnabled = _redo.Count > 0;
    }

    private void Undo()
    {
        CommitText();
        if (_undo.Count == 0) return;
        var el = _undo.Pop();
        Anno.Children.Remove(el);
        _redo.Push(el);
        UpdateUndoButtons();
    }

    private void Redo()
    {
        CommitText();
        if (_redo.Count == 0) return;
        var el = _redo.Pop();
        Anno.Children.Add(el);
        _undo.Push(el);
        UpdateUndoButtons();
    }

    private void Undo_Click(object s, RoutedEventArgs e) => Undo();
    private void Redo_Click(object s, RoutedEventArgs e) => Redo();

    // ---------- Tastatur ----------

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_edit != null) return; // Der Text-Kasten regelt seine Tasten selbst.
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.Z: if (shift) Redo(); else Undo(); e.Handled = true; break;
                case Key.Y: Redo(); e.Handled = true; break;
                case Key.C: CopyImage(); e.Handled = true; break;
                case Key.S: SaveImage(); e.Handled = true; break;
                case Key.N: ((App)Application.Current).NewCapture(this); e.Handled = true; break;
                case Key.D0: case Key.NumPad0: FitView(); e.Handled = true; break;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.Escape: Close(); e.Handled = true; break;
            case Key.A: SetTool(Tool.Arrow); e.Handled = true; break;
            case Key.T: SetTool(Tool.Text); e.Handled = true; break;
            case Key.R: SetTool(Tool.Rect); e.Handled = true; break;
            case Key.P: SetTool(Tool.Pixelate); e.Handled = true; break;
        }
    }

    // ---------- Kopieren und Speichern ----------

    /// <summary>Bild plus Zeichnungen in voller Auflösung (1 Bildpixel = 1 Pixel).</summary>
    private BitmapSource RenderResult()
    {
        CommitText();
        // Frisch Gezeichnetes muss erst gemessen und angeordnet sein, sonst fehlt es im Export.
        ContentGrid.UpdateLayout();
        int w = _base.PixelWidth, h = _base.PixelHeight;
        var brush = new VisualBrush(ContentGrid)
        {
            Stretch = Stretch.Fill,
            Viewbox = new Rect(0, 0, w, h),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawRectangle(brush, null, new Rect(0, 0, w, h));
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    // ---------- Ziehen (Bild als Datei in andere Programme ziehen) ----------

    private Point _dragStart;
    private bool _dragArmed;

    private void Drag_MouseDown(object s, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragArmed = true;
        DragPill.CaptureMouse();
    }

    private void Drag_MouseUp(object s, MouseButtonEventArgs e)
    {
        _dragArmed = false;
        DragPill.ReleaseMouseCapture();
    }

    private void Drag_MouseMove(object s, MouseEventArgs e)
    {
        if (!_dragArmed || e.LeftButton != MouseButtonState.Pressed) return;
        var d = e.GetPosition(this) - _dragStart;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _dragArmed = false;
        DragPill.ReleaseMouseCapture();
        try
        {
            var bmp = RenderResult();
            string file = WriteDragFile(bmp);

            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { file });
            data.SetImage(bmp);
            DragDrop.DoDragDrop(DragPill, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            App.Log(ex.ToString());
            ShowToast("Ziehen hat nicht geklappt", null);
        }
    }

    /// <summary>PNG im Temp-Ordner. Alte Dateien (über 7 Tage) räumt Snapper beim Start auf.</summary>
    internal static string DragFolder =>
        Path.Combine(Path.GetTempPath(), "Snapper");

    private static string WriteDragFile(BitmapSource bmp)
    {
        Directory.CreateDirectory(DragFolder);
        string file = Path.Combine(DragFolder, $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png");
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(file);
        enc.Save(fs);
        return file;
    }

    private void Copy_Click(object s, RoutedEventArgs e) => CopyImage();
    private void Save_Click(object s, RoutedEventArgs e) => SaveImage();

    private void CopyImage()
    {
        try
        {
            var bmp = RenderResult();
            var png = new MemoryStream();
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            enc.Save(png);

            var data = new DataObject();
            data.SetImage(bmp);
            data.SetData("PNG", png);

            for (int i = 0; ; i++)
            {
                try { Clipboard.SetDataObject(data, true); break; }
                catch (COMException) when (i < 6) { Thread.Sleep(60); }
            }
            ShowToast("In die Zwischenablage kopiert", null);
        }
        catch (Exception ex)
        {
            App.Log(ex.ToString());
            ShowToast("Kopieren hat nicht geklappt", null);
        }
    }

    private void SaveImage()
    {
        try
        {
            var bmp = RenderResult();
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
            Directory.CreateDirectory(dir);
            string name = $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            string file = Path.Combine(dir, name + ".png");
            for (int i = 2; File.Exists(file); i++) file = Path.Combine(dir, $"{name}_{i}.png");

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(file)) enc.Save(fs);

            _lastSavedPath = file;
            ShowToast("Gespeichert in Bilder\\Screenshots  ·  Klick öffnet den Ordner", file);
        }
        catch (Exception ex)
        {
            App.Log(ex.ToString());
            ShowToast("Speichern hat nicht geklappt", null);
        }
    }

    private void ShowToast(string text, string? path)
    {
        ToastText.Text = text;
        _lastSavedPath = path;
        Toast.IsHitTestVisible = path != null;
        Toast.Cursor = path != null ? Cursors.Hand : Cursors.Arrow;

        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromMilliseconds(160)));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromMilliseconds(path != null ? 3200 : 1900)));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(path != null ? 3500 : 2200)));
        Toast.BeginAnimation(OpacityProperty, anim);
    }

    private void Toast_Click(object sender, MouseButtonEventArgs e)
    {
        if (_lastSavedPath == null || !File.Exists(_lastSavedPath)) return;
        try { Process.Start("explorer.exe", $"/select,\"{_lastSavedPath}\""); } catch { }
    }
}
