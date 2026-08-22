using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Changes;
using OpenApiBreakingChangeGuard.Documents;

namespace OpenApiBreakingChangeGuard.Rules;

internal static class ParameterRules
{
    public static void Compare(
        IndexedOperation oldOperation,
        IndexedOperation newOperation,
        ChangeCollector changes,
        Action<ContentSchema, ContentSchema, string> compareSchemas,
        CancellationToken cancellationToken)
    {
        var oldKeys = oldOperation.Parameters.Keys.ToHashSet();
        var newKeys = newOperation.Parameters.Keys.ToHashSet();
        foreach (var key in oldKeys.Except(newKeys).Order())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var oldParameter = oldOperation.Parameters[key];
            changes.Add(new(
                RuleIds.ParameterRemoved,
                ChangeSeverity.Info,
                ChangeCategory.Parameter,
                oldParameter.Location,
                $"{DisplayLocation(key.Location)} parameter removed: {key.Name}",
                key.Name,
                null,
                oldOperation.Key));
        }

        foreach (var key in newKeys.Except(oldKeys).Order())
        {
            var parameter = newOperation.Parameters[key];
            changes.Add(new(
                parameter.IsRequired ? RuleIds.RequiredParameterAdded : RuleIds.OptionalParameterAdded,
                parameter.IsRequired ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                ChangeCategory.Parameter,
                parameter.Location,
                $"{(parameter.IsRequired ? "Required" : "Optional")} {key.Location} parameter added: {key.Name}",
                null,
                key.Name,
                newOperation.Key));
        }

        foreach (var key in oldKeys.Intersect(newKeys).Order())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var oldParameter = oldOperation.Parameters[key];
            var newParameter = newOperation.Parameters[key];
            if (!oldParameter.IsRequired && newParameter.IsRequired)
            {
                changes.Add(new(
                    RuleIds.ParameterBecameRequired,
                    ChangeSeverity.Breaking,
                    ChangeCategory.Parameter,
                    newParameter.Location,
                    $"{DisplayLocation(key.Location)} parameter {key.Name} became required",
                    "optional",
                    "required",
                    newOperation.Key));
            }

            if (!oldParameter.IsDeprecated && newParameter.IsDeprecated)
            {
                changes.Add(new(
                    RuleIds.DeprecationAdded,
                    ChangeSeverity.Info,
                    ChangeCategory.Deprecation,
                    newParameter.Location,
                    $"{DisplayLocation(key.Location)} parameter deprecated: {key.Name}",
                    "false",
                    "true",
                    newOperation.Key));
            }

            if (oldParameter.Schema is not null && newParameter.Schema is not null)
            {
                compareSchemas(oldParameter.Schema, newParameter.Schema, newParameter.Schema.Location);
            }
        }
    }

    private static string DisplayLocation(string location) =>
        string.Concat(location[0].ToString().ToUpperInvariant(), location.AsSpan(1));
}
