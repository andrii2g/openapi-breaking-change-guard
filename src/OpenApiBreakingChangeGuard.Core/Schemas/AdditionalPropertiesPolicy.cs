namespace OpenApiBreakingChangeGuard.Schemas;

internal enum AdditionalPropertiesKind
{
    Unspecified,
    AllowedAny,
    Forbidden,
    AllowedSchema,
}

internal sealed record AdditionalPropertiesPolicy(AdditionalPropertiesKind Kind, NormalizedSchema? Schema = null)
{
    public static AdditionalPropertiesPolicy Unspecified { get; } = new(AdditionalPropertiesKind.Unspecified);
}
