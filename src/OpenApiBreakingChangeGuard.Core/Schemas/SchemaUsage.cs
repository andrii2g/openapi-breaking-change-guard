namespace OpenApiBreakingChangeGuard.Schemas;

/// <summary>Identifies whether a schema is used for a request or response.</summary>
public enum SchemaUsage
{
    /// <summary>The server accepts values described by the schema.</summary>
    Request,
    /// <summary>The server returns values described by the schema.</summary>
    Response,
}
