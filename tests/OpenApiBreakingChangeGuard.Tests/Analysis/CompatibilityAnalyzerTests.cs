using System.Text;
using OpenApiBreakingChangeGuard.Analysis;
using OpenApiBreakingChangeGuard.Changes;
using OpenApiBreakingChangeGuard.Loading;
using Xunit;

namespace OpenApiBreakingChangeGuard.Tests.Analysis;

public sealed class CompatibilityAnalyzerTests
{
    [Fact]
    public async Task IdenticalReorderedDocumentHasNoChanges()
    {
        var oldJson = Document("""
            "/items": {
              "get": {
                "responses": {
                  "200": { "description": "ok", "content": { "application/json": { "schema": {
                    "type": "object", "required": ["b", "a"],
                    "properties": { "a": { "enum": [1.0, 2] }, "b": { "anyOf": [{"type":"null"},{"type":"string"}] } }
                  } } } }
                }
              }
            }
            """);
        var newJson = Document("""
            "/items": {
              "get": {
                "description": "changed only",
                "responses": {
                  "200": { "description": "different", "content": { "application/json": { "schema": {
                    "properties": { "b": { "anyOf": [{"type":"string"},{"type":"null"}] }, "a": { "enum": [2.0, 1] } },
                    "required": ["a", "b"], "type": "object"
                  } } } }
                }
              }
            }
            """);

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task RemovedPathSuppressesDescendantOperations()
    {
        var oldJson = Document("\"/gone\": { \"get\": { \"responses\": { \"200\": { \"description\": \"ok\" } } }, \"post\": { \"responses\": {} } }");
        var result = await AnalyzeAsync(oldJson, Document(string.Empty));

        var change = Assert.Single(result.Changes);
        Assert.Equal(RuleIds.PathRemoved, change.RuleId);
    }

    [Fact]
    public async Task PathParameterInheritanceAndOperationOverrideAreEffective()
    {
        var oldJson = Document("""
            "/items/{id}": {
              "parameters": [{ "in": "query", "name": "limit", "required": true, "schema": { "type": "integer" } }],
              "get": { "parameters": [{ "in": "query", "name": "limit", "required": false, "schema": { "type": "integer" } }], "responses": {} }
            }
            """);
        var newJson = Document("""
            "/items/{id}": {
              "parameters": [{ "in": "query", "name": "limit", "required": true, "schema": { "type": "integer" } }],
              "get": { "parameters": [{ "in": "query", "name": "limit", "required": true, "schema": { "type": "integer" } }], "responses": {} }
            }
            """);

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.Equal(RuleIds.ParameterBecameRequired, Assert.Single(result.Changes).RuleId);
    }

    [Fact]
    public async Task ResponseKeysAreOpaqueAndSortedOrdinally()
    {
        var oldJson = Document("\"/x\": { \"get\": { \"responses\": { \"default\": {}, \"2XX\": {}, \"200\": {}, \"x-range\": {} } } }");
        var newJson = Document("\"/x\": { \"get\": { \"responses\": {} } }");

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.Equal(new[] { "200", "2XX", "default", "x-range" }, result.Changes.Select(change => change.OldValue));
        Assert.All(result.Changes, change => Assert.Equal(RuleIds.ResponseRemoved, change.RuleId));
    }

    [Fact]
    public async Task RequestAndResponsePropertyAdditionsAreDirectional()
    {
        var oldJson = OperationWithSchemas("""{ "type": "object", "properties": {} }""", """{ "type": "object", "properties": {} }""");
        var newJson = OperationWithSchemas(
            """{ "type": "object", "properties": { "request_id": { "type": "string" } }, "required": ["request_id"] }""",
            """{ "type": "object", "properties": { "response_id": { "type": "string" } }, "required": ["response_id"] }""");

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.Contains(result.Changes, change => change.RuleId == RuleIds.RequiredRequestPropertyAdded && change.Severity == ChangeSeverity.Breaking);
        Assert.Contains(result.Changes, change => change.RuleId == RuleIds.RequiredResponsePropertyAdded && change.Severity == ChangeSeverity.NonBreaking);
    }

    [Fact]
    public async Task EnumAndUnionChangesRespectDirectionAndStrictProfile()
    {
        var oldSchema = """{ "type": "object", "properties": { "state": { "type": "string", "enum": ["a"] }, "value": { "anyOf": [{"type":"string"}] } } }""";
        var newSchema = """{ "type": "object", "properties": { "state": { "type": "string", "enum": ["a", "b"] }, "value": { "anyOf": [{"type":"string"},{"type":"null"}] } } }""";

        var client = await AnalyzeAsync(OperationWithSchemas(oldSchema, oldSchema), OperationWithSchemas(newSchema, newSchema));
        var strict = await AnalyzeAsync(OperationWithSchemas(oldSchema, oldSchema), OperationWithSchemas(newSchema, newSchema), CompatibilityProfile.Strict);

        Assert.Contains(client.Changes, change => change.RuleId == RuleIds.EnumWidened && change.Severity == ChangeSeverity.NonBreaking && change.Location.Contains("requestBody", StringComparison.Ordinal));
        Assert.Contains(client.Changes, change => change.RuleId == RuleIds.EnumWidened && change.Severity == ChangeSeverity.Info && change.Location.Contains("responses", StringComparison.Ordinal));
        Assert.Contains(client.Changes, change => change.RuleId == RuleIds.AlternativeAdded && change.Severity == ChangeSeverity.Info && change.Location.Contains("responses", StringComparison.Ordinal));
        Assert.Contains(strict.Changes, change => change.RuleId == RuleIds.EnumWidened && change.Severity == ChangeSeverity.Breaking && change.Location.Contains("responses", StringComparison.Ordinal));
        Assert.Contains(strict.Changes, change => change.RuleId == RuleIds.AlternativeAdded && change.Severity == ChangeSeverity.Breaking && change.Location.Contains("responses", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingAndRemoteReferencesMakeAnalysisIncomplete()
    {
        var oldJson = OperationWithSchemas("""{ "$ref": "#/components/schemas/Missing" }""", """{ "type": "string" }""");
        var newJson = OperationWithSchemas("""{ "$ref": "https://example.test/schema.json" }""", """{ "type": "string" }""");

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OBCGD001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OBCGD002");
    }

    [Fact]
    public async Task ChangedUnsupportedConstraintMakesAnalysisIncomplete()
    {
        var oldJson = OperationWithSchemas("""{ "type": "string", "minLength": 1 }""", """{ "type": "string" }""");
        var newJson = OperationWithSchemas("""{ "type": "string", "minLength": 2 }""", """{ "type": "string" }""");

        var result = await AnalyzeAsync(oldJson, newJson);

        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OBCGD004");
    }

    [Fact]
    public async Task InvalidEnvelopeThrowsInputException()
    {
        await Assert.ThrowsAsync<OpenApiLoadException>(() => AnalyzeAsync("{", Document(string.Empty)));
        await Assert.ThrowsAsync<OpenApiLoadException>(() => AnalyzeAsync("{}", Document(string.Empty)));
    }

    [Fact]
    public async Task ReducedOpenAiFixturesProduceExpectedRules()
    {
        var root = FindRepositoryRoot();
        var audit = Path.Combine(root, "tests", "OpenApiBreakingChangeGuard.Tests", "Fixtures", "openai-audit-source-removed");
        var mcp = Path.Combine(root, "tests", "OpenApiBreakingChangeGuard.Tests", "Fixtures", "openai-mcp-error-union-narrowed");

        var auditResult = await AnalyzeAsync(await File.ReadAllTextAsync(Path.Combine(audit, "old.json")), await File.ReadAllTextAsync(Path.Combine(audit, "new.json")));
        var mcpResult = await AnalyzeAsync(await File.ReadAllTextAsync(Path.Combine(mcp, "old.json")), await File.ReadAllTextAsync(Path.Combine(mcp, "new.json")));

        Assert.Equal(RuleIds.PropertyRemoved, Assert.Single(auditResult.Changes).RuleId);
        var mcpChange = Assert.Single(mcpResult.Changes);
        Assert.Equal(RuleIds.AlternativeRemoved, mcpChange.RuleId);
        Assert.Equal(ChangeSeverity.NonBreaking, mcpChange.Severity);
    }

    private static async Task<AnalysisResult> AnalyzeAsync(
        string oldJson,
        string newJson,
        CompatibilityProfile profile = CompatibilityProfile.Client)
    {
        await using var oldStream = new MemoryStream(Encoding.UTF8.GetBytes(oldJson));
        await using var newStream = new MemoryStream(Encoding.UTF8.GetBytes(newJson));
        return await new CompatibilityAnalyzer().AnalyzeAsync(
            oldStream,
            "old.json",
            newStream,
            "new.json",
            new AnalysisOptions { Profile = profile });
    }

    private static string Document(string paths) => $$"""
        {
          "openapi": "3.1.0",
          "info": { "title": "test", "version": "1" },
          "paths": { {{paths}} }
        }
        """;

    private static string OperationWithSchemas(string requestSchema, string responseSchema) => Document($$"""
        "/items": {
          "post": {
            "requestBody": { "content": { "application/json": { "schema": {{requestSchema}} } } },
            "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": {{responseSchema}} } } } }
          }
        }
        """);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenApiBreakingChangeGuard.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
