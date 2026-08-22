# OpenAPI Breaking Change Guard

CLI tool that compares an accepted OpenAPI contract with a candidate contract and fails
CI when the candidate can break existing clients.

The tool performs a semantic, direction-aware comparison. It does not report
irrelevant formatting, ordering, or description-only changes as compatibility
failures.

## Supported behavior

- Accept `old.json` and `new.json` containing OpenAPI 3.0 or 3.1 documents.
- Detect removed paths and methods.
- Detect new required parameters and parameters that became required.
- Detect removed response codes.
- Detect removed properties and required-property additions.
- Detect enum narrowing and primitive type changes.
- Detect removed `anyOf` or `oneOf` alternatives.
- Resolve local `$ref` values with cycle protection.
- Classify changes as `Breaking`, `NonBreaking`, or `Info`.
- Print a deterministic console report.
- Optionally write a deterministic Markdown report.
- Return a non-zero process exit code when breaking changes exist.
- Run in ordinary pull-request CI and in a scheduled OpenAI API monitor.

## Current non-goals

- YAML input.
- Remote references.
- Full JSON Schema satisfiability or equivalence.
- Callback and webhook compatibility.
- Security-scheme compatibility.
- Rename inference.
- Comparing example values or descriptions semantically.
- Automatically accepting or committing a new baseline.

See [docs/limitations.md](docs/limitations.md) for the exact boundary.

## Usage

```bash
openapi-guard old.json new.json \
  --report artifacts/compatibility-report.md
```

Included example summary:

```text
OpenAPI Breaking Change Guard

Old: examples/old.json (OpenAPI 3.1.0)
New: examples/new.json (OpenAPI 3.1.0)
Profile: client
Analysis: complete

Breaking:     6
NonBreaking:  4
Info:         0
```

Exit codes:

| Code | Meaning |
|---:|---|
| `0` | Analysis completed and no breaking changes were found. |
| `1` | Analysis completed and at least one breaking change was found. |
| `2` | CLI usage, file access, JSON, or OpenAPI validation error. |
| `3` | Analysis was incomplete because a configured safety/feature boundary was reached. |

## Why OpenAI is a useful target

The official OpenAI specification is a large OpenAPI 3.1 document with local
references and extensive `anyOf`, `oneOf`, and `allOf` usage. Recent real-world
changes include removing a response property and removing the `string`
alternative from a tool-call error union. Those examples make it a valuable
regression corpus for a structural compatibility tool.

The scheduled workflow downloads:

```text
https://raw.githubusercontent.com/openai/openai-openapi/main/openapi.json
```

and compares it with a reviewed file committed at:

- `baselines/openai.json`
- `baselines/openai-baseline.lock.json`

The accepted baseline is OpenAI commit
[`f9400172ebe08522ab228b771d885e3bd5456e42`](https://github.com/openai/openai-openapi/commit/f9400172ebe08522ab228b771d885e3bd5456e42)
from 2026-07-22. Its lock file records the pinned source, reviewer, acceptance
time, and SHA-256. The scheduled workflow verifies that hash and never updates
the accepted baseline automatically.

## Repository map

| Path | Purpose |
|---|---|
| `SPECIFICATION.md` | Functional requirements and invariants. |
| `baselines/` | Accepted OpenAI specification and provenance lock. |
| `docs/architecture.md` | Components, data flow, ownership, and algorithms. |
| `docs/compatibility-rules.md` | Complete MVP rule semantics and classifications. |
| `docs/schema-normalization.md` | `$ref`, composition, nullability, and canonical shapes. |
| `docs/cli-contract.md` | CLI syntax, validation, and exit-code contract. |
| `docs/reporting.md` | Console/Markdown formats and determinism rules. |
| `docs/testing.md` | Unit, integration, fixture, and OpenAI regression strategy. |
| `docs/openai-monitoring.md` | Baseline lifecycle and scheduled monitoring. |
| `examples/` | Small runnable old/new example and expected Markdown output. |
| `src/` | Core comparison library and thin CLI host. |
| `tests/` | xUnit tests and compatibility fixtures. |

## Development

Prerequisite: .NET 10 SDK.

```bash
dotnet restore OpenApiBreakingChangeGuard.slnx
dotnet build OpenApiBreakingChangeGuard.slnx --configuration Release --no-restore
dotnet test OpenApiBreakingChangeGuard.slnx --configuration Release --no-build
```

Run the included example:

```bash
dotnet run \
  --project src/OpenApiBreakingChangeGuard.Cli \
  -- examples/old.json examples/new.json \
  --report artifacts/example-report.md
```

## Packaging

The CLI project is configured as a .NET tool:

```bash
dotnet pack src/OpenApiBreakingChangeGuard.Cli \
  --configuration Release \
  --output artifacts/packages

dotnet tool install \
  --global \
  --add-source artifacts/packages \
  OpenApiBreakingChangeGuard
```

## Status

Version 0.1 is implemented, tested, packable as `openapi-guard`, and exercised
against the reviewed OpenAI baseline. The scheduled monitor is configured in
`.github/workflows/monitor-openai.yml`; baseline replacement remains a
deliberately human-reviewed operation described in `docs/openai-monitoring.md`.

## License

MIT.

