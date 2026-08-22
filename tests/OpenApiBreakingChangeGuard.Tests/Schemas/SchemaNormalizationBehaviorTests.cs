using System.Text;
using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Changes;
using Xunit;

namespace OpenApiBreakingChangeGuard.Tests.Schemas;

public sealed class SchemaNormalizationBehaviorTests
{
    [Fact]
    public async Task Nullable30AndExplicitNull31AreEquivalent()
    {
        var oldJson = Document("3.0.3", """{ "type": "string", "nullable": true }""");
        var newJson = Document("3.1.0", """{ "type": ["null", "string"] }""");

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task AllOfObjectFragmentsMergeIndependentlyOfOrder()
    {
        var oldJson = Document("3.1.0", """{ "allOf": [{ "type": "object", "properties": { "a": { "type": "string" } } }, { "type": "object", "properties": { "b": { "type": "integer" } }, "required": ["b"] }] }""");
        var newJson = Document("3.1.0", """{ "allOf": [{ "required": ["b"], "properties": { "b": { "type": "integer" } }, "type": "object" }, { "properties": { "a": { "type": "string" } }, "type": "object" }] }""");

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task RecursiveLocalReferenceTerminatesAndFindsPropertyRemoval()
    {
        var oldJson = RecursiveDocument(includeName: true);
        var newJson = RecursiveDocument(includeName: false);

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.True(result.IsComplete);
        Assert.Contains(result.Changes, change => change.RuleId == RuleIds.PropertyRemoved && change.Location.EndsWith(".properties.name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EscapedReferenceTokensResolve()
    {
        var json = """
            {
              "openapi": "3.1.0",
              "paths": { "/x": { "get": { "responses": { "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/a~1b~0c" } } } } } } } },
              "components": { "schemas": { "a/b~c": { "type": "string" } } }
            }
            """;

        var result = await AnalyzeAsync(json, json);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Diagnostics);
    }

    private static string Document(string version, string schema) => $$"""
        {
          "openapi": "{{version}}",
          "paths": { "/x": { "post": { "requestBody": { "content": { "application/json": { "schema": {{schema}} } } }, "responses": {} } } }
        }
        """;

    private static string RecursiveDocument(bool includeName) => $$"""
        {
          "openapi": "3.1.0",
          "paths": { "/nodes": { "get": { "responses": { "200": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Node" } } } } } } } },
          "components": { "schemas": { "Node": { "type": "object", "properties": {
            "next": { "$ref": "#/components/schemas/Node" }{{(includeName ? ", \"name\": { \"type\": \"string\" }" : string.Empty)}}
          } } } }
        }
        """;

    private static async Task<AnalysisResult> AnalyzeAsync(string oldJson, string newJson)
    {
        await using var oldStream = new MemoryStream(Encoding.UTF8.GetBytes(oldJson));
        await using var newStream = new MemoryStream(Encoding.UTF8.GetBytes(newJson));
        return await new CompatibilityAnalyzer().AnalyzeAsync(oldStream, "old.json", newStream, "new.json");
    }
}
