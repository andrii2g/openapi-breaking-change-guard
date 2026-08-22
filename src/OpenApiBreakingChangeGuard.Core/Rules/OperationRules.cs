using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Changes;
using OpenApiBreakingChangeGuard.Documents;

namespace OpenApiBreakingChangeGuard.Rules;

internal static class OperationRules
{
    public static void Compare(
        IndexedDocument oldDocument,
        IndexedDocument newDocument,
        ChangeCollector changes,
        Action<IndexedOperation, IndexedOperation> compareCommon,
        CancellationToken cancellationToken)
    {
        var oldPaths = oldDocument.Paths.Keys.ToHashSet(StringComparer.Ordinal);
        var newPaths = newDocument.Paths.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var path in oldPaths.Except(newPaths, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            changes.Add(new(
                RuleIds.PathRemoved,
                ChangeSeverity.Breaking,
                ChangeCategory.Path,
                $"paths.{path}",
                $"Path removed: {path}",
                path,
                null));
        }

        foreach (var path in newPaths.Except(oldPaths, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            changes.Add(new(
                RuleIds.PathAdded,
                ChangeSeverity.NonBreaking,
                ChangeCategory.Path,
                $"paths.{path}",
                $"Path added: {path}",
                null,
                path));
        }

        foreach (var path in oldPaths.Intersect(newPaths, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var oldMethods = oldDocument.Paths[path].ToHashSet(StringComparer.Ordinal);
            var newMethods = newDocument.Paths[path].ToHashSet(StringComparer.Ordinal);
            foreach (var method in oldMethods.Except(newMethods, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                var key = new OperationKey(path, method);
                changes.Add(new(
                    RuleIds.OperationRemoved,
                    ChangeSeverity.Breaking,
                    ChangeCategory.Operation,
                    $"paths.{path}.{method}",
                    $"Operation removed: {key}",
                    key.ToString(),
                    null,
                    key));
            }

            foreach (var method in newMethods.Except(oldMethods, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                var key = new OperationKey(path, method);
                changes.Add(new(
                    RuleIds.OperationAdded,
                    ChangeSeverity.NonBreaking,
                    ChangeCategory.Operation,
                    $"paths.{path}.{method}",
                    $"Operation added: {key}",
                    null,
                    key.ToString(),
                    key));
            }

            foreach (var method in oldMethods.Intersect(newMethods, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = new OperationKey(path, method);
                var oldOperation = oldDocument.Operations[key];
                var newOperation = newDocument.Operations[key];
                if (!oldOperation.IsDeprecated && newOperation.IsDeprecated)
                {
                    changes.Add(new(
                        RuleIds.DeprecationAdded,
                        ChangeSeverity.Info,
                        ChangeCategory.Deprecation,
                        oldOperation.Location,
                        $"Operation deprecated: {key}",
                        "false",
                        "true",
                        key));
                }

                compareCommon(oldOperation, newOperation);
            }
        }
    }
}
