using OpenApiBreakingChangeGuard.References;
using OpenApiBreakingChangeGuard.Schemas;
using Xunit;

namespace OpenApiBreakingChangeGuard.Tests.References;

public sealed class ReferenceAndNumericTests
{
    [Fact]
    public void JsonPointerDecodesEscapedTokens()
    {
        Assert.Equal(new[] { "components", "schemas", "a/b~c" }, JsonPointer.Decode("#/components/schemas/a~1b~0c"));
    }

    [Theory]
    [InlineData("1", "1e0")]
    [InlineData("1.0", "1e0")]
    [InlineData("10e-1", "1e0")]
    [InlineData("-0.000", "0e0")]
    [InlineData("123.4500e2", "12345e0")]
    public void NumericCanonicalizerIsPrecisionSafe(string input, string expected)
    {
        Assert.Equal(expected, NumericCanonicalizer.Canonicalize(input));
    }

    [Fact]
    public void InvalidPointerEscapeIsRejected()
    {
        Assert.Throws<ArgumentException>(() => JsonPointer.Decode("#/bad~2token"));
        Assert.Throws<ArgumentException>(() => JsonPointer.Decode("https://example.test/schema"));
    }
}
