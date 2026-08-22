using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Changes;
using OpenApiBreakingChangeGuard.Documents;
using OpenApiBreakingChangeGuard.Schemas;

namespace OpenApiBreakingChangeGuard.Rules;

internal static class ResponseRules
{
    public static void Compare(
        IndexedOperation oldOperation,
        IndexedOperation newOperation,
        ChangeCollector changes,
        Action<ContentSchema, ContentSchema, SchemaUsage, string> compareSchemas,
        CancellationToken cancellationToken)
    {
        CompareContent(
            oldOperation.RequestContent,
            newOperation.RequestContent,
            $"{oldOperation.Location}.requestBody.content",
            SchemaUsage.Request,
            oldOperation.Key,
            changes,
            compareSchemas,
            cancellationToken);

        var oldKeys = oldOperation.Responses.Keys.ToHashSet(StringComparer.Ordinal);
        var newKeys = newOperation.Responses.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var key in oldKeys.Except(newKeys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            changes.Add(new(
                RuleIds.ResponseRemoved,
                ChangeSeverity.Breaking,
                ChangeCategory.Response,
                $"{oldOperation.Location}.responses.{key}",
                $"Response {key} was removed from {oldOperation.Key}",
                key,
                null,
                oldOperation.Key));
        }

        foreach (var key in newKeys.Except(oldKeys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            changes.Add(new(
                RuleIds.ResponseAdded,
                ChangeSeverity.NonBreaking,
                ChangeCategory.Response,
                $"{newOperation.Location}.responses.{key}",
                $"Response {key} was added to {newOperation.Key}",
                null,
                key,
                newOperation.Key));
        }

        foreach (var key in oldKeys.Intersect(newKeys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompareContent(
                oldOperation.Responses[key].Content,
                newOperation.Responses[key].Content,
                $"{oldOperation.Location}.responses.{key}.content",
                SchemaUsage.Response,
                oldOperation.Key,
                changes,
                compareSchemas,
                cancellationToken);
        }
    }

    private static void CompareContent(
        IReadOnlyDictionary<string, ContentSchema> oldContent,
        IReadOnlyDictionary<string, ContentSchema> newContent,
        string location,
        SchemaUsage usage,
        OperationKey operation,
        ChangeCollector changes,
        Action<ContentSchema, ContentSchema, SchemaUsage, string> compareSchemas,
        CancellationToken cancellationToken)
    {
        var oldTypes = oldContent.Keys.ToHashSet(StringComparer.Ordinal);
        var newTypes = newContent.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var mediaType in oldTypes.Except(newTypes, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            changes.Add(new(
                RuleIds.MediaTypeRemoved,
                ChangeSeverity.Breaking,
                ChangeCategory.MediaType,
                $"{location}.{mediaType}",
                $"{DisplayUsage(usage)} media type removed: {mediaType}",
                mediaType,
                null,
                operation));
        }

        foreach (var mediaType in newTypes.Except(oldTypes, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            changes.Add(new(
                RuleIds.MediaTypeAdded,
                ChangeSeverity.NonBreaking,
                ChangeCategory.MediaType,
                $"{location}.{mediaType}",
                $"{DisplayUsage(usage)} media type added: {mediaType}",
                null,
                mediaType,
                operation));
        }

        foreach (var mediaType in oldTypes.Intersect(newTypes, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            compareSchemas(oldContent[mediaType], newContent[mediaType], usage, newContent[mediaType].Location);
        }
    }

    private static string DisplayUsage(SchemaUsage usage) => usage == SchemaUsage.Request ? "Request" : "Response";
}
