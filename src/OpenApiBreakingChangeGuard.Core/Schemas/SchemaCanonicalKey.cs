using System.Text;

namespace OpenApiBreakingChangeGuard.Schemas;

internal static class SchemaCanonicalKey
{
    public static string Create(NormalizedSchema schema)
    {
        var builder = new StringBuilder();
        Append(builder, schema);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, NormalizedSchema schema)
    {
        switch (schema)
        {
            case AnySchema:
                builder.Append("any");
                break;
            case NeverSchema:
                builder.Append("never");
                break;
            case PrimitiveSchema primitive:
                builder.Append("primitive:");
                Segment(builder, primitive.Type);
                break;
            case EnumSchema enumSchema:
                builder.Append("enum:types[");
                foreach (var type in enumSchema.BaseTypes) Segment(builder, type);
                builder.Append("]values[");
                foreach (var value in enumSchema.Values) Segment(builder, value.Canonical);
                builder.Append(']');
                break;
            case ObjectSchema objectSchema:
                builder.Append("object:props[");
                foreach (var property in objectSchema.Properties.OrderBy(static item => item.Key, StringComparer.Ordinal))
                {
                    Segment(builder, property.Key);
                    Append(builder, property.Value.Schema);
                }
                builder.Append("]required[");
                foreach (var required in objectSchema.Required.Order(StringComparer.Ordinal)) Segment(builder, required);
                builder.Append("]additional:").Append(objectSchema.AdditionalProperties.Kind);
                if (objectSchema.AdditionalProperties.Schema is not null) Append(builder, objectSchema.AdditionalProperties.Schema);
                break;
            case ArraySchema array:
                builder.Append("array:");
                Append(builder, array.Items);
                break;
            case UnionSchema union:
                builder.Append("union:").Append(union.UnionKind).Append('[');
                foreach (var alternative in union.Alternatives.OrderBy(static item => item.CanonicalKey, StringComparer.Ordinal))
                {
                    Append(builder, alternative);
                }
                builder.Append(']');
                break;
            case RecursiveRefSchema recursive:
                builder.Append("recursive:");
                Segment(builder, recursive.Pointer);
                break;
            case UnsupportedSchema unsupported:
                builder.Append("unsupported:");
                Segment(builder, unsupported.Reason);
                break;
        }

        if (schema.OpaqueKeywords.Count > 0)
        {
            builder.Append("opaque[");
            foreach (var keyword in schema.OpaqueKeywords.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                Segment(builder, keyword.Key);
                Segment(builder, keyword.Value);
            }
            builder.Append(']');
        }
    }

    private static void Segment(StringBuilder builder, string value) =>
        builder.Append(value.Length).Append(':').Append(value).Append(';');
}
