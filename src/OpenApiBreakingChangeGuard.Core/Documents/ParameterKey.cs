namespace OpenApiBreakingChangeGuard.Documents;

/// <summary>Identifies an effective parameter by exact location and name.</summary>
/// <param name="Location">The OpenAPI parameter location.</param>
/// <param name="Name">The exact parameter name.</param>
public sealed record ParameterKey(string Location, string Name) : IComparable<ParameterKey>
{
    /// <inheritdoc />
    public int CompareTo(ParameterKey? other)
    {
        if (other is null)
        {
            return 1;
        }

        var locationComparison = StringComparer.Ordinal.Compare(Location, other.Location);
        return locationComparison != 0
            ? locationComparison
            : StringComparer.Ordinal.Compare(Name, other.Name);
    }
}
