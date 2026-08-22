using OpenApiBreakingChangeGuard.Analysis;

namespace OpenApiBreakingChangeGuard.Cli;

internal sealed record CliArguments(
    string OldPath,
    string NewPath,
    string? OldRevision,
    string? NewRevision,
    string? OldRevisionUrl,
    string? NewRevisionUrl,
    string? ReportPath,
    CompatibilityProfile Profile,
    bool IncludeInfo,
    int MaxConsoleChanges,
    bool ShowHelp,
    bool ShowVersion);
