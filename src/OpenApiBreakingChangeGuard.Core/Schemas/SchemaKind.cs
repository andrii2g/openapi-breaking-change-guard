namespace OpenApiBreakingChangeGuard.Schemas;

internal enum SchemaKind
{
    Any,
    Never,
    Primitive,
    Enum,
    Object,
    Array,
    Union,
    RecursiveReference,
    Unsupported,
}
