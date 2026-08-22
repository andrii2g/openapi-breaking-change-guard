# OpenAPI compatibility report

> **Verdict: Breaking changes detected.**

| Input | File | OpenAPI |
|---|---|---|
| Old | `examples/old.json` | `3.1.0` |
| New | `examples/new.json` | `3.1.0` |

| Severity | Count |
|---|---:|
| Breaking | 6 |
| NonBreaking | 4 |
| Info | 0 |

## Breaking changes

### OBCG001 — Path removed

- Location: `paths./v1/legacy`
- Change: Path removed: `/v1/legacy`.

### OBCG007 — Parameter became required

- Location: `paths./v1/widgets.get.parameters.query.limit`
- Change: Query parameter `limit` became required.

### OBCG011 — Property removed

- Location: `paths./v1/widgets.get.responses.200.content.application/json.schema.items.properties.display_name`
- Change: Response property `display_name` was removed.

### OBCG018 — Primitive type changed

- Location: `paths./v1/widgets.get.responses.200.content.application/json.schema.items.properties.id`
- Change: Primitive type changed from `string` to `integer`.

### OBCG009 — Response code removed

- Location: `paths./v1/widgets.get.responses.404`
- Change: Response `404` was removed from `GET /v1/widgets`.

### OBCG002 — Operation removed

- Location: `paths./v1/widgets.post`
- Change: Operation removed: `POST /v1/widgets`.

## Non-breaking changes

### OBCG003 — Path added

- Location: `paths./v1/health`
- Change: Path added: `/v1/health`.

### OBCG019 — Schema alternative removed

- Location: `paths./v1/widgets.get.responses.200.content.application/json.schema.items.properties.error`
- Change: Response schema alternative `primitive:string` is no longer returned.

### OBCG016 — Enum narrowed

- Location: `paths./v1/widgets.get.responses.200.content.application/json.schema.items.properties.status`
- Change: Response enum value `inactive` is no longer returned.

### OBCG014 — Required response property added

- Location: `paths./v1/widgets.get.responses.200.content.application/json.schema.items.properties.tag`
- Change: Required response property `tag` was added.

## Informational changes

No informational changes.

## Diagnostics

No diagnostics.

## Scope

This report covers the documented JSON-only OpenAPI 3.0/3.1 compatibility
subset. See `docs/limitations.md` for exclusions.
