namespace OpenApiBreakingChangeGuard.Cli;

internal sealed class CliUsageException : ArgumentException
{
    public CliUsageException(string message)
        : base(message)
    {
    }
}
