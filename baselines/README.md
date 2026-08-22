# Accepted OpenAI baseline

This directory contains the human-reviewed OpenAI API contract used by the
scheduled compatibility monitor:

- `openai.json` is the immutable accepted specification snapshot.
- `openai-baseline.lock.json` records its upstream repository, exact commit,
  pinned source URL, SHA-256, acceptance time, and reviewer.

The current baseline is pinned to OpenAI commit
[`f9400172ebe08522ab228b771d885e3bd5456e42`](https://github.com/openai/openai-openapi/commit/f9400172ebe08522ab228b771d885e3bd5456e42)
from 2026-07-22. The expected SHA-256 is:

```text
37026c9d6e0154d9a86c6df27b4fd97cc6f63c35b4f3cca53c6d9c43c989b2f1
```

The scheduled workflow verifies this digest before comparison. It never writes
to the repository or accepts a new baseline.

Follow [the monitoring guide](../docs/openai-monitoring.md) for provenance
verification, interpreting scheduled failures, and the human-reviewed update
procedure.
