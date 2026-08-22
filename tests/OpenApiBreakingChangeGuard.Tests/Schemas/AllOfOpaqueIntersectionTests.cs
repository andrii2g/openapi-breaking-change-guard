using System.Text;
using OpenApiBreakingChangeGuard.Analysis;
using Xunit;

namespace OpenApiBreakingChangeGuard.Tests.Schemas;

public sealed class AllOfOpaqueIntersectionTests
{
    [Fact]
    public async Task StableOpaqueAllOfIntersectionDoesNotMakeAnalysisIncomplete()
    {
        var document = Document("""
            {
              "allOf": [
                { "type": "string", "minLength": 1 },
                { "type": "string", "maxLength": 10 }
              ]
            }
            """);

        var result = await AnalyzeAsync(document, document);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Changes);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task ChangedOpaqueAllOfConstraintIsVisibleAndIncomplete()
    {
        var oldDocument = Document("""{ "allOf": [{ "type": "string", "minLength": 1 }, { "type": "string", "maxLength": 10 }] }""");
        var newDocument = Document("""{ "allOf": [{ "type": "string", "minLength": 1 }, { "type": "string", "maxLength": 20 }] }""");

        var result = await AnalyzeAsync(oldDocument, newDocument);

        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OBCGD004");
    }

    [Fact]
    public async Task ChangedUnrepresentableAllOfShapeIsVisibleAndIncomplete()
    {
        var oldDocument = Document("""{ "allOf": [{ "oneOf": [{ "type": "string" }, { "type": "integer" }] }, { "type": "object" }] }""");
        var newDocument = Document("""{ "allOf": [{ "oneOf": [{ "type": "string" }, { "type": "number" }] }, { "type": "object" }] }""");

        var result = await AnalyzeAsync(oldDocument, newDocument);

        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OBCGD005");
    }

    private static string Document(string schema) => $$"""
        {
          "openapi": "3.1.0",
          "paths": {
            "/x": {
              "post": {
                "requestBody": { "content": { "application/json": { "schema": {{schema}} } } },
                "responses": {}
              }
            }
          }
        }
        """;

    private static async Task<AnalysisResult> AnalyzeAsync(string oldJson, string newJson)
    {
        await using var oldStream = new MemoryStream(Encoding.UTF8.GetBytes(oldJson));
        await using var newStream = new MemoryStream(Encoding.UTF8.GetBytes(newJson));
        return await new CompatibilityAnalyzer().AnalyzeAsync(oldStream, "old.json", newStream, "new.json");
    }
}
