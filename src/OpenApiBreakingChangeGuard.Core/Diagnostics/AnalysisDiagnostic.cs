namespace OpenApiBreakingChangeGuard.Diagnostics;

/// <summary>Represents a deterministic validation or completeness diagnostic.</summary>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Location">The logical API location.</param>
/// <param name="Message">The diagnostic explanation.</param>
public sealed record AnalysisDiagnostic(DiagnosticSeverity Severity, string Code, string Location, string Message);
