namespace OpenApiBreakingChangeGuard.Schemas;

internal sealed record CanonicalEnumValue(string Canonical, string Display) : IComparable<CanonicalEnumValue>
{
    public int CompareTo(CanonicalEnumValue? other) =>
        other is null ? 1 : StringComparer.Ordinal.Compare(Canonical, other.Canonical);
}
