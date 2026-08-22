using System.Diagnostics;
using Xunit;

namespace OpenApiBreakingChangeGuard.Tests.Cli;

public sealed class CliProcessTests
{
    [Fact]
    public async Task CliReturnsAllDocumentedExitCodes()
    {
        var root = FindRepositoryRoot();
        var exampleOld = Path.Combine(root, "examples", "old.json");
        var exampleNew = Path.Combine(root, "examples", "new.json");
        var success = await RunAsync(root, exampleOld, exampleOld);
        var breaking = await RunAsync(root, exampleOld, exampleNew);
        var invalidPath = Path.Combine(Path.GetTempPath(), $"obcg-invalid-{Guid.NewGuid():N}.json");
        var incompletePath = Path.Combine(Path.GetTempPath(), $"obcg-incomplete-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(invalidPath, "{");
            await File.WriteAllTextAsync(incompletePath, """
                { "openapi": "3.1.0", "paths": { "/x": { "get": { "responses": { "200": { "content": { "application/json": { "schema": { "$ref": "#/missing" } } } } } } } } }
                """);
            var invalid = await RunAsync(root, invalidPath, exampleOld);
            var incomplete = await RunAsync(root, incompletePath, incompletePath);

            Assert.Equal(0, success.ExitCode);
            Assert.Equal(1, breaking.ExitCode);
            Assert.Equal(2, invalid.ExitCode);
            Assert.Equal(3, incomplete.ExitCode);
        }
        finally
        {
            File.Delete(invalidPath);
            File.Delete(incompletePath);
        }
    }

    [Fact]
    public async Task InvalidInputDoesNotOverwriteExistingReport()
    {
        var root = FindRepositoryRoot();
        var report = Path.Combine(Path.GetTempPath(), $"obcg-report-{Guid.NewGuid():N}.md");
        try
        {
            await File.WriteAllTextAsync(report, "preserve me");
            var result = await RunAsync(root, "missing-old.json", "missing-new.json", "--report", report);

            Assert.Equal(2, result.ExitCode);
            Assert.Equal("preserve me", await File.ReadAllTextAsync(report));
        }
        finally
        {
            File.Delete(report);
        }
    }

    [Fact]
    public async Task RevisionMetadataFlowsToConsoleAndMarkdown()
    {
        var root = FindRepositoryRoot();
        var example = Path.Combine(root, "examples", "old.json");
        var report = Path.Combine(Path.GetTempPath(), $"obcg-revisions-{Guid.NewGuid():N}.md");
        try
        {
            var result = await RunAsync(
                root,
                example,
                example,
                "--old-revision",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "--new-revision=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "--old-revision-url=https://github.com/example/api/blob/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/openapi.json",
                "--new-revision-url=https://github.com/example/api/blob/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/openapi.json",
                "--report",
                report);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("@ aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", result.StandardOutput, StringComparison.Ordinal);
            var markdown = await File.ReadAllTextAsync(report);
            Assert.Contains("| Input | File | Revision | OpenAPI |", markdown, StringComparison.Ordinal);
            Assert.Contains("[`bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb`](<https://github.com/example/api/blob/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/openapi.json>)", markdown, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(report);
        }
    }

    private static async Task<ProcessResult> RunAsync(string root, params string[] arguments)
    {
        var assembly = Path.Combine(root, "src", "OpenApiBreakingChangeGuard.Cli", "bin", "Release", "net10.0", "openapi-guard.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(assembly);
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("CLI process could not be started.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new(process.ExitCode, await standardOutput, await standardError);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenApiBreakingChangeGuard.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
