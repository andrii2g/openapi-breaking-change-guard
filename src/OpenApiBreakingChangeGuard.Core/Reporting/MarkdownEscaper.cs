namespace OpenApiBreakingChangeGuard.Reporting;

/// <summary>Escapes values embedded in deterministic Markdown reports.</summary>
public static class MarkdownEscaper
{
    /// <summary>Escapes a table cell and replaces line breaks with HTML breaks.</summary>
    public static string TableCell(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r\n", "<br>", StringComparison.Ordinal)
        .Replace("\r", "<br>", StringComparison.Ordinal)
        .Replace("\n", "<br>", StringComparison.Ordinal);

    /// <summary>Wraps arbitrary text in a valid Markdown inline-code span.</summary>
    public static string InlineCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var longestRun = 0;
        var currentRun = 0;
        foreach (var character in value)
        {
            if (character == '`')
            {
                longestRun = Math.Max(longestRun, ++currentRun);
            }
            else
            {
                currentRun = 0;
            }
        }

        var delimiter = new string('`', longestRun + 1);
        var padding = value.StartsWith('`') || value.EndsWith('`') ||
            (value.StartsWith(' ') && value.EndsWith(' ')) ? " " : string.Empty;
        return $"{delimiter}{padding}{value}{padding}{delimiter}";
    }
}
