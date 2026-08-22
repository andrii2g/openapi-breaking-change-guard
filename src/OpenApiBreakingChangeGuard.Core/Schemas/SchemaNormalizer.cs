using System.Text;
using System.Text.Json;
using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Diagnostics;
using OpenApiBreakingChangeGuard.Documents;
using OpenApiBreakingChangeGuard.References;

namespace OpenApiBreakingChangeGuard.Schemas;

internal sealed class SchemaNormalizer
{
    private static readonly HashSet<string> SupportedKeywords = new(StringComparer.Ordinal)
    {
        "$ref", "additionalProperties", "allOf", "anyOf", "enum", "items", "nullable",
        "oneOf", "properties", "required", "type",
    };

    private static readonly HashSet<string> IgnoredKeywords = new(StringComparer.Ordinal)
    {
        "$id", "$schema", "default", "deprecated", "description", "example", "examples", "readOnly", "title", "writeOnly",
    };

    private static readonly HashSet<string> PrimitiveTypes = new(StringComparer.Ordinal)
    {
        "array", "boolean", "integer", "null", "number", "object", "string",
    };

    private readonly LocalReferenceResolver _resolver;
    private readonly bool _openApi31;
    private readonly AnalysisLimits _limits;
    private readonly DiagnosticCollector _diagnostics;
    private readonly Dictionary<string, NormalizedSchema> _cache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);
    private int _nodeCount;
    private bool _nodeLimitReported;

    public SchemaNormalizer(
        LocalReferenceResolver resolver,
        bool openApi31,
        AnalysisLimits limits,
        DiagnosticCollector diagnostics)
    {
        _resolver = resolver;
        _openApi31 = openApi31;
        _limits = limits;
        _diagnostics = diagnostics;
    }

    public NormalizedSchema Normalize(ContentSchema source, SchemaUsage usage, CancellationToken cancellationToken) =>
        NormalizeElement(source.Schema, source.Location, source.Pointer, usage, cancellationToken, false, false);

    private NormalizedSchema NormalizeElement(
        JsonElement element,
        string location,
        string pointer,
        SchemaUsage usage,
        CancellationToken cancellationToken,
        bool skipReference,
        bool skipAllOf)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var origin = new SchemaOrigin(location, pointer, usage);
        if (++_nodeCount > _limits.MaxSchemaNodes)
        {
            if (!_nodeLimitReported)
            {
                _nodeLimitReported = true;
                _diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.LimitExceeded,
                    location,
                    $"The schema node limit of {_limits.MaxSchemaNodes} was exceeded."));
            }

            return new UnsupportedSchema("schema node limit exceeded", origin, EmptyOpaque.Instance);
        }

        if (element.ValueKind == JsonValueKind.True) return new AnySchema(origin, EmptyOpaque.Instance);
        if (element.ValueKind == JsonValueKind.False) return new NeverSchema(origin, EmptyOpaque.Instance);
        if (element.ValueKind != JsonValueKind.Object)
        {
            _diagnostics.Add(new(
                DiagnosticSeverity.Error,
                DiagnosticCodes.InvalidStructure,
                location,
                "A schema must be an object or boolean."));
            return new UnsupportedSchema("invalid schema value", origin, EmptyOpaque.Instance);
        }

        if (!skipReference && element.TryGetProperty("$ref", out var referenceElement))
        {
            if (referenceElement.ValueKind != JsonValueKind.String)
            {
                _diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.InvalidReference,
                    location,
                    "A schema '$ref' must be a string."));
                return new UnsupportedSchema("invalid reference", origin, EmptyOpaque.Instance);
            }

            var reference = referenceElement.GetString()!;
            var target = NormalizeReference(reference, location, usage, cancellationToken);
            if (!HasSemanticReferenceSiblings(element)) return target;
            if (!_openApi31)
            {
                _diagnostics.Add(new(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.ReferenceSiblingIgnored,
                    location,
                    $"Semantic siblings of reference '{reference}' were ignored under OpenAPI 3.0 rules."));
                return target;
            }

            var siblings = NormalizeElement(element, location, pointer, usage, cancellationToken, true, false);
            return MergeAllOf([target, siblings], origin);
        }

        var opaque = ReadOpaqueKeywords(element);
        if (!skipAllOf && element.TryGetProperty("allOf", out var allOf))
        {
            if (allOf.ValueKind != JsonValueKind.Array)
            {
                return InvalidComposition(location, origin, opaque, "'allOf' must be an array.");
            }

            var branches = new List<NormalizedSchema>();
            var branchIndex = 0;
            foreach (var branch in allOf.EnumerateArray())
            {
                branches.Add(NormalizeElement(
                    branch,
                    $"{location}.allOf[{branchIndex++}]",
                    pointer,
                    usage,
                    cancellationToken,
                    false,
                    false));
            }

            if (HasOwnShapeKeywords(element, "allOf"))
            {
                branches.Add(NormalizeElement(element, location, pointer, usage, cancellationToken, true, true));
            }

            return MergeAllOf(branches, origin);
        }

        if (element.TryGetProperty("anyOf", out var anyOf))
        {
            return NormalizeUnion(anyOf, UnionKind.AnyOf, element, origin, usage, cancellationToken, opaque);
        }

        if (element.TryGetProperty("oneOf", out var oneOf))
        {
            return NormalizeUnion(oneOf, UnionKind.OneOf, element, origin, usage, cancellationToken, opaque);
        }

        NormalizedSchema result;
        if (element.TryGetProperty("enum", out var enumElement))
        {
            result = NormalizeEnum(element, enumElement, origin, opaque);
        }
        else if (HasType(element, "object") || element.TryGetProperty("properties", out _) || element.TryGetProperty("required", out _))
        {
            result = NormalizeObject(element, origin, usage, cancellationToken, opaque);
        }
        else if (HasType(element, "array"))
        {
            var items = element.TryGetProperty("items", out var itemElement)
                ? NormalizeElement(itemElement, $"{location}.items", pointer, usage, cancellationToken, false, false)
                : new AnySchema(origin with { LogicalLocation = $"{location}.items" }, EmptyOpaque.Instance);
            result = new ArraySchema(items, origin, opaque);
        }
        else if (element.TryGetProperty("type", out var typeElement))
        {
            result = NormalizeTypes(typeElement, origin, opaque);
        }
        else
        {
            result = new AnySchema(origin, opaque);
        }

        return ApplyNullable(element, result, origin);
    }

    private NormalizedSchema NormalizeReference(
        string reference,
        string location,
        SchemaUsage usage,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{usage}:{reference}";
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;
        var origin = new SchemaOrigin(location, reference, usage);
        if (!_active.Add(cacheKey)) return new RecursiveRefSchema(reference, origin);
        if (_active.Count > _limits.MaxReferenceDepth)
        {
            _active.Remove(cacheKey);
            _diagnostics.Add(new(
                DiagnosticSeverity.Error,
                DiagnosticCodes.LimitExceeded,
                location,
                $"The reference expansion depth limit of {_limits.MaxReferenceDepth} was exceeded."));
            return new UnsupportedSchema("reference depth exceeded", origin, EmptyOpaque.Instance);
        }

        var resolution = _resolver.Resolve(reference, location);
        if (!resolution.IsSuccess)
        {
            _active.Remove(cacheKey);
            _diagnostics.Add(resolution.Diagnostic!);
            return new UnsupportedSchema("unresolved reference", origin, EmptyOpaque.Instance);
        }

        var normalized = NormalizeElement(
            resolution.Element,
            location,
            resolution.CanonicalPointer,
            usage,
            cancellationToken,
            false,
            false);
        _active.Remove(cacheKey);
        _cache[cacheKey] = normalized;
        return normalized;
    }

    private NormalizedSchema NormalizeUnion(
        JsonElement unionElement,
        UnionKind kind,
        JsonElement owner,
        SchemaOrigin origin,
        SchemaUsage usage,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string> opaque)
    {
        if (unionElement.ValueKind != JsonValueKind.Array)
        {
            return InvalidComposition(origin.LogicalLocation, origin, opaque, $"'{kind}' must be an array.");
        }

        var alternatives = new Dictionary<string, NormalizedSchema>(StringComparer.Ordinal);
        var index = 0;
        foreach (var rawAlternative in unionElement.EnumerateArray())
        {
            var alternative = NormalizeElement(
                rawAlternative,
                $"{origin.LogicalLocation}.{(kind == UnionKind.AnyOf ? "anyOf" : "oneOf")}[{index++}]",
                origin.CanonicalPointer,
                usage,
                cancellationToken,
                false,
                false);
            if (alternative is UnionSchema nested && nested.UnionKind == kind)
            {
                foreach (var nestedAlternative in nested.Alternatives)
                {
                    AddAlternative(alternatives, nestedAlternative, origin.LogicalLocation);
                }
            }
            else
            {
                AddAlternative(alternatives, alternative, origin.LogicalLocation);
            }
        }

        if (ReadNullable(owner))
        {
            var nullSchema = new PrimitiveSchema("null", origin, EmptyOpaque.Instance);
            AddAlternative(alternatives, nullSchema, origin.LogicalLocation);
        }

        return new UnionSchema(kind, alternatives.Values.OrderBy(static item => item.CanonicalKey, StringComparer.Ordinal).ToArray(), origin, opaque);
    }

    private void AddAlternative(Dictionary<string, NormalizedSchema> alternatives, NormalizedSchema alternative, string location)
    {
        if (!alternatives.TryAdd(alternative.CanonicalKey, alternative))
        {
            _diagnostics.Add(new(
                DiagnosticSeverity.Warning,
                DiagnosticCodes.DuplicateValue,
                location,
                "A duplicate schema alternative was ignored."));
        }
    }

    private NormalizedSchema NormalizeEnum(
        JsonElement owner,
        JsonElement enumElement,
        SchemaOrigin origin,
        IReadOnlyDictionary<string, string> opaque)
    {
        if (enumElement.ValueKind != JsonValueKind.Array)
        {
            return InvalidComposition(origin.LogicalLocation, origin, opaque, "'enum' must be an array.");
        }

        var values = new Dictionary<string, CanonicalEnumValue>(StringComparer.Ordinal);
        var inferredTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in enumElement.EnumerateArray())
        {
            var canonical = CanonicalizeEnum(value);
            if (value.ValueKind is not (JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number or JsonValueKind.String))
            {
                return InvalidComposition(origin.LogicalLocation, origin, opaque, "Enum values must be primitive JSON values or null.");
            }

            inferredTypes.Add(TypeFor(value));
            if (!values.TryAdd(canonical.Canonical, canonical))
            {
                _diagnostics.Add(new(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.DuplicateValue,
                    origin.LogicalLocation,
                    $"Duplicate enum value {canonical.Display} was ignored."));
            }
        }

        var types = ReadTypes(owner).ToHashSet(StringComparer.Ordinal);
        if (types.Count == 0) types.UnionWith(inferredTypes);
        if (ReadNullable(owner)) types.Add("null");
        return new EnumSchema(
            types.Order(StringComparer.Ordinal).ToArray(),
            values.Values.Order().ToArray(),
            origin,
            opaque);
    }

    private NormalizedSchema NormalizeObject(
        JsonElement owner,
        SchemaOrigin origin,
        SchemaUsage usage,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string> opaque)
    {
        var properties = new SortedDictionary<string, NormalizedProperty>(StringComparer.Ordinal);
        if (owner.TryGetProperty("properties", out var propertyElement))
        {
            if (propertyElement.ValueKind != JsonValueKind.Object)
            {
                return InvalidComposition(origin.LogicalLocation, origin, opaque, "'properties' must be an object.");
            }

            foreach (var property in propertyElement.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
            {
                var normalized = NormalizeElement(
                    property.Value,
                    $"{origin.LogicalLocation}.properties.{property.Name}",
                    origin.CanonicalPointer,
                    usage,
                    cancellationToken,
                    false,
                    false);
                properties[property.Name] = new(property.Name, normalized);
            }
        }

        var required = new SortedSet<string>(StringComparer.Ordinal);
        if (owner.TryGetProperty("required", out var requiredElement))
        {
            if (requiredElement.ValueKind != JsonValueKind.Array)
            {
                return InvalidComposition(origin.LogicalLocation, origin, opaque, "'required' must be an array.");
            }

            foreach (var item in requiredElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    return InvalidComposition(origin.LogicalLocation, origin, opaque, "Every 'required' entry must be a string.");
                }

                var name = item.GetString()!;
                if (!required.Add(name))
                {
                    _diagnostics.Add(new(
                        DiagnosticSeverity.Warning,
                        DiagnosticCodes.DuplicateValue,
                        $"{origin.LogicalLocation}.required",
                        $"Duplicate required property name '{name}' was ignored."));
                }
            }
        }

        foreach (var name in required.Where(name => !properties.ContainsKey(name)))
        {
            _diagnostics.Add(new(
                DiagnosticSeverity.Warning,
                DiagnosticCodes.InvalidStructure,
                $"{origin.LogicalLocation}.required",
                $"Required name '{name}' has no corresponding property schema."));
        }

        var additional = NormalizeAdditionalProperties(owner, origin, usage, cancellationToken);
        return ApplyNullable(owner, new ObjectSchema(properties, required, additional, origin, opaque), origin);
    }

    private AdditionalPropertiesPolicy NormalizeAdditionalProperties(
        JsonElement owner,
        SchemaOrigin origin,
        SchemaUsage usage,
        CancellationToken cancellationToken)
    {
        if (!owner.TryGetProperty("additionalProperties", out var additional))
        {
            return AdditionalPropertiesPolicy.Unspecified;
        }

        if (additional.ValueKind == JsonValueKind.True) return new(AdditionalPropertiesKind.AllowedAny);
        if (additional.ValueKind == JsonValueKind.False) return new(AdditionalPropertiesKind.Forbidden);
        var schema = NormalizeElement(
            additional,
            $"{origin.LogicalLocation}.additionalProperties",
            origin.CanonicalPointer,
            usage,
            cancellationToken,
            false,
            false);
        return new(AdditionalPropertiesKind.AllowedSchema, schema);
    }

    private NormalizedSchema NormalizeTypes(
        JsonElement typeElement,
        SchemaOrigin origin,
        IReadOnlyDictionary<string, string> opaque)
    {
        var types = new SortedSet<string>(StringComparer.Ordinal);
        if (typeElement.ValueKind == JsonValueKind.String)
        {
            types.Add(typeElement.GetString()!);
        }
        else if (typeElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in typeElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    return UnknownType(origin, opaque, "Schema type array entries must be strings.");
                }

                types.Add(item.GetString()!);
            }
        }
        else
        {
            return UnknownType(origin, opaque, "Schema 'type' must be a string or string array.");
        }

        var unknown = types.FirstOrDefault(type => !PrimitiveTypes.Contains(type));
        if (unknown is not null)
        {
            return UnknownType(origin, opaque, $"Schema type '{unknown}' is unsupported.");
        }

        if (types.Count == 1) return new PrimitiveSchema(types.Single(), origin, opaque);
        return new UnionSchema(
            UnionKind.AnyOf,
            types.Select(type => (NormalizedSchema)new PrimitiveSchema(type, origin, EmptyOpaque.Instance)).ToArray(),
            origin,
            opaque);
    }

    private NormalizedSchema ApplyNullable(JsonElement owner, NormalizedSchema schema, SchemaOrigin origin)
    {
        if (!ReadNullable(owner) || schema is UnionSchema || schema is EnumSchema ||
            schema is PrimitiveSchema { Type: "null" })
        {
            return schema;
        }

        var alternatives = new[]
        {
            schema,
            new PrimitiveSchema("null", origin, EmptyOpaque.Instance),
        }.OrderBy(static item => item.CanonicalKey, StringComparer.Ordinal).ToArray();
        return new UnionSchema(UnionKind.AnyOf, alternatives, origin, EmptyOpaque.Instance);
    }

    private NormalizedSchema MergeAllOf(IReadOnlyList<NormalizedSchema> branches, SchemaOrigin origin)
    {
        if (branches.Count == 0) return new AnySchema(origin, EmptyOpaque.Instance);
        NormalizedSchema result = branches[0];
        for (var index = 1; index < branches.Count; index++)
        {
            var next = branches[index];
            if (result is AnySchema) { result = next; continue; }
            if (next is AnySchema || result.CanonicalKey == next.CanonicalKey) continue;
            if (result is NeverSchema || next is NeverSchema) { result = new NeverSchema(origin, EmptyOpaque.Instance); continue; }
            if (result is PrimitiveSchema leftPrimitive && next is PrimitiveSchema rightPrimitive)
            {
                result = leftPrimitive.Type == rightPrimitive.Type
                    ? leftPrimitive
                    : new NeverSchema(origin, EmptyOpaque.Instance);
                continue;
            }

            if (result is ObjectSchema leftObject && next is ObjectSchema rightObject)
            {
                result = MergeObjects(leftObject, rightObject, origin);
                continue;
            }

            return InvalidComposition(
                origin.LogicalLocation,
                origin,
                EmptyOpaque.Instance,
                "The allOf intersection cannot be represented by the supported schema model.");
        }

        return result;
    }

    private NormalizedSchema MergeObjects(ObjectSchema left, ObjectSchema right, SchemaOrigin origin)
    {
        var properties = new SortedDictionary<string, NormalizedProperty>(left.Properties.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var property in right.Properties)
        {
            if (properties.TryGetValue(property.Key, out var existing) &&
                existing.Schema.CanonicalKey != property.Value.Schema.CanonicalKey)
            {
                return InvalidComposition(
                    $"{origin.LogicalLocation}.properties.{property.Key}",
                    origin,
                    EmptyOpaque.Instance,
                    $"Conflicting allOf schemas were found for property '{property.Key}'.");
            }

            properties[property.Key] = property.Value;
        }

        var required = new SortedSet<string>(left.Required, StringComparer.Ordinal);
        required.UnionWith(right.Required);
        var additional = MergeAdditional(left.AdditionalProperties, right.AdditionalProperties, origin);
        if (additional is null)
        {
            return InvalidComposition(
                $"{origin.LogicalLocation}.additionalProperties",
                origin,
                EmptyOpaque.Instance,
                "Conflicting allOf additional-properties policies were found.");
        }

        var opaque = new SortedDictionary<string, string>(left.OpaqueKeywords.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var keyword in right.OpaqueKeywords)
        {
            if (opaque.TryGetValue(keyword.Key, out var existing) && existing != keyword.Value)
            {
                return InvalidComposition(origin.LogicalLocation, origin, opaque, $"Conflicting allOf keyword '{keyword.Key}' was found.");
            }

            opaque[keyword.Key] = keyword.Value;
        }

        return new ObjectSchema(properties, required, additional, origin, opaque);
    }

    private static AdditionalPropertiesPolicy? MergeAdditional(
        AdditionalPropertiesPolicy left,
        AdditionalPropertiesPolicy right,
        SchemaOrigin origin)
    {
        if (left.Kind == AdditionalPropertiesKind.Unspecified) return right;
        if (right.Kind == AdditionalPropertiesKind.Unspecified) return left;
        if (left.Kind == AdditionalPropertiesKind.Forbidden || right.Kind == AdditionalPropertiesKind.Forbidden)
            return new(AdditionalPropertiesKind.Forbidden);
        if (left.Kind == AdditionalPropertiesKind.AllowedAny) return right;
        if (right.Kind == AdditionalPropertiesKind.AllowedAny) return left;
        if (left.Schema?.CanonicalKey == right.Schema?.CanonicalKey) return left;
        _ = origin;
        return null;
    }

    private UnsupportedSchema InvalidComposition(
        string location,
        SchemaOrigin origin,
        IReadOnlyDictionary<string, string> opaque,
        string message)
    {
        _diagnostics.Add(new(DiagnosticSeverity.Error, DiagnosticCodes.UnsupportedComposition, location, message));
        return new UnsupportedSchema(message, origin, opaque);
    }

    private UnsupportedSchema UnknownType(
        SchemaOrigin origin,
        IReadOnlyDictionary<string, string> opaque,
        string message)
    {
        _diagnostics.Add(new(DiagnosticSeverity.Error, DiagnosticCodes.UnknownType, origin.LogicalLocation, message));
        return new UnsupportedSchema(message, origin, opaque);
    }

    private static IReadOnlyDictionary<string, string> ReadOpaqueKeywords(JsonElement element)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (SupportedKeywords.Contains(property.Name) || IgnoredKeywords.Contains(property.Name) ||
                property.Name.StartsWith("x-", StringComparison.Ordinal))
            {
                continue;
            }

            result[property.Name] = CanonicalJson(property.Value);
        }

        return result;
    }

    private static bool HasSemanticReferenceSiblings(JsonElement element) =>
        element.EnumerateObject().Any(property =>
            property.Name != "$ref" &&
            !IgnoredKeywords.Contains(property.Name) &&
            !property.Name.StartsWith("x-", StringComparison.Ordinal));

    private static bool HasOwnShapeKeywords(JsonElement element, string excluded) =>
        element.EnumerateObject().Any(property =>
            property.Name != excluded &&
            property.Name != "$ref" &&
            !IgnoredKeywords.Contains(property.Name) &&
            !property.Name.StartsWith("x-", StringComparison.Ordinal));

    private static bool HasType(JsonElement owner, string expected) => ReadTypes(owner).Contains(expected, StringComparer.Ordinal);

    private static IEnumerable<string> ReadTypes(JsonElement owner)
    {
        if (!owner.TryGetProperty("type", out var type)) yield break;
        if (type.ValueKind == JsonValueKind.String)
        {
            yield return type.GetString()!;
        }
        else if (type.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in type.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String) yield return item.GetString()!;
            }
        }
    }

    private static bool ReadNullable(JsonElement owner) =>
        owner.TryGetProperty("nullable", out var nullable) && nullable.ValueKind == JsonValueKind.True;

    private static CanonicalEnumValue CanonicalizeEnum(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => new("null", "null"),
        JsonValueKind.True => new("boolean:true", "true"),
        JsonValueKind.False => new("boolean:false", "false"),
        JsonValueKind.Number => new($"number:{NumericCanonicalizer.Canonicalize(value.GetRawText())}", value.GetRawText()),
        JsonValueKind.String => new($"string:{JsonSerializer.Serialize(value.GetString())}", value.GetString()!),
        _ => throw new InvalidOperationException("Enum values must be primitive JSON values or null."),
    };

    private static string TypeFor(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => "null",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Number => "number",
        JsonValueKind.String => "string",
        _ => "unsupported",
    };

    private static string CanonicalJson(JsonElement element)
    {
        var builder = new StringBuilder();
        AppendCanonicalJson(builder, element);
        return builder.ToString();
    }

    private static void AppendCanonicalJson(StringBuilder builder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                foreach (var property in element.EnumerateObject().OrderBy(static item => item.Name, StringComparer.Ordinal))
                {
                    builder.Append(JsonSerializer.Serialize(property.Name)).Append(':');
                    AppendCanonicalJson(builder, property.Value);
                    builder.Append(',');
                }
                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                foreach (var item in element.EnumerateArray())
                {
                    AppendCanonicalJson(builder, item);
                    builder.Append(',');
                }
                builder.Append(']');
                break;
            case JsonValueKind.Number:
                builder.Append(NumericCanonicalizer.Canonicalize(element.GetRawText()));
                break;
            default:
                builder.Append(element.GetRawText());
                break;
        }
    }
}
