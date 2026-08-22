# Fixture conventions

Each directory contains `old.json`, `new.json`, and `expected.json`.

The two included OpenAI-shaped fixtures are reduced, hand-authored regressions:

- `openai-audit-source-removed` models a removed response property.
- `openai-mcp-error-union-narrowed` models removal of the primitive `string`
  alternative from an `anyOf` response field.

Add the complete fixture catalog from `docs/testing.md` while implementing rule
phases. Keep fixtures minimal enough that a reviewer can understand the intended
finding without searching through unrelated schema.

