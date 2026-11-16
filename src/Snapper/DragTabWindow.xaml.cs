using System.Windows;
using System.Windows.Input;

namespace Snapper;

/// <summary>Roter Reiter "Ziehen" unter dem Editor. Festhalten und in eine andere App ziehen.</summary>
public partial class DragTabWindow : Window
{
    private readonly EditorWindow _editor;
    private Point _start;
    private bool _armed;

    public DragTabWindow(EditorWindow editor, string sizeText)
    {
        _editor = editor;
        InitializeComponent();
        SizeText.Text = sizeText;
    }

    private void Tab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        _armed = true;
        e.Handled = true;
    }

    private void Tab_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _armed = false;
        e.Handled = true;
    }

    private void Tab_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_armed || e.LeftButton != MouseButtonState.Pressed) return;
        var d = e.GetPosition(this) - _start;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _armed = false;
        _editor.StartFileDrag((UIElement)Content);
    }
}
