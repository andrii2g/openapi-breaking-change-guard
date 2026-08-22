namespace OpenApiBreakingChangeGuard.Reporting;

/// <summary>Configures console-detail rendering.</summary>
public sealed record ConsoleReportOptions
{
    /// <summary>Gets whether informational findings are included in details.</summary>
    public bool IncludeInfo { get; init; }
    /// <summary>Gets the maximum number of detailed findings.</summary>
    public int MaxChanges { get; init; } = 200;
}
