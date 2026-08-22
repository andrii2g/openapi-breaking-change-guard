using System.Text.Json;

namespace OpenApiBreakingChangeGuard.Documents;

internal sealed record ContentSchema(JsonElement Schema, string Location, string Pointer);
