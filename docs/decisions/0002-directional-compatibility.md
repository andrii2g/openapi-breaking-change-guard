# ADR 0002: Direction-aware compatibility

Status: Accepted

## Context

The same set change has different effects in requests and responses. Removing
an accepted request enum value breaks old callers; removing a possible response
enum value usually reduces what clients must handle.

## Decision

Every schema entry point and recursive comparison carries `SchemaUsage.Request`
or `SchemaUsage.Response`. Rules select severity from that usage and the active
compatibility profile.

## Consequences

- Findings represent actual client/server direction rather than textual change.
- Shared component schemas may be analyzed more than once when used in both
  directions.
- Memoization keys include usage.
- Tests must cover both directions for set-based rules.

