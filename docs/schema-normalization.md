# Schema normalization

## Objective

Different JSON syntax can express the same supported schema. Normalization
creates a canonical structural representation so comparison ignores ordering,
formatting, reference placement, and OpenAPI 3.0 versus 3.1 null syntax.

This is not a complete JSON Schema evaluator.

## Shape algebra

Implement a closed hierarchy equivalent to:

```text
Any
Never
Primitive(type)
Enum(baseTypes, values)
Object(properties, required, additionalProperties)
Array(items)
Union(kind, alternatives)          kind = AnyOf | OneOf
RecursiveRef(canonicalPointer)
Unsupported(keywords, reason)
```

Every shape also has origin/provenance metadata that is excluded from semantic
equality.

## Canonical primitive types

Supported names:

```text
null, boolean, integer, number, string, object, array
```

Unknown type names produce an incomplete diagnostic. Do not coerce `integer`
to `number` or infer a type from `format`.

OpenAPI 3.0:

```json
{ "type": "string", "nullable": true }
```

normalizes to a union of `Primitive(string)` and `Primitive(null)`.

OpenAPI 3.1:

```json
{ "type": ["string", "null"] }
```

and:

```json
{
  "anyOf": [
    { "type": "string" },
    { "type": "null" }
  ]
}
```

normalize to equivalent allowed-type sets for the rules that only need type
membership. Preserve original union kind in provenance.

## Boolean schemas

- `true` normalizes to `Any`.
- `false` normalizes to `Never`.

Changing request `Any` to a narrower known shape is breaking. Changing a
response known shape to `Any` is potentially breaking and incomplete in the
MVP unless union rules can express the exact widening.

## Objects

Canonical object properties are stored in an ordinally sorted map. Required
names are a deduplicated ordinal set. Property order and required-array order do
not affect equality.

`additionalProperties`:

- Missing: `Unspecified`.
- `true`: `AllowedAny`.
- `false`: `Forbidden`.
- Schema: `AllowedSchema(normalizedSchema)`.

The MVP records this policy but only reports changes when implemented with
focused rule tests. Until then, a change in additional-properties policy emits
an incomplete diagnostic rather than being silently ignored.

## Arrays

An array contains a normalized `items` shape. Missing `items` becomes `Any` for
OpenAPI 3.0-compatible input. Tuple keywords such as `prefixItems` are outside
the MVP and produce an incomplete diagnostic when reached by a compared schema.

## Enums

Canonical enum values include JSON type to prevent collisions.

Recommended representation:

```text
null:null
bool:true
num:1
str:ready
```

Use `JsonElement.GetRawText()` plus a numeric canonicalizer. The canonicalizer
should parse finite JSON numbers without binary floating-point conversion:

1. Split sign, integer, fraction, and exponent text.
2. Remove leading zeros from the integer coefficient.
3. Combine fraction digits into the coefficient and adjust decimal exponent.
4. Remove trailing coefficient zeros while increasing exponent.
5. Normalize all zero spellings to `0e0`.

Thus `1`, `1.0`, and `10e-1` compare equal without precision loss.

Enum arrays are sets for compatibility. Duplicate canonical values are ignored
with a warning diagnostic.

## Local references

Normalize a `$ref` by resolving the canonical JSON Pointer and memoizing its
target. Provenance retains the reference boundary, but semantic equality uses
the target shape.

### Sibling behavior

- OpenAPI 3.0 Schema Objects commonly treat `$ref` siblings as ignored. Emit a
  warning when semantic siblings exist and normalize only the target.
- OpenAPI 3.1 permits JSON Schema-style siblings. Normalize the target and
  siblings as an intersection.
- When the only OpenAPI 3.1 semantic sibling is `nullable: true`, apply
  nullability directly to the resolved target. This preserves referenced object
  structure while adding the `null` alternative.
- Other unrepresentable but stable sibling intersections use the same opaque
  intersection policy as `allOf`.

Description, title, example, and vendor-extension siblings never affect schema
compatibility.

## `anyOf` and `oneOf`

For each alternative:

1. Normalize recursively.
2. Compute a stable canonical key excluding provenance.
3. Remove duplicate keys with a warning.
4. Sort keys ordinally.
5. Flatten nested unions only when the nested and parent kinds are identical.

Do not flatten `oneOf` into `anyOf`. `oneOf` exclusivity matters even though the
MVP does not prove arbitrary branch overlap.

Example:

```json
{
  "anyOf": [
    { "type": "null" },
    { "$ref": "#/components/schemas/ToolError" },
    { "type": "string" }
  ]
}
```

canonical alternative keys might be:

```text
primitive:null
primitive:string
object:{code=primitive:string;message=primitive:string}
```

Removing `primitive:string` is then a set subtraction, independent of branch
ordering or ref usage.

## `allOf`

Normalize every branch, then attempt a supported intersection merge.

### Object merge

- Union property names.
- Union required names.
- For a property present in one branch, retain it.
- For a property present in multiple branches with equal canonical shapes,
  retain one shape.
- For unequal shapes, recursively intersect only if a supported exact result is
  available; otherwise retain a deterministic opaque intersection at that
  property. A later change to that intersection makes analysis incomplete.
- Combine additional-properties policies only when the result is unambiguous.

### Primitive merge

- Same primitive type remains that type.
- Distinct primitive types become `Never` only when logically disjoint under
  the supported model.
- A primitive combined with incompatible object/array becomes `Never`.
- Do not invent coercion through formats.

### Opaque intersection fallback

When a valid intersection cannot be expressed by the closed shape algebra,
canonicalize the intersected shapes, order them ordinally, and retain a bounded
SHA-256 identity. Do not emit an error merely because an unchanged intersection
is opaque. Emit OBCGD005 when old and new opaque intersections differ and a
reliable compatibility decision is therefore impossible.

## Recursive schemas

Memoization states:

```text
NotStarted -> InProgress -> Complete | Failed
```

Encountering an `InProgress` target emits `RecursiveRef(pointer)` rather than
recursing. Canonical keys for recursive shapes use stable pointers, never object
identity or memory addresses.

Two recursive schemas in different documents cannot be compared solely by
pointer text. Recursive comparison tracks pairs of old/new canonical pointers;
revisiting the same pair is treated as already in progress and does not create
another finding.

## Unsupported keywords

The following are outside the MVP when their change could affect compatibility:

- `not`
- `if`, `then`, `else`
- `dependentSchemas`
- `unevaluatedProperties`
- `contains`, `minContains`, `maxContains`
- `prefixItems`
- `patternProperties`
- `propertyNames`

If an unsupported keyword exists identically in old and new canonical raw JSON,
it may be carried as opaque unchanged metadata. If added, removed, or changed at
a compared location, emit an error diagnostic and mark analysis incomplete.

Constraints such as `minimum`, `maximum`, lengths, `pattern`, and `format` follow
the same opaque-unchanged policy until their rule family is implemented.

## Canonical keys

Canonical keys must:

- Be independent of source ordering and whitespace.
- Exclude descriptions, examples, titles, and vendor extensions.
- Include semantic shape kind and content.
- Escape delimiters or use length-prefixed segments to prevent collisions.
- Be bounded; hash an internally generated canonical byte sequence with SHA-256
  if retaining full strings would exceed memory targets.

Do not expose hashes as the primary user explanation. Findings should display a
short human-readable alternative description.

