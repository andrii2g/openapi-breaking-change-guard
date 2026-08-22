using System.Text.Json;
using OpenApiBreakingChangeGuard.Diagnostics;

namespace OpenApiBreakingChangeGuard.References;

internal sealed record ReferenceResolution(
    bool IsSuccess,
    JsonElement Element,
    string CanonicalPointer,
    AnalysisDiagnostic? Diagnostic);
