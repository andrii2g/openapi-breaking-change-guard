using OpenApiBreakingChangeGuard.Analysis;

namespace OpenApiBreakingChangeGuard.Cli;

internal sealed record CliArguments(
    string OldPath,
    string NewPath,
    string? ReportPath,
    CompatibilityProfile Profile,
    bool IncludeInfo,
    int MaxConsoleChanges,
    bool ShowHelp,
    bool ShowVersion);
