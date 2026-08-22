using System.Globalization;
using System.Text;
using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Changes;

namespace OpenApiBreakingChangeGuard.Reporting;

/// <summary>Renders a compact, deterministic plain-text analysis report.</summary>
public sealed class ConsoleReportRenderer
{
    /// <summary>Renders an analysis result without ANSI control sequences.</summary>
    public string Render(AnalysisResult result, ConsoleReportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        options ??= new ConsoleReportOptions();
        if (options.MaxChanges is < 0 or > 10_000)
        {
            throw new ArgumentException("The console change limit must be between 0 and 10,000.", nameof(options));
        }

        var builder = new StringBuilder();
        builder.Append("OpenAPI Breaking Change Guard\n\n");
        AppendInput(builder, "Old", result.OldLabel, result.OldRevision, result.OldOpenApiVersion);
        AppendInput(builder, "New", result.NewLabel, result.NewRevision, result.NewOpenApiVersion);
        builder.Append("Profile: ").Append(result.Profile.ToString().ToLowerInvariant()).Append('\n')
            .Append("Analysis: ").Append(result.IsComplete ? "complete" : "incomplete").Append("\n\n")
            .Append("Breaking:     ").Append(result.BreakingCount.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("NonBreaking:  ").Append(result.NonBreakingCount.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("Info:         ").Append(result.InfoCount.ToString(CultureInfo.InvariantCulture)).Append('\n');

        var visible = result.Changes
            .Where(change => options.IncludeInfo || change.Severity != ChangeSeverity.Info)
            .ToArray();
        var rendered = visible.Take(options.MaxChanges).ToArray();
        foreach (var change in rendered)
        {
            builder.Append('\n')
                .Append(SeverityLabel(change.Severity).PadRight(11))
                .Append(' ').Append(change.RuleId).Append("  ").Append(change.Location).Append('\n')
                .Append("  ").Append(change.Summary).Append('\n');
        }

        if (visible.Length > rendered.Length)
        {
            builder.Append('\n').Append("... ")
                .Append((visible.Length - rendered.Length).ToString(CultureInfo.InvariantCulture))
                .Append(" additional changes omitted from console output.\n")
                .Append("Use --report <path> for the complete report.\n");
        }

        if (result.Diagnostics.Count > 0)
        {
            builder.Append("\nDiagnostics:\n");
            foreach (var diagnostic in result.Diagnostics)
            {
                builder.Append(diagnostic.Severity.ToString().ToUpperInvariant())
                    .Append(' ').Append(diagnostic.Code).Append(' ').Append(diagnostic.Location).Append('\n')
                    .Append("  ").Append(diagnostic.Message).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static void AppendInput(StringBuilder builder, string kind, string label, string? revision, string openApiVersion)
    {
        builder.Append(kind).Append(": ").Append(label);
        if (revision is not null) builder.Append(" @ ").Append(revision);
        builder.Append(" (OpenAPI ").Append(openApiVersion).Append(")\n");
    }

    private static string SeverityLabel(ChangeSeverity severity) => severity switch
    {
        ChangeSeverity.Breaking => "BREAKING",
        ChangeSeverity.NonBreaking => "NONBREAKING",
        _ => "INFO",
    };
}
