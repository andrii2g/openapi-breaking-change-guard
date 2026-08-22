# Product specification

## 1. Purpose

The product answers one bounded question:

> Can an existing client built against the old OpenAPI contract continue to
> communicate with a server conforming to the new contract?

It compares semantic API structure rather than source text. It is conservative
when a supported change can invalidate an old request or invalidate assumptions
made while reading a new response.

## 2. Terminology

- **Old document**: accepted contract used by existing clients.
- **New document**: candidate server contract.
- **Request position**: parameter, request body, or schema reached from them.
- **Response position**: response body or schema reached from it.
- **Breaking**: an old valid client interaction may no longer conform.
- **NonBreaking**: a supported additive or relaxing change.
- **Info**: a meaningful change without an MVP compatibility failure.
- **Incomplete**: analysis could not safely decide under the supported subset.
- **Logical location**: stable API-oriented path such as
  `paths./v1/widgets.get.responses.200.content.application/json.schema`.

## 3. Inputs

The CLI requires exactly two positional paths:

```text
openapi-guard OLD.json NEW.json
```

Both files must:

- Exist and be readable.
- Be UTF-8 JSON, with or without a UTF-8 BOM.
- Contain a JSON object at the root.
- Contain an `openapi` string beginning with `3.0.` or `3.1.`.
- Contain a `paths` object.

Unknown top-level fields and `x-*` extensions are permitted and ignored.

## 4. Outputs

The analysis result contains:

- Old and new source labels.
- Detected OpenAPI versions.
- Optional caller-supplied old and new source revisions and HTTPS file URLs.
- Ordered changes.
- Ordered diagnostics.
- Counts by severity.
- A deterministic method-level review summary derived from breaking findings.
- Whether analysis was complete.

Console output is always produced unless a future quiet option is approved.
Markdown output is produced only when `--report PATH` is supplied.

Reports must not contain current time, machine paths beyond user-supplied
labels, random IDs, or culture-sensitive formatting. Optional source revisions
are treated as display metadata and must never be inferred through network or
filesystem access by the core.

## 5. Exit contract

Precedence when selecting an exit code:

1. Invalid CLI/input: `2`.
2. Incomplete analysis: `3`.
3. Complete analysis with breaking changes: `1`.
4. Complete analysis without breaking changes: `0`.

Thus an incomplete analysis never passes merely because no breaking change was
found in the portion that was examined.

## 6. Supported document surface

### 6.1 Operations

- Path keys beginning with `/`.
- HTTP methods: `get`, `put`, `post`, `delete`, `options`, `head`, `patch`,
  and `trace`.
- Path-level and operation-level `parameters`.
- Operation `requestBody`.
- Operation `responses`.

Ignore path-item metadata such as `summary`, `description`, `servers`, and
vendor extensions.

### 6.2 Parameters

Identify a parameter by the ordered pair `(in, name)`. Supported locations are
`path`, `query`, `header`, and `cookie`. Parameter names are compared ordinally;
header names are intentionally not folded in the MVP.

An operation inherits path-level parameters. An operation-level parameter with
the same `(in, name)` replaces the inherited parameter.

Parameter schemas may be inline or locally referenced. Parameter objects may
also be locally referenced.

### 6.3 Request bodies and responses

Compare schemas separately for every media type in `content`. Media types are
opaque ordinal strings. Do not guess compatibility between
`application/json` and `application/*+json`.

Response keys are opaque ordinal strings, including `default`, `2XX`, and
vendor-specific values. Do not parse them as integers for identity.

### 6.4 Schemas

Support:

- Boolean schemas (`true` and `false`) where OpenAPI 3.1 permits them.
- `type` as a string.
- `type` arrays if present in JSON Schema style input.
- `properties`.
- `required`.
- `enum` containing primitive JSON values or `null`.
- `items`.
- `additionalProperties` as a boolean or schema.
- `nullable` for OpenAPI 3.0.
- Local `$ref`.
- `allOf`, `anyOf`, and `oneOf`.

Other validation keywords are retained only as unsupported metadata for the
MVP and must not accidentally influence implemented rules.

## 7. Safety limits

Defaults, exposed through internal `AnalysisLimits` rather than CLI options in
the MVP:

| Limit | Default |
|---|---:|
| Input file size | 32 MiB per file |
| JSON nesting depth | 256 |
| Local-reference expansion depth | 128 |
| Unique references resolved | 100,000 |
| Schema nodes normalized | 500,000 |
| Changes retained | 10,000 |
| Diagnostics retained | 1,000 |

Exceeding a limit produces an error diagnostic and incomplete exit code `3`.

## 8. Change identity

Every change has:

- Stable rule ID.
- Severity.
- Category.
- Logical location.
- Summary.
- Optional old value.
- Optional new value.
- Optional operation identity.

Duplicate changes with the same rule ID, logical location, and canonical old/new
values must be collapsed.

## 9. Deterministic order

Sort changes by:

1. Severity rank: Breaking, NonBreaking, Info.
2. Logical location, ordinal.
3. Rule ID, ordinal.
4. Canonical old value, ordinal.
5. Canonical new value, ordinal.

Sort diagnostics by severity, code, and location. Use `\n` in generated
Markdown on all operating systems.

## 10. Functional acceptance criteria

1. Removing `/v1/widgets` reports one breaking path removal and does not also
   report each removed operation below that path.
2. Removing only `POST /v1/widgets` reports one breaking operation removal.
3. Adding an optional query parameter reports `NonBreaking`.
4. Adding a required query parameter reports `Breaking`.
5. Changing an optional parameter to required reports `Breaking` once.
6. Removing response key `404` reports `Breaking`.
7. Removing a response property reports `Breaking`.
8. Adding a required request property reports `Breaking`.
9. Adding a required response property reports `NonBreaking` under the default
   client profile.
10. Narrowing a request enum reports `Breaking`.
11. Removing a primitive alternative from `anyOf` reports `Breaking`.
12. Changing `string` to `integer` reports `Breaking`.
13. Reordering JSON object keys, required entries, enum entries, union branches,
    or `allOf` branches produces no change.
14. Description-only and example-only edits produce only configured `Info`
    results or no results; never `Breaking`.
15. A missing local reference produces a visible diagnostic and exit `3`.
16. The same inputs produce byte-identical Markdown reports across repeated
    executions.
17. Breaking findings produce exit `1` after the report is successfully written.
18. Invalid JSON produces exit `2` and never overwrites an existing report.


