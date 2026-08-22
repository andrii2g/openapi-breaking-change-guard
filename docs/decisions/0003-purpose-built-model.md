# ADR 0003: Purpose-built OpenAPI index and schema algebra

Status: Accepted

## Context

Full OpenAPI libraries provide broad parsing and resolution behavior, but they
may normalize away source distinctions, introduce remote-reference behavior,
or expose a much larger object model than the MVP needs. The educational goal
includes schema traversal and structural diff design.

## Decision

Build a small index and normalized schema algebra using `System.Text.Json`.
Support only the documented surface and make unsupported semantic changes
visible.

## Consequences

- Rule behavior remains explicit and testable.
- The project must implement local JSON Pointer resolution and cycle handling.
- Feature boundaries require discipline.
- The tool is not advertised as a universal OpenAPI compliance checker.

