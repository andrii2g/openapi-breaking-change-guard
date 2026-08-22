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

## Bootstrap procedure

1. Review the upstream commit and official API changelog/deprecations.
2. Download `openapi.json` pinned to the full commit SHA, not moving `main`.
3. Calculate SHA-256.
4. Store the file and lock metadata.
5. Run the guard against the file itself; expect zero changes and exit `0`.
6. Commit both baseline files in the same reviewed commit.

## Scheduled run

The workflow:

1. Checks out the default branch.
2. Builds the tool in Release mode.
3. Downloads current `main/openapi.json` with retries.
4. Verifies both documents are non-empty JSON.
5. Compares current against the accepted baseline.
6. Captures the guard exit code without stopping report publication.
7. Adds Markdown to the workflow job summary.
8. Uploads the report, current specification, and download metadata.
9. Fails at the end when the guard result is non-zero.

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

## Failure interpretation

| Outcome | Meaning | Action |
|---|---|---|
| Exit 0 | No detected breaking change in supported scope. | Review Info changes as convenient. |
| Exit 1 | Breaking change detected. | Inspect report and affected client logic. |
| Exit 2 | Download/input/report failure. | Fix workflow or upstream availability issue. |
| Exit 3 | Analysis incomplete. | Add support or investigate construct before accepting. |

## Deprecations are separate

Schema comparison sees removals when the contract changes. Deprecation notices
often appear much earlier. Monitoring should therefore be paired with periodic
human review of:

```text
https://developers.openai.com/api/docs/changelog
https://developers.openai.com/api/docs/deprecations
```

The MVP intentionally does not scrape these pages.

