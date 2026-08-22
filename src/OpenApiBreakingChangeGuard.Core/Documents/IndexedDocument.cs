using OpenApiBreakingChangeGuard.Loading;
using OpenApiBreakingChangeGuard.References;

namespace OpenApiBreakingChangeGuard.Documents;

internal sealed record IndexedDocument(
    LoadedOpenApiDocument Source,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Paths,
    IReadOnlyDictionary<OperationKey, IndexedOperation> Operations,
    LocalReferenceResolver Resolver);
