namespace OpenApiBreakingChangeGuard.Diagnostics;

/// <summary>Describes diagnostic impact on analysis completeness.</summary>
public enum DiagnosticSeverity
{
    /// <summary>The construct was handled reliably with a warning.</summary>
    Warning,
    /// <summary>A compatibility decision could not be made reliably.</summary>
    Error,
}
