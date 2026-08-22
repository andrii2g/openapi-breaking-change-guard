# Supported feature boundary

## Supported in 0.1

| Area | Support |
|---|---|
| Input | Local UTF-8 JSON files. |
| Versions | OpenAPI 3.0.x and 3.1.x envelopes. |
| References | Local `#/...` JSON Pointer references. |
| Operations | Standard HTTP methods under `paths`. |
| Parameters | Path and operation parameters; local refs; inheritance/override. |
| Bodies | Request/response `content` schemas by exact media type. |
| Schemas | Type, properties, required, enum, items, nullable, local refs. |
| Composition | Bounded `allOf`, `anyOf`, and `oneOf`. |
| Reports | Console and Markdown. |

## Intentionally unsupported

- YAML.
- Swagger/OpenAPI 2.0.
- Remote/file references.
- Callbacks and webhooks.
- Links, examples, encoding, and external documentation.
- Security scheme and security requirement compatibility.
- Servers and server variables.
- Header/cookie case-normalization policy beyond exact parameter identity.
- Discriminator overlap/mapping semantics.
- XML serialization metadata.
- Full regular-expression, numeric, or length constraint compatibility.
- Arbitrary JSON Schema equivalence.

## Honest behavior

The guard distinguishes:

1. **Ignored non-semantic metadata**: title, description, examples, extensions.
2. **Opaque unchanged constraint**: unsupported keyword exists identically in
   both schemas; safe to carry without deciding it.
3. **Changed unsupported constraint**: could affect compatibility; diagnostic
   error and incomplete analysis.
4. **Unsupported reachable construct**: diagnostic error and incomplete
   analysis.

It must never silently return success after category 3 or 4.

## Known policy choices

- Adding response properties is non-breaking by default.
- Widening response enum/union values is Info by default and Breaking in strict
  mode.
- Removing parameters is Info because rejection of unknown inputs is not
  represented reliably by OpenAPI.
- Integer and number are distinct types in 0.1.
- Media types are exact strings.
- Path-template variable renames are not inferred.

These are explicit compatibility policies, not universal truths. Future
profiles may provide alternatives without changing historical rule IDs.

