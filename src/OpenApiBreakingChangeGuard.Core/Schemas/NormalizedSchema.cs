namespace OpenApiBreakingChangeGuard.Schemas;

internal abstract record NormalizedSchema(
    SchemaKind Kind,
    SchemaOrigin Origin,
    IReadOnlyDictionary<string, string> OpaqueKeywords)
{
    public string CanonicalKey => SchemaCanonicalKey.Create(this);
}

internal sealed record AnySchema(
    SchemaOrigin SchemaOrigin,
    IReadOnlyDictionary<string, string> Opaque)
    : NormalizedSchema(SchemaKind.Any, SchemaOrigin, Opaque);

internal sealed record NeverSchema(
    SchemaOrigin SchemaOrigin,
    IReadOnlyDictionary<string, string> Opaque)
    : NormalizedSchema(SchemaKind.Never, SchemaOrigin, Opaque);

internal sealed record PrimitiveSchema(
    string Type,
    SchemaOrigin SchemaOrigin,
    IReadOnlyDictionary<string, string> Opaque)
    : NormalizedSchema(SchemaKind.Primitive, SchemaOrigin, Opaque);

internal sealed record EnumSchema(
    IReadOnlyList<string> BaseTypes,
    IReadOnlyList<CanonicalEnumValue> Values,
    SchemaOrigin SchemaOrigin,
    IReadOnlyDictionary<string, string> Opaque)
    : NormalizedSchema(SchemaKind.Enum, SchemaOrigin, Opaque);

internal sealed record ObjectSchema(
    IReadOnlyDictionary<string, NormalizedProperty> Properties,
    IReadOnlySet<string> Required,
    AdditionalPropertiesPolicy AdditionalProperties,
    SchemaOrigin SchemaOrigin,
    IReadOnlyDictionary<string, string> Opaque)
    : NormalizedSchema(SchemaKind.Object, SchemaOrigin, Opaque);

internal sealed record ArraySchema(
    NormalizedSchema Items,
    SchemaOrigin SchemaOrigin,
    IReadOnlyDictionary<string, string> Opaque)
    : NormalizedSchema(SchemaKind.Array, SchemaOrigin, Opaque);

internal sealed record UnionSchema(
    UnionKind UnionKind,
    IReadOnlyList<NormalizedSchema> Alternatives,
    SchemaOrigin SchemaOrigin,
    IReadOnlyDictionary<string, string> Opaque)
    : NormalizedSchema(SchemaKind.Union, SchemaOrigin, Opaque);

internal sealed record RecursiveRefSchema(
    string Pointer,
    SchemaOrigin SchemaOrigin)
    : NormalizedSchema(SchemaKind.RecursiveReference, SchemaOrigin, EmptyOpaque.Instance);

internal sealed record UnsupportedSchema(
    string Reason,
    SchemaOrigin SchemaOrigin,
    IReadOnlyDictionary<string, string> Opaque)
    : NormalizedSchema(SchemaKind.Unsupported, SchemaOrigin, Opaque);

internal static class EmptyOpaque
{
    public static IReadOnlyDictionary<string, string> Instance { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
