using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Changes;
using OpenApiBreakingChangeGuard.Diagnostics;
using OpenApiBreakingChangeGuard.Schemas;

namespace OpenApiBreakingChangeGuard.Rules;

internal sealed class SchemaRules
{
    private readonly ChangeCollector _changes;
    private readonly DiagnosticCollector _diagnostics;
    private readonly HashSet<string> _activeRecursivePairs = new(StringComparer.Ordinal);

    public SchemaRules(ChangeCollector changes, DiagnosticCollector diagnostics)
    {
        _changes = changes;
        _diagnostics = diagnostics;
    }

    public void Compare(
        NormalizedSchema oldSchema,
        NormalizedSchema newSchema,
        SchemaComparisonContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CompareOpaque(oldSchema, newSchema, context.Location);
        if (oldSchema.CanonicalKey == newSchema.CanonicalKey)
        {
            return;
        }

        if (oldSchema is UnsupportedSchema || newSchema is UnsupportedSchema)
        {
            _diagnostics.Add(new(
                DiagnosticSeverity.Error,
                DiagnosticCodes.UnsupportedComposition,
                context.Location,
                "The compared schema contains an unsupported shape."));
            return;
        }

        if (oldSchema is RecursiveRefSchema oldRecursive && newSchema is RecursiveRefSchema newRecursive)
        {
            var pair = $"{context.Usage}:{oldRecursive.Pointer.Length}:{oldRecursive.Pointer}:{newRecursive.Pointer}";
            if (!_activeRecursivePairs.Add(pair)) return;
            if (!StringComparer.Ordinal.Equals(oldRecursive.Pointer, newRecursive.Pointer))
            {
                _diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.UnsupportedComposition,
                    context.Location,
                    "Recursive schemas with different reference identities cannot be compared reliably."));
            }

            _activeRecursivePairs.Remove(pair);
            return;
        }

        switch (oldSchema, newSchema)
        {
            case (ObjectSchema oldObject, ObjectSchema newObject):
                CompareObjects(oldObject, newObject, context, cancellationToken);
                return;
            case (ArraySchema oldArray, ArraySchema newArray):
                Compare(oldArray.Items, newArray.Items, context with { Location = $"{context.Location}.items" }, cancellationToken);
                return;
            case (EnumSchema oldEnum, EnumSchema newEnum):
                CompareEnums(oldEnum, newEnum, context);
                return;
            case (UnionSchema oldUnion, UnionSchema newUnion):
                CompareUnions(oldUnion, newUnion, context);
                return;
            case (PrimitiveSchema oldPrimitive, PrimitiveSchema newPrimitive):
                AddTypeReplacement(oldPrimitive.Type, newPrimitive.Type, context);
                return;
            case (PrimitiveSchema oldPrimitive, EnumSchema newEnum):
                ComparePrimitiveToEnum(oldPrimitive, newEnum, context, primitiveIsOld: true);
                return;
            case (EnumSchema oldEnum, PrimitiveSchema newPrimitive):
                ComparePrimitiveToEnum(newPrimitive, oldEnum, context, primitiveIsOld: false);
                return;
            case (AnySchema, _):
                if (context.Usage == SchemaUsage.Request)
                {
                    AddTypeReplacement("any", Describe(newSchema), context);
                }
                else
                {
                    AddIncomplete(context.Location, "A response schema changed from unconstrained to a narrower shape that cannot be quantified by the MVP.");
                }
                return;
            case (_, AnySchema):
                if (context.Usage == SchemaUsage.Request)
                {
                    AddNonBreakingTypeChange(Describe(oldSchema), "any", context);
                }
                else
                {
                    AddIncomplete(context.Location, "A response schema widened to an unconstrained shape.");
                }
                return;
            case (NeverSchema, _):
                AddNonBreakingTypeChange("never", Describe(newSchema), context);
                return;
            case (_, NeverSchema):
                AddTypeReplacement(Describe(oldSchema), "never", context);
                return;
            default:
                AddTypeReplacement(Describe(oldSchema), Describe(newSchema), context);
                return;
        }
    }

    private void CompareObjects(
        ObjectSchema oldSchema,
        ObjectSchema newSchema,
        SchemaComparisonContext context,
        CancellationToken cancellationToken)
    {
        var oldNames = oldSchema.Properties.Keys.ToHashSet(StringComparer.Ordinal);
        var newNames = newSchema.Properties.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var name in oldNames.Except(newNames, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            _changes.Add(new(
                RuleIds.PropertyRemoved,
                ChangeSeverity.Breaking,
                ChangeCategory.Schema,
                $"{context.Location}.properties.{name}",
                $"{DisplayUsage(context.Usage)} property removed: {name}",
                name,
                null,
                context.Operation));
        }

        foreach (var name in newNames.Except(oldNames, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var required = newSchema.Required.Contains(name);
            var ruleId = required
                ? context.Usage == SchemaUsage.Request
                    ? RuleIds.RequiredRequestPropertyAdded
                    : RuleIds.RequiredResponsePropertyAdded
                : RuleIds.OptionalPropertyAdded;
            var severity = required && context.Usage == SchemaUsage.Request
                ? ChangeSeverity.Breaking
                : ChangeSeverity.NonBreaking;
            _changes.Add(new(
                ruleId,
                severity,
                ChangeCategory.Schema,
                $"{context.Location}.properties.{name}",
                $"{(required ? "Required" : "Optional")} {context.Usage.ToString().ToLowerInvariant()} property added: {name}",
                null,
                name,
                context.Operation));
        }

        foreach (var name in oldNames.Intersect(newNames, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var wasRequired = oldSchema.Required.Contains(name);
            var isRequired = newSchema.Required.Contains(name);
            if (wasRequired != isRequired)
            {
                var becameRequired = !wasRequired && isRequired;
                var breaking = context.Usage == SchemaUsage.Request ? becameRequired : !becameRequired;
                _changes.Add(new(
                    RuleIds.PropertyBecameRequired,
                    breaking ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                    ChangeCategory.Schema,
                    $"{context.Location}.properties.{name}",
                    $"{DisplayUsage(context.Usage)} property {name} became {(becameRequired ? "required" : "optional")}",
                    becameRequired ? "optional" : "required",
                    becameRequired ? "required" : "optional",
                    context.Operation));
            }

            Compare(
                oldSchema.Properties[name].Schema,
                newSchema.Properties[name].Schema,
                context with { Location = $"{context.Location}.properties.{name}" },
                cancellationToken);
        }

        if (!AdditionalEqual(oldSchema.AdditionalProperties, newSchema.AdditionalProperties))
        {
            AddIncomplete(
                $"{context.Location}.additionalProperties",
                "The additional-properties policy changed; compatibility analysis for this policy is outside the MVP.");
        }
    }

    private void CompareEnums(EnumSchema oldSchema, EnumSchema newSchema, SchemaComparisonContext context)
    {
        if (!oldSchema.BaseTypes.SequenceEqual(newSchema.BaseTypes, StringComparer.Ordinal))
        {
            AddTypeReplacement(
                string.Join('|', oldSchema.BaseTypes),
                string.Join('|', newSchema.BaseTypes),
                context);
            return;
        }

        var oldValues = oldSchema.Values.ToDictionary(static item => item.Canonical, StringComparer.Ordinal);
        var newValues = newSchema.Values.ToDictionary(static item => item.Canonical, StringComparer.Ordinal);
        var removed = oldValues.Keys.Except(newValues.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var added = newValues.Keys.Except(oldValues.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (removed.Length > 0 && added.Length > 0)
        {
            AddTypeReplacement("enum{" + string.Join(',', oldValues.Keys.Order(StringComparer.Ordinal)) + "}",
                "enum{" + string.Join(',', newValues.Keys.Order(StringComparer.Ordinal)) + "}", context);
            return;
        }

        if (removed.Length > 0)
        {
            _changes.Add(new(
                RuleIds.EnumNarrowed,
                context.Usage == SchemaUsage.Request ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                ChangeCategory.Schema,
                context.Location,
                $"{DisplayUsage(context.Usage)} enum value{Plural(removed.Length)} no longer allowed: {FormatValues(removed.Select(key => oldValues[key].Display))}",
                string.Join(',', removed),
                null,
                context.Operation));
        }

        if (added.Length > 0)
        {
            _changes.Add(new(
                RuleIds.EnumWidened,
                WideningSeverity(context),
                ChangeCategory.Schema,
                context.Location,
                $"{DisplayUsage(context.Usage)} enum value{Plural(added.Length)} added: {FormatValues(added.Select(key => newValues[key].Display))}",
                null,
                string.Join(',', added),
                context.Operation));
        }
    }

    private void CompareUnions(UnionSchema oldSchema, UnionSchema newSchema, SchemaComparisonContext context)
    {
        if (oldSchema.UnionKind != newSchema.UnionKind)
        {
            AddIncomplete(context.Location, $"Union composition changed from {oldSchema.UnionKind} to {newSchema.UnionKind}.");
            return;
        }

        var oldAlternatives = oldSchema.Alternatives.ToDictionary(static item => item.CanonicalKey, StringComparer.Ordinal);
        var newAlternatives = newSchema.Alternatives.ToDictionary(static item => item.CanonicalKey, StringComparer.Ordinal);
        foreach (var key in oldAlternatives.Keys.Except(newAlternatives.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var description = Describe(oldAlternatives[key]);
            _changes.Add(new(
                RuleIds.AlternativeRemoved,
                context.Usage == SchemaUsage.Request ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                ChangeCategory.Schema,
                context.Location,
                $"{DisplayUsage(context.Usage)} schema alternative no longer allowed: {description}",
                key,
                null,
                context.Operation));
        }

        foreach (var key in newAlternatives.Keys.Except(oldAlternatives.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var description = Describe(newAlternatives[key]);
            _changes.Add(new(
                RuleIds.AlternativeAdded,
                WideningSeverity(context),
                ChangeCategory.Schema,
                context.Location,
                $"{DisplayUsage(context.Usage)} schema alternative added: {description}",
                null,
                key,
                context.Operation));
        }
    }

    private void ComparePrimitiveToEnum(
        PrimitiveSchema primitive,
        EnumSchema enumSchema,
        SchemaComparisonContext context,
        bool primitiveIsOld)
    {
        if (enumSchema.BaseTypes.Count != 1 || enumSchema.BaseTypes[0] != primitive.Type)
        {
            AddTypeReplacement(
                primitiveIsOld ? primitive.Type : Describe(enumSchema),
                primitiveIsOld ? Describe(enumSchema) : primitive.Type,
                context);
            return;
        }

        var narrowing = primitiveIsOld;
        _changes.Add(new(
            narrowing ? RuleIds.EnumNarrowed : RuleIds.EnumWidened,
            narrowing
                ? context.Usage == SchemaUsage.Request ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking
                : WideningSeverity(context),
            ChangeCategory.Schema,
            context.Location,
            $"{DisplayUsage(context.Usage)} schema {(narrowing ? "was restricted to" : "was widened from")} an enum",
            primitiveIsOld ? primitive.Type : Describe(enumSchema),
            primitiveIsOld ? Describe(enumSchema) : primitive.Type,
            context.Operation));
    }

    private void CompareOpaque(NormalizedSchema oldSchema, NormalizedSchema newSchema, string location)
    {
        var keys = oldSchema.OpaqueKeywords.Keys.Union(newSchema.OpaqueKeywords.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            oldSchema.OpaqueKeywords.TryGetValue(key, out var oldValue);
            newSchema.OpaqueKeywords.TryGetValue(key, out var newValue);
            if (!StringComparer.Ordinal.Equals(oldValue, newValue))
            {
                _diagnostics.Add(new(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.UnsupportedKeyword,
                    $"{location}.{key}",
                    $"Unsupported schema keyword '{key}' was added, removed, or changed."));
            }
        }
    }

    private void AddTypeReplacement(string oldType, string newType, SchemaComparisonContext context) =>
        _changes.Add(new(
            RuleIds.PrimitiveTypeChanged,
            ChangeSeverity.Breaking,
            ChangeCategory.Schema,
            context.Location,
            $"Primitive type changed from {oldType} to {newType}",
            oldType,
            newType,
            context.Operation));

    private void AddNonBreakingTypeChange(string oldType, string newType, SchemaComparisonContext context) =>
        _changes.Add(new(
            RuleIds.PrimitiveTypeChanged,
            ChangeSeverity.NonBreaking,
            ChangeCategory.Schema,
            context.Location,
            $"Request schema relaxed from {oldType} to {newType}",
            oldType,
            newType,
            context.Operation));

    private void AddIncomplete(string location, string message) =>
        _diagnostics.Add(new(DiagnosticSeverity.Error, DiagnosticCodes.UnsupportedComposition, location, message));

    private static ChangeSeverity WideningSeverity(SchemaComparisonContext context) => context.Usage switch
    {
        SchemaUsage.Request => ChangeSeverity.NonBreaking,
        SchemaUsage.Response when context.Profile == CompatibilityProfile.Strict => ChangeSeverity.Breaking,
        _ => ChangeSeverity.Info,
    };

    private static bool AdditionalEqual(AdditionalPropertiesPolicy oldPolicy, AdditionalPropertiesPolicy newPolicy) =>
        oldPolicy.Kind == newPolicy.Kind && oldPolicy.Schema?.CanonicalKey == newPolicy.Schema?.CanonicalKey;

    private static string Describe(NormalizedSchema schema) => schema switch
    {
        PrimitiveSchema primitive => $"primitive:{primitive.Type}",
        EnumSchema enumSchema => $"enum:{string.Join('|', enumSchema.BaseTypes)}",
        ObjectSchema => "object",
        ArraySchema => "array",
        AnySchema => "any",
        NeverSchema => "never",
        RecursiveRefSchema recursive => $"recursive:{recursive.Pointer}",
        UnionSchema union => $"union:{union.UnionKind}",
        _ => "unsupported",
    };

    private static string DisplayUsage(SchemaUsage usage) => usage == SchemaUsage.Request ? "Request" : "Response";
    private static string Plural(int count) => count == 1 ? string.Empty : "s";
    private static string FormatValues(IEnumerable<string> values) => string.Join(", ", values.Order(StringComparer.Ordinal));
}
