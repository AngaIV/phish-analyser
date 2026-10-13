using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhishAnalyser.Desktop.Models;

namespace PhishAnalyser.Desktop;

/// <summary>
/// Temporary verification harness: renders each bubble state straight to a PNG without
/// showing a window, so the output can be inspected reliably. Not part of the product.
/// </summary>
public static class StateRenderer
{
    public static void RenderAll(string outDir)
    {
        Directory.CreateDirectory(outDir);

        var states = new (string Name, BubbleView View)[]
        {
            ("01-analysing", BubbleViewFactory.Analysing()),
            ("02-noissues", new(BubbleState.NoIssues, 0, "No issues found", "")),
            ("03-warning3", new(BubbleState.Warning, 3, "Worth a closer look", "")),
            ("04-danger7", new(BubbleState.Danger, 7, "Likely phishing", "")),
            ("05-unknown", new(BubbleState.Unknown, 1, "Could not fully check", "")),
        };

        foreach (var (name, view) in states)
        {
            // Off-screen but *shown*: RenderTargetBitmap will not draw a window that was
            // never realised, and the visual tree needs a real presentation source.
            var window = new BubbleWindow
            {
                Left = -2000,
                Top = -2000,
                ShowActivated = false,
            };
            window.Show();
            window.Render(view);
            window.UpdateLayout();

            // Let the render pass settle before capturing.
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            var target = new RenderTargetBitmap(46, 46, 96, 96, PixelFormats.Pbgra32);
            target.Render(window);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(target));
            using (var stream = File.Create(Path.Combine(outDir, name + ".png")))
                encoder.Save(stream);

            window.Close();
        }
    }
}