# Changelog

All notable changes will be recorded here. This project follows Semantic
Versioning after the first release.

## 22.08.2026

### Added

- JSON-only OpenAPI 3.0/3.1 loading with bounded local-reference resolution.
- Direction-aware compatibility rules OBCG001 through OBCG023.
- Deterministic console and Markdown reports with atomic file replacement.
- Explicit client and strict compatibility profiles.
- Process-level CLI coverage for exit codes 0, 1, 2, and 3.
- Packable .NET 10 tool command `openapi-guard`.
- CI and non-mutating scheduled OpenAI monitoring workflows.

- Reviewed OpenAI API baseline pinned to upstream commit
  `f9400172ebe08522ab228b771d885e3bd5456e42`, with SHA-256 provenance lock.
- Full-baseline regression against the current OpenAI specification.
- Optional old/new source revisions and direct HTTPS source links in reports.
- API-method review summary grouped from breaking findings.
- Commit-pinned current downloads and revision provenance in the OpenAI monitor.

### Fixed

- Deterministic handling of otherwise unrepresentable `allOf` intersections.
- OpenAPI 3.1 `$ref` schemas whose only semantic sibling is `nullable: true`.

