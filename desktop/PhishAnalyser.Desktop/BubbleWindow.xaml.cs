using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PhishAnalyser.Desktop.Models;

namespace PhishAnalyser.Desktop;

public partial class BubbleWindow : Window
{
    private const double Diameter = 46;
    private const double Radius = 20;          // 40px circle inside a 46px bubble

    // Palette from ui-design/bubble-states/mockup.html
    private static readonly Brush Safe    = Freeze("#0EA47A");
    private static readonly Brush Warn    = Freeze("#E0900B");
    private static readonly Brush Danger  = Freeze("#E5484D");
    private static readonly Brush Muted   = Freeze("#94A3B8");
    private static readonly Brush Unknown = Freeze("#C3CBD6");

    private bool _spinning;

    // Drag state: set on mouse down, updated while the button is held.
    // In screen coordinates, so the moving window cannot skew the delta.
    private Point _cursorStart;
    private Point _windowOrigin;
    private bool _dragging;

    private static Brush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();   // frozen brushes are shareable across threads and cheaper to render
        return brush;
    }

    public BubbleWindow()
    {
        InitializeComponent();
        Render(BubbleViewFactory.Analysing());
    }

    /// <summary>Raised when the user clicks the bubble. The host opens the detail panel.</summary>
    public event EventHandler? BubbleClicked;

    /// <summary>Applies a view to the bubble. Safe to call from any thread.</summary>
    public void Render(BubbleView view)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Render(view));
            return;
        }

        var (colour, glyph, fraction) = view.State switch
        {
            // Green shows "0" — a count — never a tick. See D-011.
            BubbleState.NoIssues  => (Safe,    "0", 1.0),
            BubbleState.Warning   => (Warn,    view.Number.ToString(), ArcFraction(view.Number, 3)),
            BubbleState.Danger    => (Danger,  view.Number.ToString(), ArcFraction(view.Number, 7)),
            BubbleState.Unknown   => (Unknown, "\u2014", 1.0),
            BubbleState.Analysing => (Muted,   "",  0.32),
            BubbleState.Hidden    => (Muted,   "",  0.0),
            _                     => (Unknown, "\u2014", 1.0),   // fail safe on an unknown state
        };

        Glyph.Text = glyph;
        Glyph.Foreground = colour;
        Arc.Stroke = colour;
        Arc.Data = ArcGeometry(fraction);

        // Dashed ring distinguishes "could not analyse" from "nothing found",
        // even in greyscale.
        Arc.StrokeDashArray = view.State == BubbleState.Unknown
            ? new DoubleCollection { 3, 6 }
            : null;

        SetSpinning(view.State == BubbleState.Analysing);
    }

    /// <summary>Scales a count to a ring fraction.</summary>
    private static double ArcFraction(int number, int fullScale) =>
        Math.Clamp(number / (double)fullScale, 0.05, 1.0);

    private void SetSpinning(bool spinning)
    {
        if (spinning == _spinning && spinning)
            return;

        _spinning = spinning;

        if (spinning)
        {
            var sweep = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.15))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            };
            ArcRotation.BeginAnimation(RotateTransform.AngleProperty, sweep);
        }
        else
        {
            ArcRotation.BeginAnimation(RotateTransform.AngleProperty, null);
            ArcRotation.Angle = 0;
        }
    }

    private static Geometry ArcGeometry(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);

        if (fraction >= 1)
        {
            var full = new EllipseGeometry(new Point(Diameter / 2, Diameter / 2), Radius, Radius);
            full.Freeze();
            return full;
        }

        var angle = fraction * 360;
        var figure = new PathFigure
        {
            StartPoint = PointOnCircle(0),
            IsClosed = false,
            IsFilled = false,
        };

        figure.Segments.Add(new ArcSegment(
            PointOnCircle(angle),
            new Size(Radius, Radius),
            0,
            angle > 180,                       // large-arc flag
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    private static Point PointOnCircle(double degrees)
    {
        var radians = (degrees - 90) * Math.PI / 180;
        return new Point(
            Diameter / 2 + Radius * Math.Cos(radians),
            Diameter / 2 + Radius * Math.Sin(radians));
    }

    private void OnBubbleMouseDown(object sender, MouseButtonEventArgs e)
    {
        _cursorStart = CursorPositionInDip(e);
        _windowOrigin = new Point(Left, Top);
        _dragging = false;
        Root.CaptureMouse();
    }

    private void OnBubbleMouseMove(object sender, MouseEventArgs e)
    {
        if (!Root.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            return;

        var cursor = CursorPositionInDip(e);
        var dx = cursor.X - _cursorStart.X;
        var dy = cursor.Y - _cursorStart.Y;

        // Only treat it as a drag past the system threshold, so a slightly shaky click
        // still opens the panel instead of nudging the bubble by a pixel.
        if (!_dragging &&
            Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragging = true;
        Left = Clamp(_windowOrigin.X + dx, SystemParameters.VirtualScreenLeft,
                     SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width);
        Top = Clamp(_windowOrigin.Y + dy, SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height);
    }

    /// <summary>
    /// The cursor's absolute position on the virtual desktop, in device-independent units.
    /// <para>
    /// <c>Left</c>/<c>Top</c> are DIPs and <c>GetPosition(this)</c> is DIPs relative to the
    /// window, so their sum is DIPs too — no physical-pixel conversion, and therefore no
    /// DPI scaling error when the bubble is dragged between monitors at different scales.
    /// Anchoring to an absolute position is also what stops the drift: a window-relative
    /// origin is measured against a frame of reference that is itself moving, so each
    /// mouse-move samples a stale origin and the bubble trails the cursor.
    /// </para>
    /// </summary>
    private Point CursorPositionInDip(MouseEventArgs e)
    {
        var inWindow = e.GetPosition(this);
        return new Point(Left + inWindow.X, Top + inWindow.Y);
    }

    /// <summary>
    /// Keeps the bubble inside the virtual desktop, across all monitors. Without this it
    /// can be dragged off-screen and, since it has no taskbar entry or tray icon yet, would
    /// be unrecoverable without killing the process.
    /// </summary>
    private static double Clamp(double value, double min, double max) =>
        max < min ? min : Math.Clamp(value, min, max);

    private void OnBubbleMouseUp(object sender, MouseButtonEventArgs e)
    {
        Root.ReleaseMouseCapture();

        // A press that never became a drag is a click: open the detail panel.
        if (!_dragging)
            BubbleClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnExitClick(object sender, RoutedEventArgs e) =>
        Application.Current.Shutdown();
}