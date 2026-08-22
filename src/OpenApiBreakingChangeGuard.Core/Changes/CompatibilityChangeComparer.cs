namespace OpenApiBreakingChangeGuard.Changes;

/// <summary>Orders compatibility findings according to the reporting contract.</summary>
public sealed class CompatibilityChangeComparer : IComparer<CompatibilityChange>, IEqualityComparer<CompatibilityChange>
{
    /// <summary>Gets the shared comparer.</summary>
    public static CompatibilityChangeComparer Instance { get; } = new();

    private CompatibilityChangeComparer()
    {
    }

    /// <inheritdoc />
    public int Compare(CompatibilityChange? x, CompatibilityChange? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        var comparison = Rank(x.Severity).CompareTo(Rank(y.Severity));
        if (comparison != 0) return comparison;
        comparison = StringComparer.Ordinal.Compare(x.Location, y.Location);
        if (comparison != 0) return comparison;
        comparison = StringComparer.Ordinal.Compare(x.RuleId, y.RuleId);
        if (comparison != 0) return comparison;
        comparison = StringComparer.Ordinal.Compare(x.OldValue ?? string.Empty, y.OldValue ?? string.Empty);
        return comparison != 0
            ? comparison
            : StringComparer.Ordinal.Compare(x.NewValue ?? string.Empty, y.NewValue ?? string.Empty);
    }

    /// <inheritdoc />
    public bool Equals(CompatibilityChange? x, CompatibilityChange? y) =>
        x is not null &&
        y is not null &&
        StringComparer.Ordinal.Equals(x.RuleId, y.RuleId) &&
        StringComparer.Ordinal.Equals(x.Location, y.Location) &&
        StringComparer.Ordinal.Equals(x.OldValue, y.OldValue) &&
        StringComparer.Ordinal.Equals(x.NewValue, y.NewValue);

    /// <inheritdoc />
    public int GetHashCode(CompatibilityChange obj) => HashCode.Combine(
        StringComparer.Ordinal.GetHashCode(obj.RuleId),
        StringComparer.Ordinal.GetHashCode(obj.Location),
        obj.OldValue is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.OldValue),
        obj.NewValue is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.NewValue));

    private static int Rank(ChangeSeverity severity) => severity switch
    {
        ChangeSeverity.Breaking => 0,
        ChangeSeverity.NonBreaking => 1,
        _ => 2,
    };
}
