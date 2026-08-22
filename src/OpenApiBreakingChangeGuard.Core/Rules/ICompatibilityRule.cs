using OpenApiBreakingChangeGuard.Analysis;

namespace OpenApiBreakingChangeGuard.Rules;

internal interface ICompatibilityRule<in TContext>
{
    string RuleId { get; }

    void Evaluate(
        TContext context,
        ChangeCollector changes,
        DiagnosticCollector diagnostics,
        CancellationToken cancellationToken);
}
