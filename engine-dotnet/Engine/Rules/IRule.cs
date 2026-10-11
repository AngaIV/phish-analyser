using PhishAnalyser.Engine.Models;
using MimeKit;

namespace PhishAnalyser.Engine.Rules;

public interface IRule
{
    string RuleId {get;}
    IReadOnlyList<Finding> Evaluate(MimeMessage message);
}
