namespace OpenApiBreakingChangeGuard.References;

internal sealed class ReferenceResolutionCache
{
    private readonly Dictionary<string, ReferenceResolution> _items = new(StringComparer.Ordinal);

    public int Count => _items.Count;

    public bool TryGet(string reference, out ReferenceResolution resolution) =>
        _items.TryGetValue(reference, out resolution!);

    public void Add(string reference, ReferenceResolution resolution) => _items[reference] = resolution;
}
