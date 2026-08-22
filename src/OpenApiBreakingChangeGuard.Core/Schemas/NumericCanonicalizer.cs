using System.Globalization;
using System.Numerics;

namespace OpenApiBreakingChangeGuard.Schemas;

/// <summary>Canonicalizes finite JSON number text without floating-point conversion.</summary>
public static class NumericCanonicalizer
{
    /// <summary>Returns a canonical coefficient-and-decimal-exponent representation.</summary>
    /// <param name="rawNumber">Valid JSON number text.</param>
    public static string Canonicalize(string rawNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawNumber);
        var index = 0;
        var negative = rawNumber[index] == '-';
        if (negative) index++;

        var exponentMarker = rawNumber.IndexOfAny(['e', 'E'], index);
        var mantissaEnd = exponentMarker < 0 ? rawNumber.Length : exponentMarker;
        var dot = rawNumber.IndexOf('.', index, mantissaEnd - index);
        var integerEnd = dot < 0 ? mantissaEnd : dot;
        var integerPart = rawNumber[index..integerEnd];
        var fractionPart = dot < 0 ? string.Empty : rawNumber[(dot + 1)..mantissaEnd];
        var coefficient = (integerPart + fractionPart).TrimStart('0');
        if (coefficient.Length == 0)
        {
            return "0e0";
        }

        var exponent = exponentMarker < 0
            ? BigInteger.Zero
            : BigInteger.Parse(rawNumber[(exponentMarker + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        exponent -= fractionPart.Length;
        var trailingZeros = coefficient.Length - coefficient.TrimEnd('0').Length;
        if (trailingZeros > 0)
        {
            coefficient = coefficient[..^trailingZeros];
            exponent += trailingZeros;
        }

        return $"{(negative ? "-" : string.Empty)}{coefficient}e{exponent.ToString(CultureInfo.InvariantCulture)}";
    }
}
