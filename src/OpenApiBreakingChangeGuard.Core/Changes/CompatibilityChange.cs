using OpenApiBreakingChangeGuard.Documents;

namespace OpenApiBreakingChangeGuard.Changes;

/// <summary>Represents one deterministic, explainable compatibility finding.</summary>
public sealed record CompatibilityChange
{
    /// <summary>Initializes a compatibility finding.</summary>
    public CompatibilityChange(
        string ruleId,
        ChangeSeverity severity,
        ChangeCategory category,
        string location,
        string summary,
        string? oldValue = null,
        string? newValue = null,
        OperationKey? operation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        RuleId = ruleId;
        Severity = severity;
        Category = category;
        Location = location;
        Summary = summary;
        OldValue = oldValue;
        NewValue = newValue;
        Operation = operation;
    }

    /// <summary>Gets the stable rule identifier.</summary>
    public string RuleId { get; }
    /// <summary>Gets the compatibility severity.</summary>
    public ChangeSeverity Severity { get; }
    /// <summary>Gets the finding category.</summary>
    public ChangeCategory Category { get; }
    /// <summary>Gets the stable logical API location.</summary>
    public string Location { get; }
    /// <summary>Gets the human-readable summary.</summary>
    public string Summary { get; }
    /// <summary>Gets the optional canonical old value.</summary>
    public string? OldValue { get; }
    /// <summary>Gets the optional canonical new value.</summary>
    public string? NewValue { get; }
    /// <summary>Gets the containing operation when applicable.</summary>
    public OperationKey? Operation { get; }
}
