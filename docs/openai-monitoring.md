# Monitoring the OpenAI API specification

## Purpose

The scheduled workflow is a practical demonstration and a useful daily guard.
It compares a reviewed OpenAI API contract against the latest published JSON.

Upstream repository:

```text
https://github.com/openai/openai-openapi
```

Specification URL:

```text
https://raw.githubusercontent.com/openai/openai-openapi/main/openapi.json
```

## Baseline files

```text
baselines/openai.json
baselines/openai-baseline.lock.json
```

The accepted snapshot currently points to upstream commit
[`f9400172ebe08522ab228b771d885e3bd5456e42`](https://github.com/openai/openai-openapi/commit/f9400172ebe08522ab228b771d885e3bd5456e42),
committed on 2026-07-22. Its SHA-256 is:

```text
37026c9d6e0154d9a86c6df27b4fd97cc6f63c35b4f3cca53c6d9c43c989b2f1
```

Treat `openai-baseline.lock.json` as the machine-readable source of truth for
these values.

The lock file records provenance:

```json
{
  "repository": "https://github.com/openai/openai-openapi",
  "ref": "full upstream commit SHA",
  "source": "https://raw.githubusercontent.com/openai/openai-openapi/<SHA>/openapi.json",
  "sha256": "lowercase SHA-256 of openai.json",
  "acceptedAtUtc": "ISO-8601 timestamp of the human acceptance act",
  "acceptedBy": "repository owner or maintainer"
}
```

The analysis report remains timestamp-free; the baseline acceptance record may
contain time because it is provenance, not generated diff output.

## Baseline update procedure

1. Review the upstream commit and official API changelog/deprecations.
2. Download `openapi.json` pinned to the full commit SHA, not moving `main`.
3. Calculate SHA-256.
4. Store the file and lock metadata.
5. Run the guard against the file itself; expect zero changes and exit `0`.
6. Commit both baseline files in the same reviewed commit.

Routine scheduled runs must not perform this procedure automatically.

## Scheduled run

The workflow:

1. Checks out the default branch.
2. Builds the tool in Release mode.
3. Resolves the exact commit currently referenced by upstream `main`.
4. Downloads `openapi.json` from that pinned commit with retries.
5. Verifies both documents are non-empty JSON.
6. Passes the baseline and current commit SHAs and exact GitHub file URLs into
   the report.
7. Compares current against the accepted baseline.
8. Captures the guard exit code without stopping report publication.
9. Adds Markdown to the workflow job summary.
10. Uploads the report, current specification, resolved commit, and download metadata.
11. Completes with a warning when breaking changes are found, and fails only for
    invalid input, incomplete analysis, or an unexpected guard result.

The minute is intentionally not `0` because scheduled GitHub workloads may be
delayed at the start of an hour.

## Acceptance workflow

A failed scheduled run is a review signal, not an instruction to accept the new
schema.

After reviewing findings:

- Update affected production clients first when necessary.
- Review the OpenAI API changelog and deprecation schedule.
- Record any known limitation or false positive as a focused issue/fixture.
- Only then replace the baseline and lock in a human-reviewed commit.

Never let the scheduled workflow push, open an automatic baseline PR, or mutate
the accepted file in the MVP.

## Notifications

GitHub's normal failed-workflow notification is enough for the MVP. Automatic
issue creation is deferred because daily duplicate issues require fingerprinting,
permissions, closure policy, and abuse/noise controls.

## Result interpretation

| Outcome | Workflow result | Meaning | Action |
|---|---|---|---|
| Exit 0 | Success | No detected breaking change in supported scope. | Review Info changes as convenient. |
| Exit 1 | Success with warning | Breaking change detected. | Inspect the report and affected client logic. |
| Exit 2 | Failure | Download/input/report failure. | Fix the workflow or upstream availability issue. |
| Exit 3 | Failure | Analysis incomplete. | Add support or investigate the construct before accepting. |

Breaking findings are the monitor's expected product, so they remain prominent
in the job summary, uploaded report, and a warning annotation without turning the
workflow run red. Operational failures and unreliable analysis still fail the
workflow.

## Deprecations are separate

Schema comparison sees removals when the contract changes. Deprecation notices
often appear much earlier. Monitoring should therefore be paired with periodic
human review of:

```text
https://developers.openai.com/api/docs/changelog
https://developers.openai.com/api/docs/deprecations
```

The MVP intentionally does not scrape these pages.

