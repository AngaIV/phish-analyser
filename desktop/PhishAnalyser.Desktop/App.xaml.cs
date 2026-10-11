using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PhishAnalyser.Desktop.Models;

namespace PhishAnalyser.Desktop;

public partial class App : Application
{
    private readonly List<BubbleWindow> _windows = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Temporary: `--render-states <dir>` writes one PNG per bubble state and exits.
        if (e.Args.Length >= 2 && e.Args[0] == "--render-states")
        {
            StateRenderer.RenderAll(e.Args[1]);
            Shutdown();
            return;
        }

        StartConsoleCancelHandler();

        // Step A harness: ONE bubble, stepped through the five states with the number keys.
        // Replaced by the Outlook host in Step B.
        var states = new[]
        {
            BubbleViewFactory.Analysing(),
            new BubbleView(BubbleState.NoIssues, 0, "No issues found", ""),
            new BubbleView(BubbleState.Warning, 3, "Worth a closer look", ""),
            new BubbleView(BubbleState.Danger, 7, "Likely phishing", ""),
            new BubbleView(BubbleState.Unknown, 1, "Could not fully check", ""),
        };

        var window = new BubbleWindow
        {
            Left = 120,
            Top = 120,
        };
        window.Show();
        _windows.Add(window);

        var index = 1;                       // start on the green state
        window.Render(states[index]);

        // Step with 1..5 so any state can be held and inspected. Esc or Ctrl+Q quits.
        window.KeyDown += (_, key) =>
        {
            if (key.Key == Key.Escape)
            {
                Shutdown();
            }
            else if (key.Key is >= Key.D1 and <= Key.D5)
            {
                index = key.Key - Key.D1;
                window.Render(states[index]);
            }
        };

        // Auto-cycle as well, so the transitions are visible; any key press stops it
        // so a state can be held still.
        var cycle = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            if (!cycle)
                return;
            index = (index + 1) % states.Length;
            window.Render(states[index]);
        };
        timer.Start();
        window.KeyDown += (_, _) => cycle = false;
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