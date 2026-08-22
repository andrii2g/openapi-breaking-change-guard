# Testing strategy

The test suite is intentionally compact and behavior-oriented. It currently
contains 40 tests and runs as part of the .NET 10 solution.

## Current test groups

### Analysis

`Analysis/CompatibilityAnalyzerTests.cs` covers:

- Reordered but equivalent documents.
- Removed-path cascade suppression.
- Path-parameter inheritance and operation override.
- Opaque response keys and ordinal ordering.
- Request/response directionality for properties, enums, and unions.
- Client versus strict profile behavior.
- Missing and remote references.
- Changed unsupported constraints.
- Invalid OpenAPI envelopes.
- Reduced OpenAI-shaped regressions.

### Rule matrix

`Rules/RuleMatrixTests.cs` exercises every stable rule ID from OBCG001 through
OBCG023 and verifies repeatable ordering.

### Schema normalization

The `Schemas/` tests cover:

- OpenAPI 3.0 `nullable` equivalence with OpenAPI 3.1 `null` alternatives.
- OpenAPI 3.1 `$ref` with `nullable: true` siblings.
- Order-independent object `allOf` merging.
- Stable opaque `allOf` intersections and visible changed intersections.
- Recursive local references.
- Unsupported keyword changes.

### References and canonical values

`References/ReferenceAndNumericTests.cs` covers RFC 6901 escaping, invalid
pointer escapes, and precision-safe numeric canonicalization.

### Reporting

`Reporting/ReporterTests.cs` verifies deterministic LF-only Markdown, escaping,
result sorting/deduplication, and console detail limits.

### Process-level CLI

`Cli/CliProcessTests.cs` launches the built CLI and covers exit codes 0, 1, 2,
and 3, plus preservation of an existing report after invalid input. Tests use
the built apphost or current `dotnet` host; they do not invoke `dotnet run`.

### Workflow support

`scripts/test-provider-status.sh` verifies the provider badge and metadata
mappings for guard exit codes 0 through 3 and rejects a result whose exit code
conflicts with its report.

## Fixture conventions

Reduced external shapes live under `Fixtures/`, with `old.json`, `new.json`, and
`expected.json`. They are hand-authored and small enough for direct review. The
current fixtures model an OpenAI audit response property removal and an MCP
error-union narrowing.

Most synthetic cases are inline in test classes because the whole contract fits
on screen. Add a fixture when a shape is reused or is clearer as standalone
JSON; do not create one directory per assertion mechanically.

## OpenAI validation

The complete accepted specification is committed at `baselines/openai.json` but
is not copied into the unit-test assembly. Validation has two layers:

1. Normal CI runs the reduced OpenAI-shaped fixtures.
2. The scheduled monitor verifies the baseline SHA-256, downloads the current
   official specification, runs the full comparison, and uploads the report and
   input metadata.

A manual local baseline self-check is:

```bash
dotnet run --project src/OpenApiBreakingChangeGuard.Cli -- \
  baselines/openai.json baselines/openai.json \
  --report artifacts/openai-baseline-self-check.md
```

Expect complete analysis, zero changes, and exit `0`. Warning diagnostics from
duplicate upstream set entries or required names without local property schemas
remain visible and do not make analysis incomplete.

## Required checks

Run before committing:

```bash
bash scripts/test-provider-status.sh
dotnet build OpenApiBreakingChangeGuard.slnx --configuration Release
dotnet test --solution OpenApiBreakingChangeGuard.slnx --configuration Release --no-build
dotnet format OpenApiBreakingChangeGuard.slnx --verify-no-changes --no-restore
```

For new semantic behavior, add a focused regression and test both request and
response direction when severity depends on usage. Determinism matters more than
line coverage: repeated analysis must produce the same ordered results and
byte-identical Markdown.
