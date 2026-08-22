namespace OpenApiBreakingChangeGuard.Documents;

/// <summary>Identifies an OpenAPI operation by exact path and lowercase method.</summary>
/// <param name="Path">The exact path key.</param>
/// <param name="Method">The lowercase HTTP method.</param>
public sealed record OperationKey(string Path, string Method) : IComparable<OperationKey>
{
    /// <inheritdoc />
    public int CompareTo(OperationKey? other)
    {
        if (other is null)
        {
            return 1;
        }

        var pathComparison = StringComparer.Ordinal.Compare(Path, other.Path);
        return pathComparison != 0
            ? pathComparison
            : StringComparer.Ordinal.Compare(Method, other.Method);
    }

    /// <summary>Returns an uppercase method and path display value.</summary>
    public override string ToString() => $"{Method.ToUpperInvariant()} {Path}";
}
