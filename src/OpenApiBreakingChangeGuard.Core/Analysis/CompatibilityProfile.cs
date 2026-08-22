namespace OpenApiBreakingChangeGuard.Analysis;

/// <summary>Selects directional response widening policy.</summary>
public enum CompatibilityProfile
{
    /// <summary>Practical existing-client compatibility.</summary>
    Client,
    /// <summary>Promotes response enum and union widening to breaking.</summary>
    Strict,
}
