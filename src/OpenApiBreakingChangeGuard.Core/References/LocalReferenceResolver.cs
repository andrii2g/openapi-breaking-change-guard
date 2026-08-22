using System.Globalization;
using System.Text.Json;
using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Diagnostics;

namespace OpenApiBreakingChangeGuard.References;

internal sealed class LocalReferenceResolver
{
    private readonly JsonElement _root;
    private readonly AnalysisLimits _limits;
    private readonly ReferenceResolutionCache _cache = new();

    public LocalReferenceResolver(JsonElement root, AnalysisLimits limits)
    {
        _root = root;
        _limits = limits;
    }

    public ReferenceResolution Resolve(string reference, string logicalLocation)
    {
        if (_cache.TryGet(reference, out var cached))
        {
            return cached;
        }

        ReferenceResolution result;
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            result = Failure(
                DiagnosticCodes.UnsupportedReference,
                logicalLocation,
                $"Reference '{reference}' is not a supported local '#/' reference.");
        }
        else if (_cache.Count >= _limits.MaxUniqueReferences)
        {
            result = Failure(
                DiagnosticCodes.LimitExceeded,
                logicalLocation,
                $"The unique reference limit of {_limits.MaxUniqueReferences} was exceeded.");
        }
        else
        {
            result = Traverse(reference, logicalLocation);
        }

        _cache.Add(reference, result);
        return result;
    }

    private ReferenceResolution Traverse(string reference, string logicalLocation)
    {
        IReadOnlyList<string> tokens;
        try
        {
            tokens = JsonPointer.Decode(reference);
        }
        catch (ArgumentException exception)
        {
            return Failure(DiagnosticCodes.InvalidReference, logicalLocation, exception.Message);
        }

        var current = _root;
        foreach (var token in tokens)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(token, out current))
                {
                    return Failure(
                        DiagnosticCodes.InvalidReference,
                        logicalLocation,
                        $"Reference '{reference}' targets a missing object property '{token}'.");
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                if (!IsCanonicalArrayIndex(token, out var index) || index >= current.GetArrayLength())
                {
                    return Failure(
                        DiagnosticCodes.InvalidReference,
                        logicalLocation,
                        $"Reference '{reference}' contains an invalid array index '{token}'.");
                }

                current = current[index];
            }
            else
            {
                return Failure(
                    DiagnosticCodes.InvalidReference,
                    logicalLocation,
                    $"Reference '{reference}' traverses through a non-container value.");
            }
        }

        return new(true, current, reference, null);
    }

    private static bool IsCanonicalArrayIndex(string token, out int index)
    {
        index = -1;
        if (token.Length == 0 || (token.Length > 1 && token[0] == '0'))
        {
            return false;
        }

        return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0;
    }

    private static ReferenceResolution Failure(string code, string location, string message) =>
        new(false, default, string.Empty, new(DiagnosticSeverity.Error, code, location, message));
}
