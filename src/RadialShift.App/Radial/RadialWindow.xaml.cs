using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;
using static RadialShift.App.Infra.NativeMethods;

namespace RadialShift.App.Radial;

/// <summary>Anel de categorias e ações em volta do cursor (seção 5 da spec).</summary>
public partial class RadialWindow : Window
{
    private const double SectorGapDeg = 1.5;
    private const double LabelWidth = 96;

    private readonly IReadOnlyList<RadialCategory> _categories;
    private readonly int _itemCount;
    private readonly Theme _theme;
    private readonly List<Path> _categoryShapes = new();
    private readonly List<UIElement> _actionElements = new();
    private readonly List<Path> _actionShapes = new();
    private int _openCategory = -1;
    private int _hoverAction = -1;
    private bool _closing;

    /// <summary>Disparado quando o usuário clica numa ação (o anel já está fechando).</summary>
    public event Action<RadialCategory, RadialAction>? ActionChosen;

    public RadialWindow(IReadOnlyList<RadialCategory> categories, int itemCount)
    {
        if (categories.Count is 0 or > RadialGeometry.MaxCategories)
            throw new ArgumentOutOfRangeException(nameof(categories));
        InitializeComponent();
        _categories = categories;
        _itemCount = itemCount;
        _theme = Theme.FromWindows();

        var size = RadialGeometry.OuterRadius * 2;
        Width = Height = Root.Width = Root.Height = size;
        Opacity = 0;

        BuildStatic();

        Root.MouseMove += (_, e) => OnPointer(e.GetPosition(Root));
        Root.MouseLeftButtonUp += (_, e) => OnClick(e.GetPosition(Root));
        KeyDown += (_, e) => { if (e.Key == Key.Escape) CloseRing(); };
        Deactivated += (_, _) => CloseRing();
    }

    private static double C => RadialGeometry.OuterRadius;

    /// <summary>Abre o anel centrado no cursor, deslocando para caber na área de trabalho do monitor.</summary>
    public void ShowAtCursor()
    {
        GetCursorPos(out var cursor);
        var monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref info);
        var scale = GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;

        // Tudo em pixels físicos (app PerMonitorV2).
        var radius = RadialGeometry.OuterRadius * scale;
        var (cx, cy) = RadialGeometry.ClampCenter(cursor.X, cursor.Y, radius,
            info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom);
        int x = (int)Math.Round(cx - radius), y = (int)Math.Round(cy - radius), side = (int)Math.Round(radius * 2);

        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        SetWindowPos(hwnd, IntPtr.Zero, x, y, side, side, SWP_NOZORDER | SWP_NOACTIVATE);
        Show();
        // Ao cruzar monitores com DPI diferente o WPF reajusta a janela; reposiciona no lugar exato.
        SetWindowPos(hwnd, IntPtr.Zero, x, y, side, side, SWP_NOZORDER | SWP_NOACTIVATE);

        ForceForeground(hwnd);
        Activate();
        Focus();
        Animate();
    }

    /// <summary>Fecha sem executar nada.</summary>
    public void CloseRing()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    private void Animate()
    {
        var duration = TimeSpan.FromMilliseconds(150);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var grow = new DoubleAnimation(0.6, 1, duration) { EasingFunction = ease };
        Scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        Scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
    }

    /// <summary>
    /// O Windows só deixa trazer para frente uma janela do processo que recebeu a última entrada.
    /// Como o toque foi no Explorer, anexa temporariamente a fila de entrada à dele.
    /// </summary>
    private static void ForceForeground(IntPtr hwnd)
    {
        var fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var myThread = GetCurrentThreadId();
        var attached = fgThread != 0 && fgThread != myThread && AttachThreadInput(myThread, fgThread, true);
        try
        {
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(myThread, fgThread, false);
        }
    }

    private void BuildStatic()
    {
        // Disco quase invisível até o arco de ações: mantém o mouse "dentro" do anel no vão entre os aros.
        Root.Children.Add(Disc(RadialGeometry.ActionInner, Color.FromArgb(1, 0, 0, 0)));

        var n = _categories.Count;
        var sweep = RadialGeometry.CategorySweep(n);
        for (var i = 0; i < n; i++)
        {
            var center = RadialGeometry.CategoryCenter(i, n);
            var shape = Sector(RadialGeometry.CategoryInner, RadialGeometry.CategoryOuter, center, sweep - SectorGapDeg);
            shape.Fill = _theme.Category;
            shape.Stroke = _theme.Border;
            shape.StrokeThickness = 1;
            _categoryShapes.Add(shape);
            Root.Children.Add(shape);
            Root.Children.Add(Label(_categories[i].Label,
                (RadialGeometry.CategoryInner + RadialGeometry.CategoryOuter) / 2, center, 13, FontWeights.SemiBold));
        }

        var hole = Disc(RadialGeometry.HoleRadius - 4, _theme.Hole.Color);
        hole.Stroke = _theme.Border;
        hole.StrokeThickness = 1;
        Root.Children.Add(hole);
        if (_itemCount > 1)
            Root.Children.Add(Label($"{_itemCount}\narquivos", 0, 0, 12, FontWeights.Normal));
    }

    private void OpenCategory(int index)
    {
        if (index == _openCategory) return;
        foreach (var el in _actionElements) Root.Children.Remove(el);
        _actionElements.Clear();
        _actionShapes.Clear();
        _hoverAction = -1;

        for (var i = 0; i < _categoryShapes.Count; i++)
            _categoryShapes[i].Fill = i == index ? _theme.Highlight : _theme.Category;
        _openCategory = index;
        if (index < 0) return;

        var actions = _categories[index].Actions;
        var k = Math.Min(actions.Count, RadialGeometry.MaxActions);
        var catCenter = RadialGeometry.CategoryCenter(index, _categories.Count);
        for (var j = 0; j < k; j++)
        {
            var center = RadialGeometry.ActionCenter(catCenter, j, k);
            var shape = Sector(RadialGeometry.ActionInner, RadialGeometry.ActionOuter, center,
                RadialGeometry.ActionSweep - SectorGapDeg);
            shape.Fill = _theme.Action;
            shape.Stroke = _theme.Border;
            shape.StrokeThickness = 1;
            var label = Label(actions[j].Label,
                (RadialGeometry.ActionInner + RadialGeometry.ActionOuter) / 2, center, 11, FontWeights.Normal);
            _actionShapes.Add(shape);
            _actionElements.Add(shape);
            _actionElements.Add(label);
            Root.Children.Add(shape);
            Root.Children.Add(label);
        }
    }

    private void HoverAction(int index)
    {
        if (index == _hoverAction) return;
        for (var j = 0; j < _actionShapes.Count; j++)
            _actionShapes[j].Fill = j == index ? _theme.Highlight : _theme.Action;
        _hoverAction = index;
    }

    private RadialHit Hit(Point p)
    {
        var count = _openCategory >= 0 ? _actionShapes.Count : 0;
        return RadialGeometry.HitTest(p.X - C, p.Y - C, _categories.Count, _openCategory, count);
    }

    private void OnPointer(Point p)
    {
        var hit = Hit(p);
        switch (hit.Zone)
        {
            case RadialZone.Category:
                OpenCategory(hit.Index);
                break;
            case RadialZone.Action:
                HoverAction(hit.Index);
                break;
            default:
                HoverAction(-1); // mantém a categoria aberta para dar tempo de chegar ao arco
                break;
        }
    }

    private void OnClick(Point p)
    {
        var hit = Hit(p);
        switch (hit.Zone)
        {
            case RadialZone.Action:
                var category = _categories[_openCategory];
                var action = category.Actions[hit.Index];
                CloseRing();
                ActionChosen?.Invoke(category, action);
                break;
            case RadialZone.Category:
                OpenCategory(hit.Index);
                break;
            default:
                CloseRing();
                break;
        }
    }

    private static Path Sector(double inner, double outer, double centerDeg, double sweepDeg)
    {
        sweepDeg = Math.Min(sweepDeg, 359.9);
        var a0 = centerDeg - sweepDeg / 2;
        var a1 = centerDeg + sweepDeg / 2;
        var large = sweepDeg > 180;

        Point P(double r, double a)
        {
            var (x, y) = RadialGeometry.PointAt(r, a);
            return new Point(C + x, C + y);
        }

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(P(outer, a0), true, true);
            ctx.ArcTo(P(outer, a1), new Size(outer, outer), 0, large, SweepDirection.Clockwise, true, true);
            ctx.LineTo(P(inner, a1), true, true);
            ctx.ArcTo(P(inner, a0), new Size(inner, inner), 0, large, SweepDirection.Counterclockwise, true, true);
        }
        geo.Freeze();
        return new Path { Data = geo };
    }

    private static Ellipse Disc(double radius, Color fill)
    {
        var e = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = new SolidColorBrush(fill) };
        Canvas.SetLeft(e, C - radius);
        Canvas.SetTop(e, C - radius);
        return e;
    }

    private TextBlock Label(string text, double radius, double angleDeg, double fontSize, FontWeight weight)
    {
        var (x, y) = RadialGeometry.PointAt(radius, angleDeg);
        var tb = new TextBlock
        {
            Text = text,
            Width = LabelWidth,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            FontWeight = weight,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = _theme.Text,
            IsHitTestVisible = false,
        };
        tb.Measure(new Size(LabelWidth, double.PositiveInfinity));
        Canvas.SetLeft(tb, C + x - LabelWidth / 2);
        Canvas.SetTop(tb, C + y - tb.DesiredSize.Height / 2);
        return tb;
    }

    private sealed record Theme(SolidColorBrush Category, SolidColorBrush Action, SolidColorBrush Highlight,
        SolidColorBrush Hole, SolidColorBrush Border, SolidColorBrush Text)
    {
        public static Theme FromWindows()
        {
            var light = true;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int v) light = v != 0;
            }
            catch (Exception)
            {
                // Sem acesso ao registro: fica no tema claro.
            }

            return light
                ? new Theme(B(0xF5, 0xF9, 0xF9, 0xF9), B(0xF5, 0xFF, 0xFF, 0xFF), B(0xFF, 0xCC, 0xE4, 0xF7),
                    B(0xF5, 0xEE, 0xEE, 0xEE), B(0x40, 0x00, 0x00, 0x00), B(0xFF, 0x1B, 0x1B, 0x1B))
                : new Theme(B(0xF0, 0x2B, 0x2B, 0x2B), B(0xF0, 0x20, 0x20, 0x20), B(0xFF, 0x00, 0x5A, 0x9E),
                    B(0xF0, 0x1C, 0x1C, 0x1C), B(0x50, 0xFF, 0xFF, 0xFF), B(0xFF, 0xF3, 0xF3, 0xF3));

            static SolidColorBrush B(byte a, byte r, byte g, byte b)
            {
                var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                brush.Freeze();
                return brush;
            }
        }
    }
}
