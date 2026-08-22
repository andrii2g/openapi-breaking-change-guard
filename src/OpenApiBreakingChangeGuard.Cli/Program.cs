using System.Reflection;
using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Loading;
using OpenApiBreakingChangeGuard.Reporting;

namespace OpenApiBreakingChangeGuard.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        CliArguments arguments;
        try
        {
            arguments = CliArgumentParser.Parse(args);
        }
        catch (CliUsageException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Use --help to see valid usage.");
            return ExitCodes.InvalidInput;
        }

        if (arguments.ShowHelp)
        {
            Console.Write(HelpText);
            return ExitCodes.Success;
        }

        if (arguments.ShowVersion)
        {
            Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0");
            return ExitCodes.Success;
        }

        try
        {
            ValidatePaths(arguments);
            await using var oldStream = new FileStream(
                arguments.OldPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var newStream = new FileStream(
                arguments.NewPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var analyzer = new CompatibilityAnalyzer();
            var result = await analyzer.AnalyzeAsync(
                oldStream,
                NormalizeLabel(arguments.OldPath),
                newStream,
                NormalizeLabel(arguments.NewPath),
                new AnalysisOptions
                {
                    Profile = arguments.Profile,
                    OldRevision = arguments.OldRevision,
                    NewRevision = arguments.NewRevision,
                    OldRevisionUrl = arguments.OldRevisionUrl,
                    NewRevisionUrl = arguments.NewRevisionUrl,
                }).ConfigureAwait(false);

            Console.Write(new ConsoleReportRenderer().Render(
                result,
                new ConsoleReportOptions
                {
                    IncludeInfo = arguments.IncludeInfo,
                    MaxChanges = arguments.MaxConsoleChanges,
                }));
            if (arguments.ReportPath is not null)
            {
                var markdown = new MarkdownReportRenderer().Render(result);
                try
                {
                    await FileReportWriter.WriteAsync(arguments.ReportPath, markdown, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    Console.Error.WriteLine($"Analysis completed, but report '{arguments.ReportPath}' could not be saved: {exception.Message}");
                    return ExitCodes.InvalidInput;
                }
            }

            if (!result.IsComplete) return ExitCodes.IncompleteAnalysis;
            return result.BreakingCount > 0 ? ExitCodes.BreakingChanges : ExitCodes.Success;
        }
        catch (Exception exception) when (exception is OpenApiLoadException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine(exception.Message);
            return ExitCodes.InvalidInput;
        }
    }

    private static void ValidatePaths(CliArguments arguments)
    {
        if (arguments.ReportPath is null) return;
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var report = Path.GetFullPath(arguments.ReportPath);
        if (comparer.Equals(report, Path.GetFullPath(arguments.OldPath)) ||
            comparer.Equals(report, Path.GetFullPath(arguments.NewPath)))
        {
            throw new CliUsageException("The report path must not refer to either input file.");
        }
    }

    private static string NormalizeLabel(string path) => path.Replace('\\', '/');

    private const string HelpText =
        "OpenAPI Breaking Change Guard\n\n" +
        "Usage:\n" +
        "  openapi-guard <old-spec> <new-spec> [options]\n\n" +
        "Arguments:\n" +
        "  <old-spec>                      Accepted OpenAPI JSON file.\n" +
        "  <new-spec>                      Candidate OpenAPI JSON file.\n\n" +
        "Options:\n" +
        "  --old-revision <value>         Source revision for the accepted specification.\n" +
        "  --new-revision <value>         Source revision for the candidate specification.\n" +
        "  --old-revision-url <url>       HTTPS file URL for the accepted revision.\n" +
        "  --new-revision-url <url>       HTTPS file URL for the candidate revision.\n" +
        "  --report <path>                 Write a Markdown report.\n" +
        "  --profile <client|strict>       Compatibility profile. Default: client.\n" +
        "  --include-info                  Include Info changes in console details.\n" +
        "  --max-console-changes <number>  Maximum detailed console findings. Default: 200.\n" +
        "  -h, --help                      Show help and return 0.\n" +
        "  --version                       Show version and return 0.\n";
}
