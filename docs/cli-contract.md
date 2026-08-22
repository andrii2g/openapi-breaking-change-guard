# CLI contract

## Command name

Installed .NET tool command:

```text
openapi-guard
```

Development invocation:

```bash
dotnet run --project src/OpenApiBreakingChangeGuard.Cli -- [arguments]
```

## Syntax

```text
openapi-guard <old-spec> <new-spec> [options]

Arguments:
  <old-spec>                      Accepted OpenAPI JSON file.
  <new-spec>                      Candidate OpenAPI JSON file.

Options:
  --report <path>                 Write a Markdown report.
  --profile <client|strict>       Compatibility profile. Default: client.
  --include-info                  Include Info changes in console details.
  --max-console-changes <number>  Maximum detailed console findings. Default: 200.
  -h, --help                      Show help and return 0.
  --version                       Show version and return 0.
```

Do not introduce subcommands in the MVP.

## Parsing rules

- Options may appear before, between, or after positional arguments.
- `--report=value`, `--profile=value`, and
  `--max-console-changes=value` are accepted in addition to separated values.
- Unknown options fail with exit `2`.
- Duplicate scalar options fail with exit `2`; do not silently choose one.
- Exactly two positional arguments are required unless help/version is present.
- `--max-console-changes` accepts an invariant integer from `0` through `10,000`.
- Paths are passed to the filesystem as supplied, but reports show their
  normalized user-facing labels without resolving symlinks.
- `--report` must not resolve to either input file after full-path normalization.

Implement a small explicit parser. A command-line package is unnecessary for
this bounded grammar.

## Standard streams

- Normal summary and findings: stdout.
- Usage errors and fatal diagnostics: stderr.
- `--help` and `--version`: stdout.

CI annotations are deferred; do not print GitHub-specific `::error::` commands
from the CLI.

## Exit codes

| Code | Symbol | Meaning |
|---:|---|---|
| 0 | `Success` | Complete analysis, no breaking changes. |
| 1 | `BreakingChanges` | Complete analysis with breaking changes. |
| 2 | `InvalidInput` | CLI/file/JSON/OpenAPI envelope error. |
| 3 | `IncompleteAnalysis` | A supported decision could not be completed. |

Report-writing failure is an operational failure and returns `2`; the console
must explain that the semantic analysis completed but its requested report could
not be saved.

## Atomic Markdown output

1. Complete analysis and render Markdown in memory or a bounded temporary file.
2. Create a temporary file beside the destination.
3. Write UTF-8 without BOM using LF.
4. Flush and close.
5. Atomically replace/move into the destination where the platform permits.
6. Clean up the temporary file on failure.

Never truncate an existing report before successful analysis/rendering.

## Examples

```bash
openapi-guard old.json new.json
```

```bash
openapi-guard \
  baselines/openai.json \
  artifacts/openai-current.json \
  --profile client \
  --include-info \
  --report artifacts/openai-report.md
```

```bash
openapi-guard old.json new.json --max-console-changes 0
```

The last command still prints counts and returns the semantic exit code; it only
suppresses detailed console findings.

