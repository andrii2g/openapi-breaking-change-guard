namespace OpenApiBreakingChangeGuard.Changes;

/// <summary>Groups compatibility findings by API concept.</summary>
public enum ChangeCategory
{
    /// <summary>A path changed.</summary>
    Path,
    /// <summary>An operation changed.</summary>
    Operation,
    /// <summary>A parameter changed.</summary>
    Parameter,
    /// <summary>A response changed.</summary>
    Response,
    /// <summary>A media type changed.</summary>
    MediaType,
    /// <summary>A schema changed.</summary>
    Schema,
    /// <summary>Deprecation metadata changed.</summary>
    Deprecation,
}
