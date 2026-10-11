namespace PhishAnalyser.Engine.Rules;

///<summary>The rules the engine runs. Adding a rule means adding it here and nowhere else.</summary>
public static class RuleSet
{
    public static IReadOnlyList<IRule> All { get; } = new IRule[]
    {
        new AuthResultsRule(),
        new IdentityMismatchRule(),
        new DisplayNameSpoofRule(),
        new LookalikeDomainRule(),
    };

    ///<summary>Runs every rule over one message and returns all findings in rule order.</summary>
    public static IReadOnlyList<Models.Finding> EvaluateAll(MimeKit.MimeMessage message) =>
        All.SelectMany(rule => rule.Evaluate(message)).ToList();
}