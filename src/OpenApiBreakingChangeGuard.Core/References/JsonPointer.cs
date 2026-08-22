namespace OpenApiBreakingChangeGuard.References;

/// <summary>Decodes local RFC 6901 JSON Pointers.</summary>
public static class JsonPointer
{
    /// <summary>Decodes a local fragment pointer into exact tokens.</summary>
    /// <param name="reference">A reference beginning with <c>#/</c>.</param>
    /// <returns>Decoded pointer tokens.</returns>
    public static IReadOnlyList<string> Decode(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only local references beginning with '#/' are supported.", nameof(reference));
        }

        return reference[2..].Split('/').Select(DecodeToken).ToArray();
    }

    private static string DecodeToken(string token)
    {
        var result = new System.Text.StringBuilder(token.Length);
        for (var index = 0; index < token.Length; index++)
        {
            if (token[index] != '~')
            {
                result.Append(token[index]);
                continue;
            }

            if (++index >= token.Length || (token[index] != '0' && token[index] != '1'))
            {
                throw new ArgumentException("The JSON Pointer contains an invalid '~' escape.", nameof(token));
            }

            result.Append(token[index] == '0' ? '~' : '/');
        }

        return result.ToString();
    }
}
