namespace OpenApiBreakingChangeGuard.Changes;

/// <summary>Describes the compatibility impact of a change.</summary>
public enum ChangeSeverity
{
    /// <summary>An existing client interaction may fail.</summary>
    Breaking,
    /// <summary>The change is compatible with existing clients.</summary>
    NonBreaking,
    /// <summary>The change is noteworthy but is not a default-profile failure.</summary>
    Info,
}
