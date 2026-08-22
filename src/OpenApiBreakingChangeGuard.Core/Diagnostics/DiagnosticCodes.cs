namespace OpenApiBreakingChangeGuard.Diagnostics;

/// <summary>Stable identifiers for analysis diagnostics.</summary>
public static class DiagnosticCodes
{
    /// <summary>A local reference is invalid or missing.</summary>
    public const string InvalidReference = "OBCGD001";
    /// <summary>A remote or file reference was encountered.</summary>
    public const string UnsupportedReference = "OBCGD002";
    /// <summary>A configured safety limit was exceeded.</summary>
    public const string LimitExceeded = "OBCGD003";
    /// <summary>An unsupported schema keyword changed.</summary>
    public const string UnsupportedKeyword = "OBCGD004";
    /// <summary>A schema composition could not be represented safely.</summary>
    public const string UnsupportedComposition = "OBCGD005";
    /// <summary>An unknown schema type was encountered.</summary>
    public const string UnknownType = "OBCGD006";
    /// <summary>A duplicate set member was ignored.</summary>
    public const string DuplicateValue = "OBCGD007";
    /// <summary>A malformed supported OpenAPI field was encountered.</summary>
    public const string InvalidStructure = "OBCGD008";
    /// <summary>A path parameter declaration was inconsistent.</summary>
    public const string InvalidPathParameter = "OBCGD009";
    /// <summary>A reference sibling was ignored under OpenAPI 3.0 rules.</summary>
    public const string ReferenceSiblingIgnored = "OBCGD010";
}
