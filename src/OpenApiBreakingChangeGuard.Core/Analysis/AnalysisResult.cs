using OpenApiBreakingChangeGuard.Changes;
using OpenApiBreakingChangeGuard.Diagnostics;

namespace OpenApiBreakingChangeGuard.Analysis;

/// <summary>Contains the immutable, deterministically ordered outcome of analysis.</summary>
public sealed record AnalysisResult
{
    /// <summary>Initializes an analysis result.</summary>
    public AnalysisResult(
        string oldLabel,
        string newLabel,
        string oldOpenApiVersion,
        string newOpenApiVersion,
        CompatibilityProfile profile,
        IEnumerable<CompatibilityChange> changes,
        IEnumerable<AnalysisDiagnostic> diagnostics)
    {
        OldLabel = oldLabel ?? throw new ArgumentNullException(nameof(oldLabel));
        NewLabel = newLabel ?? throw new ArgumentNullException(nameof(newLabel));
        OldOpenApiVersion = oldOpenApiVersion ?? throw new ArgumentNullException(nameof(oldOpenApiVersion));
        NewOpenApiVersion = newOpenApiVersion ?? throw new ArgumentNullException(nameof(newOpenApiVersion));
        Profile = profile;
        Changes = changes.Distinct(CompatibilityChangeComparer.Instance).Order(CompatibilityChangeComparer.Instance).ToArray();
        Diagnostics = diagnostics
            .Distinct()
            .OrderBy(static item => item.Severity)
            .ThenBy(static item => item.Code, StringComparer.Ordinal)
            .ThenBy(static item => item.Location, StringComparer.Ordinal)
            .ThenBy(static item => item.Message, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Gets the old input label.</summary>
    public string OldLabel { get; }
    /// <summary>Gets the new input label.</summary>
    public string NewLabel { get; }
    /// <summary>Gets the old OpenAPI version.</summary>
    public string OldOpenApiVersion { get; }
    /// <summary>Gets the new OpenAPI version.</summary>
    public string NewOpenApiVersion { get; }
    /// <summary>Gets the active compatibility profile.</summary>
    public CompatibilityProfile Profile { get; }
    /// <summary>Gets ordered compatibility changes.</summary>
    public IReadOnlyList<CompatibilityChange> Changes { get; }
    /// <summary>Gets ordered diagnostics.</summary>
    public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
    /// <summary>Gets whether analysis completed reliably.</summary>
    public bool IsComplete => Diagnostics.All(static item => item.Severity != DiagnosticSeverity.Error);
    /// <summary>Gets the number of breaking changes.</summary>
    public int BreakingCount => Changes.Count(static item => item.Severity == ChangeSeverity.Breaking);
    /// <summary>Gets the number of non-breaking changes.</summary>
    public int NonBreakingCount => Changes.Count(static item => item.Severity == ChangeSeverity.NonBreaking);
    /// <summary>Gets the number of informational changes.</summary>
    public int InfoCount => Changes.Count(static item => item.Severity == ChangeSeverity.Info);
}
