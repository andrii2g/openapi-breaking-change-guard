namespace OpenApiBreakingChangeGuard.Analysis;

/// <summary>Configures compatibility analysis.</summary>
public sealed record AnalysisOptions
{
    /// <summary>Gets the compatibility profile.</summary>
    public CompatibilityProfile Profile { get; init; } = CompatibilityProfile.Client;
    /// <summary>Gets resource limits for untrusted input.</summary>
    public AnalysisLimits Limits { get; init; } = new();
}
