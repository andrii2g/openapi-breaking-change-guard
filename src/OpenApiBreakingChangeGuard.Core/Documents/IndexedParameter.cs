namespace OpenApiBreakingChangeGuard.Documents;

internal sealed record IndexedParameter(
    ParameterKey Key,
    bool IsRequired,
    bool IsDeprecated,
    ContentSchema? Schema,
    string Location);
