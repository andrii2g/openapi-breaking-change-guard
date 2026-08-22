# Reporting

## Principles

- Lead with the outcome.
- Preserve stable rule IDs for CI searches and suppressions added later.
- Show logical API locations, not raw implementation object dumps.
- Keep console output compact; put full detail in Markdown.
- Produce deterministic, timestamp-free content.

## Console format

Header and totals:

```text
OpenAPI Breaking Change Guard

Old: baselines/openai.json @ f9400172ebe08522ab228b771d885e3bd5456e42 (OpenAPI 3.1.0)
New: artifacts/openai-current.json @ f85dbe223d40e1a31cba812ab2d755c7e98a92a3 (OpenAPI 3.1.0)
Profile: client
Analysis: complete

Breaking:     6
NonBreaking:  3
Info:         1
```

Detailed findings:

```text
BREAKING    OBCG002  paths./v1/widgets.post
  Operation removed: POST /v1/widgets

NONBREAKING OBCG003  paths./v1/health
  Path added: /v1/health
```

Diagnostics appear after changes:

```text
Diagnostics:
WARNING OBCGD007 components.schemas.Widget.required
  Duplicate required property name "id" was ignored.
```

When findings exceed the console limit:

```text
... 37 additional changes omitted from console output.
Use --report <path> for the complete report.
```

ANSI color may be added only when output is an interactive terminal and
`NO_COLOR` is not set. Tests assert the uncolored form. Color never changes
content or spacing.

## Markdown format

Required sections:

1. Title.
2. Verdict block.
3. Input metadata.
4. Counts table.
5. API methods requiring review.
6. Breaking changes.
7. Non-breaking changes.
8. Informational changes.
9. Diagnostics.
10. Supported-scope note.

Template:

```markdown
# OpenAPI compatibility report

> **Verdict: Breaking changes detected.**

| Input | File | Revision | OpenAPI |
|---|---|---|---|
| Old | `baselines/openai.json` | [`f9400172ebe08522ab228b771d885e3bd5456e42`](https://github.com/openai/openai-openapi/blob/f9400172ebe08522ab228b771d885e3bd5456e42/openapi.json) | `3.1.0` |
| New | `artifacts/openai-current.json` | [`f85dbe223d40e1a31cba812ab2d755c7e98a92a3`](https://github.com/openai/openai-openapi/blob/f85dbe223d40e1a31cba812ab2d755c7e98a92a3/openapi.json) | `3.1.0` |

| Severity | Count |
|---|---:|
| Breaking | 2 |
| NonBreaking | 3 |
| Info | 1 |

## API methods requiring review

2 API methods have breaking findings and should be reviewed before accepting the candidate.

| Method | Path | Breaking findings | Review focus |
|---|---|---:|---|
| `POST` | `/v1/widgets` | 2 | `OBCG002` — Operation removed<br>`OBCG013` — Required request property added |
| `GET` | `/v1/widgets` | 1 | `OBCG011` — Property removed |

## Breaking changes

### OBCG002 — Operation removed

- Location: `paths./v1/widgets.post`
- Change: Operation removed: `POST /v1/widgets`.
```

The `Revision` column is included when either input has revision metadata. A
missing revision is shown as an em dash. When neither revision is supplied, the
column is omitted to preserve compact output for ordinary local file comparisons.
If a validated revision URL is present, the revision becomes a direct link to
the exact source document.

The review table includes only operations with breaking findings. Rows are
sorted by descending finding count, then path and method using ordinal order.
Distinct rule IDs summarize why each operation needs attention. Breaking path
findings that do not belong to one operation are called out separately.

## Escaping

- Escape `|` and line breaks inside tables.
- Use backticks for paths, methods, parameters, properties, response keys, media
  types, enum values, and schema alternatives.
- Replace embedded backtick runs safely; do not construct invalid inline code.
- HTML from descriptions must never be injected because descriptions are not
  rendered as compatibility explanations.
- Keep summaries generated from trusted templates plus escaped identifiers.

## Location grammar

Use dot-separated API tokens with literal path preserved:

```text
paths./v1/widgets.get
paths./v1/widgets.get.parameters.query.limit
paths./v1/widgets.get.responses.200.content.application/json.schema.properties.id
components.schemas.Widget.properties.id
```

When a property is shared through a component, prefer the use-site operation
location in findings and include the component definition pointer as secondary
origin detail. This explains client impact better than reporting only a global
component.

## Verdict wording

- Complete + breaking: `Breaking changes detected.`
- Complete + no breaking: `No breaking changes detected.`
- Incomplete: `Analysis incomplete; compatibility could not be established.`

Incomplete takes precedence even if breaking findings also exist.

## Golden files

Reporter tests compare exact UTF-8 bytes with LF endings. Golden reports contain
no absolute temporary paths or timestamps. Updating a golden file requires
review of every changed line.

