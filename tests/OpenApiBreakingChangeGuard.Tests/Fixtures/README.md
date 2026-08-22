# Fixture conventions

Each directory contains `old.json`, `new.json`, and `expected.json`.

The two included OpenAI-shaped fixtures are reduced, hand-authored regressions:

- `openai-audit-source-removed` models a removed response property.
- `openai-mcp-error-union-narrowed` models removal of the primitive `string`
  alternative from an `anyOf` response field.

Most synthetic rule cases remain inline in focused test classes. Add another
fixture when a reusable or real-world shape is easier to review as standalone
JSON. Keep it minimal enough that its intended finding is clear without
searching through unrelated schema.

