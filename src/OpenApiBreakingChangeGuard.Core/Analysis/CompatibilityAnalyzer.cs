using OpenApiBreakingChangeGuard.Documents;
using OpenApiBreakingChangeGuard.Loading;
using OpenApiBreakingChangeGuard.Rules;
using OpenApiBreakingChangeGuard.Schemas;

namespace OpenApiBreakingChangeGuard.Analysis;

/// <summary>Analyzes two OpenAPI JSON streams for client compatibility.</summary>
public sealed class CompatibilityAnalyzer
{
    /// <summary>Loads, indexes, normalizes, and compares two OpenAPI documents.</summary>
    /// <param name="oldDocument">The accepted OpenAPI JSON stream.</param>
    /// <param name="oldLabel">The stable user-facing old input label.</param>
    /// <param name="newDocument">The candidate OpenAPI JSON stream.</param>
    /// <param name="newLabel">The stable user-facing new input label.</param>
    /// <param name="options">Optional analysis settings.</param>
    /// <param name="cancellationToken">A token that cancels loading or analysis.</param>
    /// <returns>The immutable analysis result.</returns>
    /// <exception cref="OpenApiLoadException">An input is not valid supported OpenAPI JSON.</exception>
    public async Task<AnalysisResult> AnalyzeAsync(
        Stream oldDocument,
        string oldLabel,
        Stream newDocument,
        string newLabel,
        AnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(oldDocument);
        ArgumentNullException.ThrowIfNull(newDocument);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(newLabel);
        options ??= new AnalysisOptions();
        ValidateLimits(options.Limits);

        using var oldLoaded = await OpenApiJsonLoader.LoadAsync(
            oldDocument,
            oldLabel,
            options.Limits,
            cancellationToken).ConfigureAwait(false);
        using var newLoaded = await OpenApiJsonLoader.LoadAsync(
            newDocument,
            newLabel,
            options.Limits,
            cancellationToken).ConfigureAwait(false);

        var diagnostics = new DiagnosticCollector(options.Limits.MaxDiagnostics);
        var changes = new ChangeCollector(options.Limits.MaxChanges, diagnostics);
        var oldIndexed = OpenApiDocumentIndexer.Index(oldLoaded, options.Limits, diagnostics, cancellationToken);
        var newIndexed = OpenApiDocumentIndexer.Index(newLoaded, options.Limits, diagnostics, cancellationToken);
        var oldNormalizer = new SchemaNormalizer(oldIndexed.Resolver, oldLoaded.IsVersion31, options.Limits, diagnostics);
        var newNormalizer = new SchemaNormalizer(newIndexed.Resolver, newLoaded.IsVersion31, options.Limits, diagnostics);
        var schemaRules = new SchemaRules(changes, diagnostics);

        void CompareSchema(
            ContentSchema oldSchema,
            ContentSchema newSchema,
            SchemaUsage usage,
            string location,
            OperationKey operation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var oldShape = oldNormalizer.Normalize(oldSchema, usage, cancellationToken);
            var newShape = newNormalizer.Normalize(newSchema, usage, cancellationToken);
            schemaRules.Compare(
                oldShape,
                newShape,
                new(usage, options.Profile, operation, location),
                cancellationToken);
        }

        void CompareOperation(IndexedOperation oldOperation, IndexedOperation newOperation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ParameterRules.Compare(
                oldOperation,
                newOperation,
                changes,
                (oldSchema, newSchema, location) => CompareSchema(
                    oldSchema,
                    newSchema,
                    SchemaUsage.Request,
                    location,
                    oldOperation.Key),
                cancellationToken);
            ResponseRules.Compare(
                oldOperation,
                newOperation,
                changes,
                (oldSchema, newSchema, usage, location) => CompareSchema(
                    oldSchema,
                    newSchema,
                    usage,
                    location,
                    oldOperation.Key),
                cancellationToken);
        }

        OperationRules.Compare(oldIndexed, newIndexed, changes, CompareOperation, cancellationToken);
        return new(
            oldLabel,
            newLabel,
            oldLoaded.Version,
            newLoaded.Version,
            options.Profile,
            changes.Items,
            diagnostics.Items);
    }

    private static void ValidateLimits(AnalysisLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.MaxInputBytes <= 0 || limits.MaxJsonDepth <= 0 || limits.MaxReferenceDepth <= 0 ||
            limits.MaxUniqueReferences <= 0 || limits.MaxSchemaNodes <= 0 || limits.MaxChanges <= 0 ||
            limits.MaxDiagnostics <= 0)
        {
            throw new ArgumentException("Every analysis limit must be positive.", nameof(limits));
        }
    }
}
