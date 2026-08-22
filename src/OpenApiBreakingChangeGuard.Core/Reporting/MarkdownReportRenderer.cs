using System.Globalization;
using System.Text;
using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Changes;

namespace OpenApiBreakingChangeGuard.Reporting;

/// <summary>Renders a complete, deterministic Markdown analysis report.</summary>
public sealed class MarkdownReportRenderer
{
    /// <summary>Renders an analysis result using LF line endings and no timestamp.</summary>
    public string Render(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var builder = new StringBuilder();
        builder.Append("# OpenAPI compatibility report\n\n")
            .Append("> **Verdict: ").Append(Verdict(result)).Append("**\n\n");
        RenderInputTable(builder, result);
        builder.Append("| Severity | Count |\n")
            .Append("|---|---:|\n")
            .Append("| Breaking | ").Append(result.BreakingCount.ToString(CultureInfo.InvariantCulture)).Append(" |\n")
            .Append("| NonBreaking | ").Append(result.NonBreakingCount.ToString(CultureInfo.InvariantCulture)).Append(" |\n")
            .Append("| Info | ").Append(result.InfoCount.ToString(CultureInfo.InvariantCulture)).Append(" |\n");

        RenderOperationReview(builder, result.Changes);
        RenderSection(builder, "Breaking changes", ChangeSeverity.Breaking, result.Changes);
        RenderSection(builder, "Non-breaking changes", ChangeSeverity.NonBreaking, result.Changes);
        RenderSection(builder, "Informational changes", ChangeSeverity.Info, result.Changes);
        EnsureBlankLine(builder);
        builder.Append("## Diagnostics\n\n");
        if (result.Diagnostics.Count == 0)
        {
            builder.Append("No diagnostics.\n");
        }
        else
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                builder.Append("### ").Append(diagnostic.Code).Append(" — ")
                    .Append(diagnostic.Severity).Append("\n\n")
                    .Append("- Location: ").Append(MarkdownEscaper.InlineCode(diagnostic.Location)).Append('\n')
                    .Append("- Diagnostic: ").Append(EscapeText(diagnostic.Message)).Append("\n\n");
            }
        }

        EnsureBlankLine(builder);
        builder.Append("## Scope\n\n")
            .Append("This report covers the documented JSON-only OpenAPI 3.0/3.1 compatibility\n")
            .Append("subset. See `docs/limitations.md` for exclusions.\n");
        return builder.ToString();
    }

    private static void RenderInputTable(StringBuilder builder, AnalysisResult result)
    {
        if (result.OldRevision is null && result.NewRevision is null)
        {
            builder.Append("| Input | File | OpenAPI |\n")
                .Append("|---|---|---|\n")
                .Append("| Old | ").Append(MarkdownEscaper.InlineCode(result.OldLabel)).Append(" | ")
                .Append(MarkdownEscaper.InlineCode(result.OldOpenApiVersion)).Append(" |\n")
                .Append("| New | ").Append(MarkdownEscaper.InlineCode(result.NewLabel)).Append(" | ")
                .Append(MarkdownEscaper.InlineCode(result.NewOpenApiVersion)).Append(" |\n\n");
            return;
        }

        builder.Append("| Input | File | Revision | OpenAPI |\n")
            .Append("|---|---|---|---|\n")
            .Append("| Old | ").Append(MarkdownEscaper.InlineCode(result.OldLabel)).Append(" | ")
            .Append(Revision(result.OldRevision, result.OldRevisionUrl)).Append(" | ")
            .Append(MarkdownEscaper.InlineCode(result.OldOpenApiVersion)).Append(" |\n")
            .Append("| New | ").Append(MarkdownEscaper.InlineCode(result.NewLabel)).Append(" | ")
            .Append(Revision(result.NewRevision, result.NewRevisionUrl)).Append(" | ")
            .Append(MarkdownEscaper.InlineCode(result.NewOpenApiVersion)).Append(" |\n\n");
    }

    private static void RenderOperationReview(StringBuilder builder, IReadOnlyList<CompatibilityChange> changes)
    {
        var breaking = changes.Where(static change => change.Severity == ChangeSeverity.Breaking).ToArray();
        var operations = breaking
            .Where(static change => change.Operation is not null)
            .GroupBy(static change => change.Operation!)
            .Select(static group => new
            {
                Operation = group.Key,
                Count = group.Count(),
                RuleIds = group.Select(static change => change.RuleId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            })
            .OrderByDescending(static item => item.Count)
            .ThenBy(static item => item.Operation.Path, StringComparer.Ordinal)
            .ThenBy(static item => item.Operation.Method, StringComparer.Ordinal)
            .ToArray();
        var pathLevelCount = breaking.Count(static change => change.Operation is null);

        EnsureBlankLine(builder);
        builder.Append("## API methods requiring review\n\n");
        if (operations.Length == 0)
        {
            builder.Append("No API methods have breaking findings.\n");
        }
        else
        {
            builder.Append(operations.Length.ToString(CultureInfo.InvariantCulture))
                .Append(operations.Length == 1 ? " API method has" : " API methods have")
                .Append(" breaking findings and should be reviewed before accepting the candidate.\n\n")
                .Append("| Method | Path | Breaking findings | Review focus |\n")
                .Append("|---|---|---:|---|\n");

            foreach (var item in operations)
            {
                var focus = string.Join("<br>", item.RuleIds.Select(ruleId =>
                    $"{MarkdownEscaper.InlineCode(ruleId)} — {MarkdownEscaper.TableCell(RuleName(ruleId))}"));
                builder.Append("| ").Append(MarkdownEscaper.TableCell(MarkdownEscaper.InlineCode(item.Operation.Method.ToUpperInvariant())))
                    .Append(" | ").Append(MarkdownEscaper.TableCell(MarkdownEscaper.InlineCode(item.Operation.Path)))
                    .Append(" | ").Append(item.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(focus).Append(" |\n");
            }
        }

        if (pathLevelCount > 0)
        {
            builder.Append("\n> ").Append(pathLevelCount.ToString(CultureInfo.InvariantCulture))
                .Append(pathLevelCount == 1 ? " additional breaking finding is" : " additional breaking findings are")
                .Append(" path-level and not assigned to a single API method.\n");
        }
    }

    private static void RenderSection(
        StringBuilder builder,
        string heading,
        ChangeSeverity severity,
        IReadOnlyList<CompatibilityChange> changes)
    {
        EnsureBlankLine(builder);
        builder.Append("## ").Append(heading).Append("\n\n");
        var matching = changes.Where(change => change.Severity == severity).ToArray();
        if (matching.Length == 0)
        {
            builder.Append(severity == ChangeSeverity.Info ? "No informational changes.\n" :
                severity == ChangeSeverity.Breaking ? "No breaking changes.\n" : "No non-breaking changes.\n");
            return;
        }

        foreach (var change in matching)
        {
            builder.Append("### ").Append(change.RuleId).Append(" — ").Append(RuleName(change.RuleId)).Append("\n\n")
                .Append("- Location: ").Append(MarkdownEscaper.InlineCode(change.Location)).Append('\n')
                .Append("- Change: ").Append(FormatChange(change)).Append(".\n\n");
        }
    }

    private static string FormatChange(CompatibilityChange change)
    {
        var identity = LastLocationToken(change.Location);
        return change.RuleId switch
        {
            RuleIds.PathRemoved => $"Path removed: {MarkdownEscaper.InlineCode(change.OldValue!)}",
            RuleIds.PathAdded => $"Path added: {MarkdownEscaper.InlineCode(change.NewValue!)}",
            RuleIds.OperationRemoved => $"Operation removed: {MarkdownEscaper.InlineCode(change.OldValue!)}",
            RuleIds.OperationAdded => $"Operation added: {MarkdownEscaper.InlineCode(change.NewValue!)}",
            RuleIds.RequiredParameterAdded => $"Required parameter {MarkdownEscaper.InlineCode(identity)} was added",
            RuleIds.OptionalParameterAdded => $"Optional parameter {MarkdownEscaper.InlineCode(identity)} was added",
            RuleIds.ParameterBecameRequired => $"{ParameterLocation(change.Location)} parameter {MarkdownEscaper.InlineCode(identity)} became required",
            RuleIds.ParameterRemoved => $"Parameter {MarkdownEscaper.InlineCode(identity)} was removed",
            RuleIds.ResponseRemoved => $"Response {MarkdownEscaper.InlineCode(change.OldValue!)} was removed from {MarkdownEscaper.InlineCode(change.Operation!.ToString())}",
            RuleIds.ResponseAdded => $"Response {MarkdownEscaper.InlineCode(change.NewValue!)} was added to {MarkdownEscaper.InlineCode(change.Operation!.ToString())}",
            RuleIds.PropertyRemoved => $"{UsageFromSummary(change.Summary)} property {MarkdownEscaper.InlineCode(identity)} was removed",
            RuleIds.OptionalPropertyAdded => $"Optional {UsageFromSummary(change.Summary).ToLowerInvariant()} property {MarkdownEscaper.InlineCode(identity)} was added",
            RuleIds.RequiredRequestPropertyAdded => $"Required request property {MarkdownEscaper.InlineCode(identity)} was added",
            RuleIds.RequiredResponsePropertyAdded => $"Required response property {MarkdownEscaper.InlineCode(identity)} was added",
            RuleIds.PropertyBecameRequired => $"{UsageFromSummary(change.Summary)} property {MarkdownEscaper.InlineCode(identity)} became {MarkdownEscaper.InlineCode(change.NewValue!)}",
            RuleIds.EnumNarrowed => $"{UsageFromSummary(change.Summary)} enum value {MarkdownEscaper.InlineCode(EnumDisplay(change.OldValue!))} {RemovalPhrase(change)}",
            RuleIds.EnumWidened => $"{UsageFromSummary(change.Summary)} enum value {MarkdownEscaper.InlineCode(EnumDisplay(change.NewValue!))} was added",
            RuleIds.PrimitiveTypeChanged => $"Primitive type changed from {MarkdownEscaper.InlineCode(change.OldValue!)} to {MarkdownEscaper.InlineCode(change.NewValue!)}",
            RuleIds.AlternativeRemoved => $"{UsageFromSummary(change.Summary)} schema alternative {MarkdownEscaper.InlineCode(SummaryTail(change.Summary))} {RemovalPhrase(change)}",
            RuleIds.AlternativeAdded => $"{UsageFromSummary(change.Summary)} schema alternative {MarkdownEscaper.InlineCode(SummaryTail(change.Summary))} was added",
            RuleIds.DeprecationAdded => EscapeText(change.Summary),
            RuleIds.MediaTypeRemoved => $"Media type {MarkdownEscaper.InlineCode(change.OldValue!)} was removed",
            RuleIds.MediaTypeAdded => $"Media type {MarkdownEscaper.InlineCode(change.NewValue!)} was added",
            _ => EscapeText(change.Summary),
        };
    }

    private static string Verdict(AnalysisResult result) => !result.IsComplete
        ? "Analysis incomplete; compatibility could not be established."
        : result.BreakingCount > 0
            ? "Breaking changes detected."
            : "No breaking changes detected.";

    private static string RuleName(string ruleId) => ruleId switch
    {
        RuleIds.PathRemoved => "Path removed",
        RuleIds.OperationRemoved => "Operation removed",
        RuleIds.PathAdded => "Path added",
        RuleIds.OperationAdded => "Operation added",
        RuleIds.RequiredParameterAdded => "Required parameter added",
        RuleIds.OptionalParameterAdded => "Optional parameter added",
        RuleIds.ParameterBecameRequired => "Parameter became required",
        RuleIds.ParameterRemoved => "Parameter removed",
        RuleIds.ResponseRemoved => "Response code removed",
        RuleIds.ResponseAdded => "Response code added",
        RuleIds.PropertyRemoved => "Property removed",
        RuleIds.OptionalPropertyAdded => "Optional property added",
        RuleIds.RequiredRequestPropertyAdded => "Required request property added",
        RuleIds.RequiredResponsePropertyAdded => "Required response property added",
        RuleIds.PropertyBecameRequired => "Existing property became required",
        RuleIds.EnumNarrowed => "Enum narrowed",
        RuleIds.EnumWidened => "Enum widened",
        RuleIds.PrimitiveTypeChanged => "Primitive type changed",
        RuleIds.AlternativeRemoved => "Schema alternative removed",
        RuleIds.AlternativeAdded => "Schema alternative added",
        RuleIds.DeprecationAdded => "Deprecation added",
        RuleIds.MediaTypeRemoved => "Media type removed",
        RuleIds.MediaTypeAdded => "Media type added",
        _ => "Compatibility change",
    };

    private static string EscapeText(string value) => value.Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
    private static string LastLocationToken(string location) => location[(location.LastIndexOf('.') + 1)..];
    private static string ParameterLocation(string location)
    {
        var marker = ".parameters.";
        var start = location.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = location.IndexOf('.', start);
        var value = location[start..end];
        return string.Concat(value[0].ToString().ToUpperInvariant(), value.AsSpan(1));
    }

    private static string UsageFromSummary(string summary) => summary.StartsWith("Request", StringComparison.Ordinal) ? "Request" : "Response";
    private static string RemovalPhrase(CompatibilityChange change) =>
        UsageFromSummary(change.Summary) == "Request" ? "is no longer allowed" : "is no longer returned";
    private static string Revision(string? revision, string? revisionUrl)
    {
        if (revision is null) return "—";
        var label = MarkdownEscaper.InlineCode(revision);
        return revisionUrl is null ? label : $"[{label}](<{revisionUrl}>)";
    }
    private static void EnsureBlankLine(StringBuilder builder)
    {
        if (builder.Length < 2 || builder[^1] != '\n' || builder[^2] != '\n')
        {
            builder.Append('\n');
        }
    }

    private static string SummaryTail(string summary) => summary[(summary.IndexOf(": ", StringComparison.Ordinal) + 2)..].Trim();
    private static string EnumDisplay(string canonical)
    {
        var first = canonical.Split(',')[0];
        var colon = first.IndexOf(':');
        var value = colon < 0 ? first : first[(colon + 1)..];
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }
}
