namespace OpenApiBreakingChangeGuard.Documents;

internal sealed record IndexedResponse(
    string Key,
    IReadOnlyDictionary<string, ContentSchema> Content,
    string Location);
