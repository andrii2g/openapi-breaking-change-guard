using System.Text.Json;
using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Diagnostics;
using OpenApiBreakingChangeGuard.Loading;
using OpenApiBreakingChangeGuard.References;

namespace OpenApiBreakingChangeGuard.Documents;

internal static class OpenApiDocumentIndexer
{
    private static readonly string[] Methods =
    [
        "delete", "get", "head", "options", "patch", "post", "put", "trace",
    ];

    public static IndexedDocument Index(
        LoadedOpenApiDocument source,
        AnalysisLimits limits,
        DiagnosticCollector diagnostics,
        CancellationToken cancellationToken)
    {
        var resolver = new LocalReferenceResolver(source.Document.RootElement, limits);
        var operations = new Dictionary<OperationKey, IndexedOperation>();
        var pathsIndex = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var paths = source.Document.RootElement.GetProperty("paths");

        foreach (var pathProperty in paths.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pathProperty.Name.Length == 0 || pathProperty.Name[0] != '/')
            {
                continue;
            }

            var pathLocation = $"paths.{pathProperty.Name}";
            var pathItem = ResolveObject(pathProperty.Value, pathLocation, resolver, limits, diagnostics);
            if (pathItem is null || pathItem.Value.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.InvalidStructure,
                    pathLocation,
                    "A path item must be a JSON object."));
                continue;
            }

            var inherited = ReadParameters(
                pathItem.Value,
                pathLocation,
                resolver,
                limits,
                diagnostics,
                cancellationToken);
            var methods = new List<string>();
            foreach (var method in Methods)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!pathItem.Value.TryGetProperty(method, out var operationElement))
                {
                    continue;
                }

                methods.Add(method);
                var operationLocation = $"{pathLocation}.{method}";
                var resolvedOperation = ResolveObject(operationElement, operationLocation, resolver, limits, diagnostics);
                if (resolvedOperation is null || resolvedOperation.Value.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(new(
                        DiagnosticSeverity.Error,
                        DiagnosticCodes.InvalidStructure,
                        operationLocation,
                        "An operation must be a JSON object."));
                    continue;
                }

                var parameters = new Dictionary<ParameterKey, IndexedParameter>(inherited);
                foreach (var pair in ReadParameters(
                    resolvedOperation.Value,
                    operationLocation,
                    resolver,
                    limits,
                    diagnostics,
                    cancellationToken))
                {
                    parameters[pair.Key] = pair.Value;
                }

                var key = new OperationKey(pathProperty.Name, method);
                operations[key] = new(
                    key,
                    ReadBoolean(resolvedOperation.Value, "deprecated"),
                    parameters,
                    ReadRequestContent(resolvedOperation.Value, operationLocation, resolver, limits, diagnostics),
                    ReadResponses(resolvedOperation.Value, operationLocation, resolver, limits, diagnostics),
                    operationLocation);
            }

            pathsIndex[pathProperty.Name] = methods.Order(StringComparer.Ordinal).ToArray();
        }

        return new(source, pathsIndex, operations, resolver);
    }

    private static Dictionary<ParameterKey, IndexedParameter> ReadParameters(
        JsonElement owner,
        string ownerLocation,
        LocalReferenceResolver resolver,
        AnalysisLimits limits,
        DiagnosticCollector diagnostics,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<ParameterKey, IndexedParameter>();
        if (!owner.TryGetProperty("parameters", out var parameters))
        {
            return result;
        }

        if (parameters.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new(
                DiagnosticSeverity.Error,
                DiagnosticCodes.InvalidStructure,
                $"{ownerLocation}.parameters",
                "Parameters must be a JSON array."));
            return result;
        }

        var index = 0;
        foreach (var rawParameter in parameters.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemLocation = $"{ownerLocation}.parameters[{index++}]";
            var parameter = ResolveObject(rawParameter, itemLocation, resolver, limits, diagnostics);
            if (parameter is null || parameter.Value.ValueKind != JsonValueKind.Object ||
                !parameter.Value.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String ||
                !parameter.Value.TryGetProperty("in", out var inElement) || inElement.ValueKind != JsonValueKind.String)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.InvalidStructure,
                    itemLocation,
                    "A parameter must be an object with string 'name' and 'in' fields."));
                continue;
            }

            var name = nameElement.GetString()!;
            var location = inElement.GetString()!;
            if (location is not ("path" or "query" or "header" or "cookie"))
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.InvalidStructure,
                    itemLocation,
                    $"Parameter location '{location}' is unsupported."));
                continue;
            }

            var required = ReadBoolean(parameter.Value, "required");
            if (location == "path" && !required)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.InvalidPathParameter,
                    itemLocation,
                    $"Path parameter '{name}' must be required and was normalized as required."));
                required = true;
            }

            ContentSchema? schema = null;
            if (parameter.Value.TryGetProperty("schema", out var schemaElement))
            {
                schema = new(schemaElement, $"{ownerLocation}.parameters.{location}.{name}.schema", string.Empty);
            }

            var key = new ParameterKey(location, name);
            result[key] = new(
                key,
                required,
                ReadBoolean(parameter.Value, "deprecated"),
                schema,
                $"{ownerLocation}.parameters.{location}.{name}");
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ContentSchema> ReadRequestContent(
        JsonElement operation,
        string location,
        LocalReferenceResolver resolver,
        AnalysisLimits limits,
        DiagnosticCollector diagnostics)
    {
        if (!operation.TryGetProperty("requestBody", out var requestBody))
        {
            return new Dictionary<string, ContentSchema>(StringComparer.Ordinal);
        }

        var resolved = ResolveObject(requestBody, $"{location}.requestBody", resolver, limits, diagnostics);
        return resolved is null
            ? new Dictionary<string, ContentSchema>(StringComparer.Ordinal)
            : ReadContent(resolved.Value, $"{location}.requestBody", diagnostics);
    }

    private static IReadOnlyDictionary<string, IndexedResponse> ReadResponses(
        JsonElement operation,
        string location,
        LocalReferenceResolver resolver,
        AnalysisLimits limits,
        DiagnosticCollector diagnostics)
    {
        var result = new Dictionary<string, IndexedResponse>(StringComparer.Ordinal);
        if (!operation.TryGetProperty("responses", out var responses))
        {
            return result;
        }

        if (responses.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(new(
                DiagnosticSeverity.Error,
                DiagnosticCodes.InvalidStructure,
                $"{location}.responses",
                "Responses must be a JSON object."));
            return result;
        }

        foreach (var responseProperty in responses.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
        {
            var responseLocation = $"{location}.responses.{responseProperty.Name}";
            var response = ResolveObject(responseProperty.Value, responseLocation, resolver, limits, diagnostics);
            var content = response is null
                ? new Dictionary<string, ContentSchema>(StringComparer.Ordinal)
                : ReadContent(response.Value, responseLocation, diagnostics);
            result[responseProperty.Name] = new(responseProperty.Name, content, responseLocation);
        }

        return result;
    }

    private static Dictionary<string, ContentSchema> ReadContent(
        JsonElement owner,
        string location,
        DiagnosticCollector diagnostics)
    {
        var result = new Dictionary<string, ContentSchema>(StringComparer.Ordinal);
        if (!owner.TryGetProperty("content", out var content))
        {
            return result;
        }

        if (content.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(new(
                DiagnosticSeverity.Error,
                DiagnosticCodes.InvalidStructure,
                $"{location}.content",
                "Content must be a JSON object."));
            return result;
        }

        foreach (var media in content.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
        {
            if (media.Value.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.InvalidStructure,
                    $"{location}.content.{media.Name}",
                    "A media type entry must be a JSON object."));
                continue;
            }

            if (media.Value.TryGetProperty("schema", out var schema))
            {
                var schemaLocation = $"{location}.content.{media.Name}.schema";
                result[media.Name] = new(schema, schemaLocation, string.Empty);
            }
        }

        return result;
    }

    private static JsonElement? ResolveObject(
        JsonElement element,
        string location,
        LocalReferenceResolver resolver,
        AnalysisLimits limits,
        DiagnosticCollector diagnostics)
    {
        var active = new HashSet<string>(StringComparer.Ordinal);
        for (var depth = 0; depth <= limits.MaxReferenceDepth; depth++)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty("$ref", out var referenceElement) ||
                referenceElement.ValueKind != JsonValueKind.String)
            {
                return element;
            }

            var reference = referenceElement.GetString()!;
            if (!active.Add(reference))
            {
                diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.InvalidReference,
                    location,
                    $"Reference cycle encountered while resolving '{reference}'."));
                return null;
            }

            var resolution = resolver.Resolve(reference, location);
            if (!resolution.IsSuccess)
            {
                diagnostics.Add(resolution.Diagnostic!);
                return null;
            }

            element = resolution.Element;
        }

        diagnostics.Add(new(
            DiagnosticSeverity.Error,
            DiagnosticCodes.LimitExceeded,
            location,
            $"The reference expansion depth limit of {limits.MaxReferenceDepth} was exceeded."));
        return null;
    }

    private static bool ReadBoolean(JsonElement owner, string propertyName) =>
        owner.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
}
