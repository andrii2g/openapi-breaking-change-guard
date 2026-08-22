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
            [new(DiagnosticSeverity.Warning, "OBCGD999", "paths.a|b", "line one\nline two")],
            "old|revision",
            "new`revision",
            "https://github.com/example/api/blob/old/openapi.json",
            "https://github.com/example/api/blob/new/openapi.json");
        var renderer = new MarkdownReportRenderer();

        var first = renderer.Render(result);
        var second = renderer.Render(result);

        Assert.Equal(first, second);
        Assert.DoesNotContain('\r', first);
        Assert.Contains("``old|`file.json``", first, StringComparison.Ordinal);
        Assert.Contains("[`old|revision`](<https://github.com/example/api/blob/old/openapi.json>)", first, StringComparison.Ordinal);
        Assert.Contains("[``new`revision``](<https://github.com/example/api/blob/new/openapi.json>)", first, StringComparison.Ordinal);
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

    [Fact]
    public void MarkdownGroupsBreakingFindingsByApiMethod()
    {
        var widgets = new OpenApiBreakingChangeGuard.Documents.OperationKey("/widgets", "post");
        var health = new OpenApiBreakingChangeGuard.Documents.OperationKey("/health", "get");
        var result = new AnalysisResult(
            "old",
            "new",
            "3.1.0",
            "3.1.0",
            CompatibilityProfile.Client,
            [
                new(RuleIds.AlternativeRemoved, ChangeSeverity.Breaking, ChangeCategory.Schema, "paths./widgets.post.requestBody.schema", "Request schema alternative no longer allowed: primitive:string", "primitive:string", operation: widgets),
                new(RuleIds.PropertyRemoved, ChangeSeverity.Breaking, ChangeCategory.Schema, "paths./widgets.post.requestBody.schema.properties.name", "Request property removed: name", "name", operation: widgets),
                new(RuleIds.OperationRemoved, ChangeSeverity.Breaking, ChangeCategory.Operation, "paths./health.get", "Operation removed: GET /health", "GET /health", operation: health),
                new(RuleIds.PathRemoved, ChangeSeverity.Breaking, ChangeCategory.Path, "paths./legacy", "Path removed: /legacy", "/legacy"),
            ],
            []);

        var output = new MarkdownReportRenderer().Render(result);

        var widgetsRow = "| `POST` | `/widgets` | 2 | `OBCG011` — Property removed<br>`OBCG019` — Schema alternative removed |";
        var healthRow = "| `GET` | `/health` | 1 | `OBCG002` — Operation removed |";
        Assert.Contains("2 API methods have breaking findings", output, StringComparison.Ordinal);
        Assert.Contains(widgetsRow, output, StringComparison.Ordinal);
        Assert.Contains(healthRow, output, StringComparison.Ordinal);
        Assert.True(output.IndexOf(widgetsRow, StringComparison.Ordinal) < output.IndexOf(healthRow, StringComparison.Ordinal));
        Assert.Contains("1 additional breaking finding is path-level", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RevisionUrlRequiresRevisionAndSafeHttpsUrl()
    {
        Assert.Throws<ArgumentException>(() => new AnalysisResult(
            "old", "new", "3.1.0", "3.1.0", CompatibilityProfile.Client, [], [],
            oldRevisionUrl: "javascript:alert(1)"));
    }

    [Fact]
    public void DirectionalRemovalWordingDistinguishesRequestsAndResponses()
    {
        var result = new AnalysisResult(
            "old",
            "new",
            "3.1.0",
            "3.1.0",
            CompatibilityProfile.Client,
            [
                new(RuleIds.AlternativeRemoved, ChangeSeverity.Breaking, ChangeCategory.Schema, "paths./x.post.requestBody.schema", "Request schema alternative no longer allowed: primitive:string", "primitive:string"),
                new(RuleIds.AlternativeRemoved, ChangeSeverity.NonBreaking, ChangeCategory.Schema, "paths./x.get.responses.200.schema", "Response schema alternative no longer allowed: primitive:string", "primitive:string"),
            ],
            []);

        var output = new MarkdownReportRenderer().Render(result);

        Assert.Contains("Request schema alternative `primitive:string` is no longer allowed", output, StringComparison.Ordinal);
        Assert.Contains("Response schema alternative `primitive:string` is no longer returned", output, StringComparison.Ordinal);
    }
}
