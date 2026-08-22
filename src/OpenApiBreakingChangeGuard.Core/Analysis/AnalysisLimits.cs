namespace OpenApiBreakingChangeGuard.Analysis;

/// <summary>Defines resource limits applied to untrusted input.</summary>
public sealed record AnalysisLimits
{
    /// <summary>Gets the maximum bytes accepted for one input.</summary>
    public long MaxInputBytes { get; init; } = 32L * 1024 * 1024;
    /// <summary>Gets the maximum JSON nesting depth.</summary>
    public int MaxJsonDepth { get; init; } = 256;
    /// <summary>Gets the maximum local-reference expansion depth.</summary>
    public int MaxReferenceDepth { get; init; } = 128;
    /// <summary>Gets the maximum unique references resolved per document.</summary>
    public int MaxUniqueReferences { get; init; } = 100_000;
    /// <summary>Gets the maximum schema nodes normalized.</summary>
    public int MaxSchemaNodes { get; init; } = 500_000;
    /// <summary>Gets the maximum changes retained.</summary>
    public int MaxChanges { get; init; } = 10_000;
    /// <summary>Gets the maximum diagnostics retained.</summary>
    public int MaxDiagnostics { get; init; } = 1_000;
}
