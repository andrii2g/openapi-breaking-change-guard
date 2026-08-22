using System.Text.Json;

namespace OpenApiBreakingChangeGuard.Loading;

internal sealed class LoadedOpenApiDocument : IDisposable
{
    public LoadedOpenApiDocument(JsonDocument document, string label, string version)
    {
        Document = document;
        Label = label;
        Version = version;
    }

    public JsonDocument Document { get; }
    public string Label { get; }
    public string Version { get; }
    public bool IsVersion31 => Version.StartsWith("3.1.", StringComparison.Ordinal);

    public void Dispose() => Document.Dispose();
}
