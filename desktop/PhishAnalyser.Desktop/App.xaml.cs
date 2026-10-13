using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using PhishAnalyser.Desktop.Analysis;
using PhishAnalyser.Desktop.Models;

namespace PhishAnalyser.Desktop;

public partial class App : Application
{
    private BubbleWindow? _bubble;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // `--render-states <dir>` writes one PNG per bubble state and exits. Verification tool.
        if (e.Args.Length >= 2 && e.Args[0] == "--render-states")
        {
            StateRenderer.RenderAll(e.Args[1]);
            Shutdown();
            return;
        }

        StartConsoleCancelHandler();

        _bubble = new BubbleWindow { Left = 120, Top = 120 };
        _bubble.Show();

        // `--headers <file>` analyses a saved header block and renders the real verdict.
        // This is the seam the mail-client surfaces feed: whatever reads the headers, the
        // engine call and the bubble are the same from here on.
        if (e.Args.Length >= 2 && e.Args[0] == "--headers")
        {
            ShowHeadersFile(e.Args[1]);
            return;
        }

        // Development harness: step the five states with 1..5, Esc to quit.
        RunStateHarness(_bubble);
    }

    private void ShowHeadersFile(string path)
    {
        string? block = null;
        try
        {
            if (File.Exists(path))
                block = File.ReadAllText(path);
        }
        catch (Exception)
        {
            block = null;                     // unreadable file is a failed read, not a pass
        }

        _bubble!.Render(HeaderAnalyser.Analyse(block));
        _bubble.KeyDown += (_, key) =>
        {
            if (key.Key == Key.Escape)
                Shutdown();
        };
    }

    private void RunStateHarness(BubbleWindow window)
    {
        var states = new[]
        {
            BubbleViewFactory.Analysing(),
            new BubbleView(BubbleState.NoIssues, 0, "No issues found", ""),
            new BubbleView(BubbleState.Warning, 3, "Worth a closer look", ""),
            new BubbleView(BubbleState.Danger, 7, "Likely phishing", ""),
            new BubbleView(BubbleState.Unknown, 1, "Could not fully check", ""),
        };

        var index = 1;
        window.Render(states[index]);

        window.KeyDown += (_, key) =>
        {
            if (key.Key == Key.Escape)
                Shutdown();
            else if (key.Key is >= Key.D1 and <= Key.D5)
                window.Render(states[key.Key - Key.D1]);
        };

        var cycle = true;
        window.KeyDown += (_, _) => cycle = false;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        timer.Tick += (_, _) =>
        {
            if (!cycle)
                return;
            index = (index + 1) % states.Length;
            window.Render(states[index]);
        };
        timer.Start();
    }

    /// <summary>
    /// A WPF app is a GUI-subsystem process with no console, so Ctrl+C in the launching
    /// terminal never reaches it. This attaches to the parent console, if there is one, so
    /// Ctrl+C and Ctrl+Break shut the app down cleanly instead of requiring Task Manager.
    /// </summary>
    private void StartConsoleCancelHandler()
    {
        if (!AttachConsole(AttachParentProcess))
            return;                           // launched from Explorer: no console, nothing to do

        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;               // we handle it; do not let the runtime kill us
            Dispatcher.Invoke(Shutdown);
        };
    }

    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);
}