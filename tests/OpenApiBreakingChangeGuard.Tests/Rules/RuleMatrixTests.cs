using System.Text;
using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Changes;
using Xunit;

namespace OpenApiBreakingChangeGuard.Tests.Rules;

public sealed class RuleMatrixTests
{
    [Fact]
    public async Task EveryMvpRuleIdHasAPositiveFixture()
    {
        var oldJson = Wrap("""
            "/removed": { "get": { "responses": {} } },
            "/ops": {
              "delete": { "responses": {} },
              "get": {
                "parameters": [
                  { "in": "query", "name": "gone", "schema": { "type": "string" } },
                  { "in": "query", "name": "common", "schema": { "type": "string" } }
                ],
                "requestBody": { "content": {
                  "application/json": { "schema": {
                    "type": "object",
                    "properties": {
                      "gone": { "type": "string" },
                      "became_required": { "type": "string" },
                      "enum_narrow": { "type": "string", "enum": ["a", "b"] },
                      "enum_wide": { "type": "string", "enum": ["a"] },
                      "typed": { "type": "string" },
                      "union_remove": { "anyOf": [{"type":"string"},{"type":"null"},{"type":"boolean"}] },
                      "union_add": { "anyOf": [{"type":"string"},{"type":"null"}] }
                    }
                  } },
                  "text/plain": { "schema": { "type": "string" } }
                } },
                "responses": {
                  "200": { "content": { "application/json": { "schema": { "type": "object", "properties": { "response_required": { "type": "string" } } } }, "text/plain": { "schema": { "type": "string" } } } },
                  "404": {}
                }
              }
            }
            """);
        var newJson = Wrap("""
            "/added": { "get": { "responses": {} } },
            "/ops": {
              "post": { "responses": {} },
              "get": {
                "deprecated": true,
                "parameters": [
                  { "in": "query", "name": "common", "required": true, "deprecated": true, "schema": { "type": "string" } },
                  { "in": "query", "name": "required_new", "required": true, "schema": { "type": "string" } },
                  { "in": "query", "name": "optional_new", "schema": { "type": "string" } }
                ],
                "requestBody": { "content": {
                  "application/json": { "schema": {
                    "type": "object",
                    "required": ["became_required", "request_added"],
                    "properties": {
                      "became_required": { "type": "string" },
                      "request_added": { "type": "string" },
                      "optional_added": { "type": "string" },
                      "enum_narrow": { "type": "string", "enum": ["a"] },
                      "enum_wide": { "type": "string", "enum": ["a", "b"] },
                      "typed": { "type": "integer" },
                      "union_remove": { "anyOf": [{"type":"string"},{"type":"null"}] },
                      "union_add": { "anyOf": [{"type":"string"},{"type":"null"},{"type":"boolean"}] }
                    }
                  } },
                  "application/xml": { "schema": { "type": "string" } }
                } },
                "responses": {
                  "200": { "content": { "application/json": { "schema": { "type": "object", "required": ["response_required", "response_added"], "properties": { "response_required": { "type": "string" }, "response_added": { "type": "string" } } } }, "application/xml": { "schema": { "type": "string" } } } },
                  "201": {}
                }
              }
            }
            """);

        var result = await AnalyzeAsync(oldJson, newJson);
        var actual = result.Changes.Select(change => change.RuleId).ToHashSet(StringComparer.Ordinal);
        var expected = Enumerable.Range(1, 23).Select(number => $"OBCG{number:000}").ToArray();

        Assert.All(expected, ruleId => Assert.Contains(ruleId, actual));
    }

    [Fact]
    public async Task RepeatingAnalysisProducesStableOrdering()
    {
        var oldJson = Wrap("\"/b\": { \"get\": { \"responses\": {} } }, \"/a\": { \"get\": { \"responses\": {} } }");
        var newJson = Wrap(string.Empty);

        var first = await AnalyzeAsync(oldJson, newJson);
        var second = await AnalyzeAsync(oldJson, newJson);

        Assert.Equal(first.Changes, second.Changes);
        Assert.Equal(new[] { "paths./a", "paths./b" }, first.Changes.Select(change => change.Location));
    }

    private static async Task<AnalysisResult> AnalyzeAsync(string oldJson, string newJson)
    {
        await using var oldStream = new MemoryStream(Encoding.UTF8.GetBytes(oldJson));
        await using var newStream = new MemoryStream(Encoding.UTF8.GetBytes(newJson));
        return await new CompatibilityAnalyzer().AnalyzeAsync(oldStream, "old.json", newStream, "new.json");
    }

    private static string Wrap(string paths) => $$"""
        { "openapi": "3.1.0", "info": { "title": "rules", "version": "1" }, "paths": { {{paths}} } }
        """;
}
