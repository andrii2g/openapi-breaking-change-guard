namespace OpenApiBreakingChangeGuard.Schemas;

internal sealed record SchemaOrigin(string LogicalLocation, string CanonicalPointer, SchemaUsage Usage);
