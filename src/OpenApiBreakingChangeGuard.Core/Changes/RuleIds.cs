namespace OpenApiBreakingChangeGuard.Changes;

/// <summary>Stable identifiers for compatibility rules.</summary>
public static class RuleIds
{
    /// <summary>Path removed.</summary>
    public const string PathRemoved = "OBCG001";
    /// <summary>Operation removed.</summary>
    public const string OperationRemoved = "OBCG002";
    /// <summary>Path added.</summary>
    public const string PathAdded = "OBCG003";
    /// <summary>Operation added.</summary>
    public const string OperationAdded = "OBCG004";
    /// <summary>Required parameter added.</summary>
    public const string RequiredParameterAdded = "OBCG005";
    /// <summary>Optional parameter added.</summary>
    public const string OptionalParameterAdded = "OBCG006";
    /// <summary>Parameter became required.</summary>
    public const string ParameterBecameRequired = "OBCG007";
    /// <summary>Parameter removed.</summary>
    public const string ParameterRemoved = "OBCG008";
    /// <summary>Response code removed.</summary>
    public const string ResponseRemoved = "OBCG009";
    /// <summary>Response code added.</summary>
    public const string ResponseAdded = "OBCG010";
    /// <summary>Property removed.</summary>
    public const string PropertyRemoved = "OBCG011";
    /// <summary>Optional property added.</summary>
    public const string OptionalPropertyAdded = "OBCG012";
    /// <summary>Required request property added.</summary>
    public const string RequiredRequestPropertyAdded = "OBCG013";
    /// <summary>Required response property added.</summary>
    public const string RequiredResponsePropertyAdded = "OBCG014";
    /// <summary>Existing property became required.</summary>
    public const string PropertyBecameRequired = "OBCG015";
    /// <summary>Enum narrowed.</summary>
    public const string EnumNarrowed = "OBCG016";
    /// <summary>Enum widened.</summary>
    public const string EnumWidened = "OBCG017";
    /// <summary>Primitive type changed.</summary>
    public const string PrimitiveTypeChanged = "OBCG018";
    /// <summary>Schema alternative removed.</summary>
    public const string AlternativeRemoved = "OBCG019";
    /// <summary>Schema alternative added.</summary>
    public const string AlternativeAdded = "OBCG020";
    /// <summary>Deprecation added.</summary>
    public const string DeprecationAdded = "OBCG021";
    /// <summary>Media type removed.</summary>
    public const string MediaTypeRemoved = "OBCG022";
    /// <summary>Media type added.</summary>
    public const string MediaTypeAdded = "OBCG023";
}
