using System.Windows;
using PhishAnalyser.Engine.Models;

namespace PhishAnalyser.Desktop;

public partial class FindingsWindow : Window
{
    public FindingsWindow(IReadOnlyList<Finding> findings)
    {
        InitializeComponent();

        var unknown = findings.Any(f => f.Status == RuleStatus.Unknown);
        var inconclusive = findings.Any(f => f.Status == RuleStatus.Inconclusive);
        Headline.Text = unknown
            ? "Could not fully check this message"
            : inconclusive
                ? "Some checks were not reported"
                : "Analysis details";

        Summary.Text = findings.Count == 0
            ? "No rule results are available."
            : "These are the checks run on this message. A pass is not proof that a message is safe.";

        FindingsList.ItemsSource = findings.Select(f => new FindingRow(
            $"{f.RuleId} — {f.Status}", f.Explanation)).ToList();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private sealed record FindingRow(string Title, string Description);
}
