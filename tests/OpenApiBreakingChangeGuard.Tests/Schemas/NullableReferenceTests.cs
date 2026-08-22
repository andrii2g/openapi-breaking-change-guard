using System.Text;
using OpenApiBreakingChangeGuard.Analysis;
using Xunit;

namespace OpenApiBreakingChangeGuard.Tests.Schemas;

public sealed class NullableReferenceTests
{
    [Fact]
    public async Task NullableReferenceSiblingPreservesReferencedObjectShape()
    {
        var oldDocument = Document(includeAddedProperty: false);
        var newDocument = Document(includeAddedProperty: true);

        var result = await AnalyzeAsync(oldDocument, newDocument);

        Assert.True(result.IsComplete);
        Assert.NotEmpty(result.Changes);
        Assert.Empty(result.Diagnostics);
    }

    private static string Document(bool includeAddedProperty) => $$"""
        {
          "openapi": "3.1.0",
          "paths": {
            "/x": {
              "get": {
                "responses": {
                  "200": {
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/Value", "nullable": true }
                      }
                    }
                  }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "Value": {
                "type": "object",
                "properties": {
                  "id": { "type": "string" }{{(includeAddedProperty ? ", \"name\": { \"type\": \"string\" }" : string.Empty)}}
                }
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
