# Accepted OpenAI baseline

This directory intentionally does not contain an arbitrary moving snapshot in
the implementation package.

Before enabling the scheduled monitor, add both reviewed files:

```text
baselines/openai.json
baselines/openai-baseline.lock.json
```

Follow `docs/openai-monitoring.md`. Pin the download to a full upstream commit
SHA, calculate SHA-256, review the official changelog/deprecations, and commit
the specification and lock file together.

The scheduled workflow checks for the baseline and fails clearly if it has not
been bootstrapped. It never updates the baseline.

