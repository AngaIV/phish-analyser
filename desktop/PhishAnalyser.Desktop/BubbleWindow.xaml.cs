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

    private void OnBubbleClick(object sender, MouseButtonEventArgs e) =>
        BubbleClicked?.Invoke(this, EventArgs.Empty);

    private void OnExitClick(object sender, RoutedEventArgs e) =>
        Application.Current.Shutdown();
}