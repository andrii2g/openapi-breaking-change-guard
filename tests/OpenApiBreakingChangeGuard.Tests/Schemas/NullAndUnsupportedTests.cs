using System.Text;
using OpenApiBreakingChangeGuard.Analysis;
using Xunit;

namespace OpenApiBreakingChangeGuard.Tests.Schemas;

public sealed class NullAndUnsupportedTests
{
    [Fact]
    public async Task NullableObjectAndExplicitNullObjectUnionAreEquivalent()
    {
        var oldJson = Document("3.0.3", """{ "type": "object", "nullable": true, "properties": { "id": { "type": "string" } } }""");
        var newJson = Document("3.1.0", """{ "type": ["object", "null"], "properties": { "id": { "type": "string" } } }""");

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task NullableEnumAndExplicitNullAlternativeAreEquivalent()
    {
        var oldJson = Document("3.0.3", """{ "type": "string", "nullable": true, "enum": ["ready"] }""");
        var newJson = Document("3.1.0", """{ "anyOf": [{ "type": "string", "enum": ["ready"] }, { "type": "null" }] }""");

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task UnsupportedOnlyChangeIsIncompleteWithoutGuessedFinding()
    {
        var oldJson = Document("3.1.0", """{ "type": "string", "minLength": 1 }""");
        var newJson = Document("3.1.0", """{ "type": "string", "minLength": 2 }""");

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.False(result.IsComplete);
        Assert.Empty(result.Changes);
    }

    private static string Document(string version, string schema) => $$"""
        { "openapi": "{{version}}", "paths": { "/x": { "post": { "requestBody": { "content": { "application/json": { "schema": {{schema}} } } }, "responses": {} } } } }
        """;

    private static async Task<AnalysisResult> AnalyzeAsync(string oldJson, string newJson)
    {
        await using var oldStream = new MemoryStream(Encoding.UTF8.GetBytes(oldJson));
        await using var newStream = new MemoryStream(Encoding.UTF8.GetBytes(newJson));
        return await new CompatibilityAnalyzer().AnalyzeAsync(oldStream, "old.json", newStream, "new.json");
    }
}
