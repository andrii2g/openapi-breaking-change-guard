using System.Text.Json;
using OpenApiBreakingChangeGuard.Analysis;

namespace OpenApiBreakingChangeGuard.Loading;

internal static class OpenApiJsonLoader
{
    public static async Task<LoadedOpenApiDocument> LoadAsync(
        Stream stream,
        string label,
        AnalysisLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(limits);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The input stream must be readable.", nameof(stream));
        }

        if (stream.CanSeek && stream.Length - stream.Position > limits.MaxInputBytes)
        {
            throw new OpenApiLoadException($"Input '{label}' exceeds the {limits.MaxInputBytes}-byte size limit.");
        }

        await using var buffer = new MemoryStream();
        var bytes = new byte[64 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > limits.MaxInputBytes)
            {
                throw new OpenApiLoadException($"Input '{label}' exceeds the {limits.MaxInputBytes}-byte size limit.");
            }

            await buffer.WriteAsync(bytes.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        buffer.Position = 0;
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(
                buffer,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = limits.MaxJsonDepth,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new OpenApiLoadException($"Input '{label}' is not valid JSON: {exception.Message}", exception);
        }

        try
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new OpenApiLoadException($"Input '{label}' must contain a JSON object at the root.");
            }

            if (!root.TryGetProperty("openapi", out var versionElement) || versionElement.ValueKind != JsonValueKind.String)
            {
                throw new OpenApiLoadException($"Input '{label}' must contain an 'openapi' string.");
            }

            var version = versionElement.GetString()!;
            if (!version.StartsWith("3.0.", StringComparison.Ordinal) && !version.StartsWith("3.1.", StringComparison.Ordinal))
            {
                throw new OpenApiLoadException($"Input '{label}' uses unsupported OpenAPI version '{version}'.");
            }

            if (!root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
            {
                throw new OpenApiLoadException($"Input '{label}' must contain a 'paths' object.");
            }

            return new LoadedOpenApiDocument(document, label, version);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }
}
