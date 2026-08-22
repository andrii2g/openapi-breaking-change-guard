# Compatibility rules

## Default profile

The default `client` profile asks whether an old client can continue calling a
new server. It follows a practical wire-compatibility model:

- Tightening what the server accepts in requests is breaking.
- Removing what the server used to return is breaking.
- Adding optional request capabilities is non-breaking.
- Adding response data is normally non-breaking, but widening response enums or
  unions is informational because exhaustive clients may need attention.

A `strict` profile promotes response enum/union widening from `Info` to
`Breaking`. It does not otherwise change MVP rules.

## Rule matrix

| ID | Name | Default severity | Trigger |
|---|---|---|---|
| OBCG001 | Path removed | Breaking | An old path key is absent from the new document. |
| OBCG002 | Operation removed | Breaking | An old HTTP method is absent under a common path. |
| OBCG003 | Path added | NonBreaking | A new path key was added. |
| OBCG004 | Operation added | NonBreaking | A new HTTP method was added under a common path. |
| OBCG005 | Required parameter added | Breaking | A new effective parameter has `required: true`. |
| OBCG006 | Optional parameter added | NonBreaking | A new effective parameter is not required. |
| OBCG007 | Parameter became required | Breaking | A common parameter changed from optional to required. |
| OBCG008 | Parameter removed | Info | An old effective parameter is absent; old clients can still omit/continue sending based on server tolerance uncertainty. |
| OBCG009 | Response code removed | Breaking | An old response key is absent under a common operation. |
| OBCG010 | Response code added | NonBreaking | A response key was added. |
| OBCG011 | Property removed | Breaking | A property allowed/returned by an old object is absent from the new object. |
| OBCG012 | Optional property added | NonBreaking | A property was added and is not required in the relevant new object. |
| OBCG013 | Required request property added | Breaking | A new request property is in the new required set. |
| OBCG014 | Required response property added | NonBreaking | A new response property is guaranteed by the new server. |
| OBCG015 | Existing property became required | Directional | Breaking in request; NonBreaking in response. |
| OBCG016 | Enum narrowed | Directional | Breaking in request; NonBreaking in response. |
| OBCG017 | Enum widened | Directional | NonBreaking in request; Info/Breaking in response by profile. |
| OBCG018 | Primitive type changed | Breaking | Canonical primitive type sets differ without subset compatibility. |
| OBCG019 | Schema alternative removed | Directional | Breaking in request; NonBreaking in response. |
| OBCG020 | Schema alternative added | Directional | NonBreaking in request; Info/Breaking in response by profile. |
| OBCG021 | Deprecation added | Info | A common path, operation, parameter, or schema changed to `deprecated: true`. |
| OBCG022 | Media type removed | Breaking | Old request or response content media type is missing. |
| OBCG023 | Media type added | NonBreaking | New content media type was added. |


## Directional schema table

| Change | Request position | Response position |
|---|---|---|
| Optional property added | NonBreaking | NonBreaking |
| Required property added | Breaking | NonBreaking |
| Existing property becomes required | Breaking | NonBreaking |
| Property removed | Breaking | Breaking |
| Enum value removed | Breaking | NonBreaking |
| Enum value added | NonBreaking | Info (`Breaking` in strict profile) |
| Union alternative removed | Breaking | NonBreaking |
| Union alternative added | NonBreaking | Info (`Breaking` in strict profile) |
| Primitive type replaced | Breaking | Breaking |
| `null` removed | Breaking | NonBreaking |
| `null` added | NonBreaking | Info (`Breaking` in strict profile) |

## Rule details

### Removed paths and operations

Use exact ordinal path and lowercase method identity. Do not attempt to match
`/users/{id}` with `/users/{userId}`. That may be a rename or a semantically
different route.

When an entire path is removed, emit only OBCG001. For a common path, compare
methods and emit OBCG002 per removed method.

### Parameters

Compare effective parameters after path-level inheritance and operation-level
override. Identity is `(in, name)`.

Path parameters are required by OpenAPI. If `in: path` omits or contradicts
`required: true`, emit a validation diagnostic. Normalize it as required only
if the document remains unambiguous; otherwise mark analysis incomplete.

Compare schemas of common parameters in request position using schema rules.

OBCG008 is `Info` because OpenAPI alone cannot tell whether a new server rejects
unknown query/header fields. A future strict-request profile may promote it.

### Responses

Compare response keys as strings. A removed `404`, `default`, or `2XX` is
breaking because generated clients and error handling may depend on the
declared shape.

For common response keys, compare each common media type in response position.
Report media type removal before comparing schemas.

### Properties and required sets

Property existence and requiredness are separate facts.

For a property in old but not new, report OBCG011 and do not traverse its child
schema. For a property in new but not old, choose OBCG012/013/014 from usage and
new requiredness. For common properties, compare required membership with
OBCG015, then compare child schemas.

An entry in `required` that has no corresponding `properties` entry is legal in
some JSON Schema contexts but ambiguous for this MVP. Emit a warning and retain
the name in the required set; do not invent a property schema.

### Enums

Canonicalize each enum value by JSON type plus invariant value:

```text
null
boolean:true
number:1
string:"1"
```

Numbers must use a canonical JSON numeric representation so `1` and `1.0` are
treated according to the selected numeric canonicalization documented in
`schema-normalization.md`.

Given old set `O` and new set `N`:

- `N` strict subset of `O`: narrowed.
- `O` strict subset of `N`: widened.
- Neither is a subset: emit one primitive/enum replacement breaking finding,
  not both narrowed and widened.

### Primitive types

Normalize `nullable: true` and unioned `null` into type/alternative sets. Exact
same sets are compatible. Subset changes use direction rules. Disjoint or
partially overlapping non-subset replacements are breaking.

Do not assume `integer` is substitutable for `number` in the MVP. A future rule
may safely model that relation after dedicated tests.

### `anyOf` and `oneOf`

Compare alternatives by stable canonical keys as sets after duplicate removal.
Reordering is irrelevant. Preserve the composition kind: changing `oneOf` to
`anyOf` is an unsupported semantic change in the MVP and produces an incomplete
diagnostic rather than a guessed compatibility result.

Alternative removal narrows possible values; alternative addition widens them.
Apply request/response directions from the table.

### `allOf`

`allOf` is intersection, not union. The normalizer merges supported object
fragments before ordinary property comparison. Equal primitive constraints and
compatible additional-properties policies merge directly. A stable intersection
that cannot be represented by the supported algebra is retained as a bounded,
deterministic opaque shape; this allows identical documents and unrelated API
changes to remain analyzable. If that opaque intersection changes between the
old and new contracts, emit an incomplete diagnostic instead of guessing its
compatibility. Never compare `allOf` branches as independent alternative sets.

## Finding wording

Summaries should name the API concept and concrete identity:

```text
Operation removed: POST /v1/widgets
Required query parameter added: limit
Response property removed: display_name
Allowed schema alternative removed: primitive:string
```

Avoid generic text such as `node changed` or dumping entire JSON objects.

## Cascade suppression

Suppress a descendant when an ancestor finding already explains its
unavailability. Do not suppress independent siblings. A removed `200` response
suppresses its media type/property removals but does not suppress removal of
`404`.

