# OpenAPI Breaking Change Guard

[![CI](https://github.com/andrii2g/openapi-breaking-change-guard/actions/workflows/ci.yml/badge.svg)](https://github.com/andrii2g/openapi-breaking-change-guard/actions/workflows/ci.yml)

## Monitored API compatibility

Each row compares a human-reviewed **accepted baseline** with the exact upstream
commit checked by the latest monitor run. A passing monitor means the comparison
completed successfully; it does **not** mean the candidate is compatible when
the comparison says that review is required.

| Provider | Monitor | Latest comparison |
|---|---|---|
| OpenAI API | [![OpenAI API monitor](https://github.com/andrii2g/openapi-breaking-change-guard/actions/workflows/monitor-openai.yml/badge.svg)](https://github.com/andrii2g/openapi-breaking-change-guard/actions/workflows/monitor-openai.yml) | [![OpenAI API latest comparison](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fapi.github.com%2Frepos%2Fandrii2g%2Fopenapi-breaking-change-guard%2Fcommits%2F525a28277a7e94e998e090131a4082b5d4dd97e8%2Fstatus&query=%24.statuses%5B0%5D.description&label=OpenAI%20API&color=blue&cacheSeconds=300)](https://github.com/andrii2g/openapi-breaking-change-guard/actions/workflows/monitor-openai.yml) |

The comparison reads `accepted commit → checked commit`. For example,
`review: 42 breaking · 19 methods` means 42 possible client-breaking findings
were detected across 19 API methods between those exact snapshots. Select the
badge, then open the latest run for the affected-method queue and full report.

CLI tool that compares an accepted OpenAPI contract with a candidate contract and fails
CI when the candidate can break existing clients.

The tool performs a semantic, direction-aware comparison. It does not report
irrelevant formatting, ordering, or description-only changes as compatibility
failures. Its Markdown report leads with the affected API methods and links to
exact source revisions when commit metadata is supplied.

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
- Optionally write a deterministic Markdown report with an API-method review
  summary before exhaustive findings.
- Record source revisions in reports and link them directly to exact HTTPS source
  files.
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

Basic comparison:

```bash
openapi-guard old.json new.json \
  --report artifacts/compatibility-report.md
```

Traceable comparison with exact source revisions:

```bash
openapi-guard \
  baselines/openai.json \
  artifacts/openai-current.json \
  --old-revision f9400172ebe08522ab228b771d885e3bd5456e42 \
  --new-revision f85dbe223d40e1a31cba812ab2d755c7e98a92a3 \
  --old-revision-url https://github.com/openai/openai-openapi/blob/f9400172ebe08522ab228b771d885e3bd5456e42/openapi.json \
  --new-revision-url https://github.com/openai/openai-openapi/blob/f85dbe223d40e1a31cba812ab2d755c7e98a92a3/openapi.json \
  --profile client \
  --include-info \
  --report artifacts/openai-compatibility-report.md
```

Revision values are optional labels. A revision URL must be an absolute HTTPS
URL and makes that revision a direct link in the Markdown input table. Neither
value changes compatibility analysis.

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

The generated Markdown report turns breaking findings into an initial review
queue before listing all details:

| Method | Path | Breaking findings | Review focus |
|---|---|---:|---|
| `GET` | `/v1/widgets` | 4 | Parameter required; response removed; property removed; type changed |
| `POST` | `/v1/widgets` | 1 | Operation removed |

See [examples/expected-report.md](examples/expected-report.md) for the complete
deterministic example and [docs/cli-contract.md](docs/cli-contract.md) for every
option.

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
time, and SHA-256. The scheduled workflow verifies that hash, resolves and
downloads an exact current upstream commit, and includes both commit hashes as
links to their exact `openapi.json` files. It never updates the accepted baseline
automatically.

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
| `scripts/` | Reusable provider-status generation and workflow support. |
| `src/` | Core comparison library and thin CLI host. |
| `tests/` | xUnit tests and compatibility fixtures. |

## Development

Prerequisite: .NET 10 SDK.

```bash
dotnet restore OpenApiBreakingChangeGuard.slnx
dotnet build OpenApiBreakingChangeGuard.slnx --configuration Release --no-restore
dotnet test --solution OpenApiBreakingChangeGuard.slnx --configuration Release --no-build
dotnet format OpenApiBreakingChangeGuard.slnx --verify-no-changes --no-restore
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

