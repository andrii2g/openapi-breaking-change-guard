# ADR 0001: JSON-only input for the MVP

Status: Accepted

## Context

OpenAPI permits JSON and YAML. The target OpenAI repository publishes both.
Supporting YAML would add a parser dependency, YAML-specific anchors/tags, and
additional input ambiguity without improving the structural comparison lesson.

## Decision

Accept local JSON only and parse with `System.Text.Json`. Use the upstream
OpenAI `openapi.json` for the scheduled demonstration.

## Consequences

- The dependency footprint stays small.
- JSON Pointer and raw JSON value handling remain direct.
- YAML users must convert before invoking version 0.1.
- A future YAML loader can feed the same index/domain model.

