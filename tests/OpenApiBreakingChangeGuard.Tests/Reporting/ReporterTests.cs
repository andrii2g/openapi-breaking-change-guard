using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Changes;
using OpenApiBreakingChangeGuard.Diagnostics;
using OpenApiBreakingChangeGuard.Reporting;
using Xunit;

namespace OpenApiBreakingChangeGuard.Tests.Reporting;

public sealed class ReporterTests
{
    [Fact]
    public void MarkdownIsDeterministicLfOnlyAndEscapesCode()
    {
        var result = new AnalysisResult(
            "old|`file.json",
            "new.json",
            "3.1.0",
            "3.1.1",
            CompatibilityProfile.Client,
            [new(RuleIds.PathRemoved, ChangeSeverity.Breaking, ChangeCategory.Path, "paths./a`b", "Path removed", "/a`b")],
            [new(DiagnosticSeverity.Warning, "OBCGD999", "paths.a|b", "line one\nline two")]);
        var renderer = new MarkdownReportRenderer();

        var first = renderer.Render(result);
        var second = renderer.Render(result);

        Assert.Equal(first, second);
        Assert.DoesNotContain('\r', first);
        Assert.Contains("``old|`file.json``", first, StringComparison.Ordinal);
        Assert.Contains("``paths./a`b``", first, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultSortsAndDeduplicatesChanges()
    {
        var duplicate = new CompatibilityChange(RuleIds.PathAdded, ChangeSeverity.NonBreaking, ChangeCategory.Path, "paths./z", "added", null, "/z");
        var result = new AnalysisResult(
            "old",
            "new",
            "3.1.0",
            "3.1.0",
            CompatibilityProfile.Client,
            [duplicate, new(RuleIds.PathRemoved, ChangeSeverity.Breaking, ChangeCategory.Path, "paths./a", "removed", "/a"), duplicate],
            []);

        Assert.Equal(2, result.Changes.Count);
        Assert.Equal(ChangeSeverity.Breaking, result.Changes[0].Severity);
    }

    [Fact]
    public void ConsoleLimitDoesNotHideCounts()
    {
        var result = new AnalysisResult(
            "old",
            "new",
            "3.1.0",
            "3.1.0",
            CompatibilityProfile.Client,
            [new(RuleIds.PathRemoved, ChangeSeverity.Breaking, ChangeCategory.Path, "paths./a", "Path removed: /a")],
            []);

        var output = new ConsoleReportRenderer().Render(result, new ConsoleReportOptions { MaxChanges = 0 });

        Assert.Contains("Breaking:     1", output, StringComparison.Ordinal);
        Assert.Contains("1 additional changes omitted", output, StringComparison.Ordinal);
    }
}
