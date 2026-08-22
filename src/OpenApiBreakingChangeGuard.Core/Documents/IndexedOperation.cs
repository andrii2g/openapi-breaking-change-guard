namespace OpenApiBreakingChangeGuard.Documents;

internal sealed record IndexedOperation(
    OperationKey Key,
    bool IsDeprecated,
    IReadOnlyDictionary<ParameterKey, IndexedParameter> Parameters,
    IReadOnlyDictionary<string, ContentSchema> RequestContent,
    IReadOnlyDictionary<string, IndexedResponse> Responses,
    string Location);
