using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Documents;

namespace OpenApiBreakingChangeGuard.Schemas;

internal sealed record SchemaComparisonContext(
    SchemaUsage Usage,
    CompatibilityProfile Profile,
    OperationKey Operation,
    string Location);
