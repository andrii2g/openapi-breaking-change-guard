using System.Globalization;
using OpenApiBreakingChangeGuard.Analysis;

namespace OpenApiBreakingChangeGuard.Cli;

internal static class CliArgumentParser
{
    public static CliArguments Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Any(argument => argument is "-h" or "--help"))
        {
            return new(string.Empty, string.Empty, null, CompatibilityProfile.Client, false, 200, true, false);
        }

        if (arguments.Any(argument => argument == "--version"))
        {
            return new(string.Empty, string.Empty, null, CompatibilityProfile.Client, false, 200, false, true);
        }

        var positional = new List<string>();
        string? report = null;
        string? profile = null;
        string? maximum = null;
        var includeInfo = false;
        var includeInfoSeen = false;
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument == "--include-info")
            {
                if (includeInfoSeen) throw new CliUsageException("Option '--include-info' was specified more than once.");
                includeInfoSeen = true;
                includeInfo = true;
                continue;
            }

            if (TryReadOption(argument, "--report", arguments, ref index, ref report) ||
                TryReadOption(argument, "--profile", arguments, ref index, ref profile) ||
                TryReadOption(argument, "--max-console-changes", arguments, ref index, ref maximum))
            {
                continue;
            }

            if (argument.Length > 0 && argument[0] == '-')
            {
                throw new CliUsageException($"Unknown option '{argument}'.");
            }

            positional.Add(argument);
        }

        if (positional.Count != 2)
        {
            throw new CliUsageException("Exactly two OpenAPI JSON paths are required.");
        }

        var parsedProfile = profile switch
        {
            null or "client" => CompatibilityProfile.Client,
            "strict" => CompatibilityProfile.Strict,
            _ => throw new CliUsageException("Option '--profile' must be 'client' or 'strict'."),
        };
        var parsedMaximum = 200;
        if (maximum is not null &&
            (!int.TryParse(maximum, NumberStyles.None, CultureInfo.InvariantCulture, out parsedMaximum) || parsedMaximum is < 0 or > 10_000))
        {
            throw new CliUsageException("Option '--max-console-changes' must be an integer from 0 through 10,000.");
        }

        return new(positional[0], positional[1], report, parsedProfile, includeInfo, parsedMaximum, false, false);
    }

    private static bool TryReadOption(
        string argument,
        string option,
        IReadOnlyList<string> arguments,
        ref int index,
        ref string? destination)
    {
        if (argument != option && !argument.StartsWith(option + "=", StringComparison.Ordinal))
        {
            return false;
        }

        if (destination is not null)
        {
            throw new CliUsageException($"Option '{option}' was specified more than once.");
        }

        if (argument == option)
        {
            if (++index >= arguments.Count || (arguments[index].Length > 0 && arguments[index][0] == '-'))
            {
                throw new CliUsageException($"Option '{option}' requires a value.");
            }

            destination = arguments[index];
        }
        else
        {
            destination = argument[(option.Length + 1)..];
            if (destination.Length == 0) throw new CliUsageException($"Option '{option}' requires a value.");
        }

        return true;
    }
}
