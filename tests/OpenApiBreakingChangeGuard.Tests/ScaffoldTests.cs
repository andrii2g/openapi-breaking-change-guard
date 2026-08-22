using OpenApiBreakingChangeGuard;
using Xunit;

namespace OpenApiBreakingChangeGuard.Tests;

public sealed class ScaffoldTests
{
    [Fact]
    public void ProductName_IsStable()
    {
        Assert.Equal("OpenAPI Breaking Change Guard", ProjectMarker.ProductName);
    }
}
