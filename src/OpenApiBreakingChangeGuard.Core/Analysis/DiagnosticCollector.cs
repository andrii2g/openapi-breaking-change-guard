using OpenApiBreakingChangeGuard.Diagnostics;

namespace OpenApiBreakingChangeGuard.Analysis;

internal sealed class DiagnosticCollector
{
    private readonly int _limit;
    private readonly List<AnalysisDiagnostic> _items = [];
    private bool _limitReported;

    public DiagnosticCollector(int limit) => _limit = limit;

    public IReadOnlyList<AnalysisDiagnostic> Items => _items;

    public void Add(AnalysisDiagnostic diagnostic)
    {
        if (_items.Count < _limit)
        {
            _items.Add(diagnostic);
        }
        else if (!_limitReported && _items.Count > 0)
        {
            _limitReported = true;
            _items[^1] = new(
                DiagnosticSeverity.Error,
                DiagnosticCodes.LimitExceeded,
                "analysis.diagnostics",
                $"The diagnostic limit of {_limit} was exceeded.");
        }
    }
}
