namespace OpenApiBreakingChangeGuard.Loading;

/// <summary>Represents an invalid or unreadable OpenAPI JSON input.</summary>
public sealed class OpenApiLoadException : Exception
{
    /// <summary>Initializes an input exception.</summary>
    public OpenApiLoadException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes an input exception with an underlying cause.</summary>
    public OpenApiLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
