namespace OpenApiBreakingChangeGuard.Analysis;

/// <summary>Configures compatibility analysis.</summary>
public sealed record AnalysisOptions
{
    /// <summary>Gets the optional source revision of the accepted document.</summary>
    public string? OldRevision { get; init; }
    /// <summary>Gets the optional source revision of the candidate document.</summary>
    public string? NewRevision { get; init; }
    /// <summary>Gets the optional HTTPS file URL for the accepted revision.</summary>
    public string? OldRevisionUrl { get; init; }
    /// <summary>Gets the optional HTTPS file URL for the candidate revision.</summary>
    public string? NewRevisionUrl { get; init; }
    /// <summary>Gets the compatibility profile.</summary>
    public CompatibilityProfile Profile { get; init; } = CompatibilityProfile.Client;
    /// <summary>Gets resource limits for untrusted input.</summary>
    public AnalysisLimits Limits { get; init; } = new();
}
