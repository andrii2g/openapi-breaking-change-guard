using OpenApiBreakingChangeGuard.Changes;
using OpenApiBreakingChangeGuard.Diagnostics;

namespace OpenApiBreakingChangeGuard.Analysis;

internal sealed class ChangeCollector
{
    private readonly int _limit;
    private readonly DiagnosticCollector _diagnostics;
    private readonly List<CompatibilityChange> _items = [];
    private bool _limitReported;

    public ChangeCollector(int limit, DiagnosticCollector diagnostics)
    {
        _limit = limit;
        _diagnostics = diagnostics;
    }

    public IReadOnlyList<CompatibilityChange> Items => _items;

    public void Add(CompatibilityChange change)
    {
        if (_items.Count < _limit)
        {
            _items.Add(change);
        }
        else if (!_limitReported)
        {
            _limitReported = true;
            _diagnostics.Add(new(
                DiagnosticSeverity.Error,
                DiagnosticCodes.LimitExceeded,
                "analysis.changes",
                $"The change limit of {_limit} was exceeded."));
        }
    }
}
