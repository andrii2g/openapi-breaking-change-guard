#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: provider-status.sh [options]

Generate provider status documents and optionally publish a narrowly scoped
GitHub commit status.

Required options:
  --provider-id ID
  --provider-label LABEL
  --repository OWNER/REPOSITORY
  --old-revision SHA
  --new-revision SHA
  --guard-exit-code CODE
  --run-url URL

Optional options:
  --old-revision-url URL
  --new-revision-url URL
  --report FILE
  --output-directory DIRECTORY  (default: artifacts/provider-status/ID)
  --status-sha SHA              Repository commit that receives the status.
  --status-context CONTEXT      (default: api-compatibility/ID)
  --publish                     Publish commit status; requires GH_TOKEN.
EOF
}

provider_id=""
provider_label=""
repository=""
old_revision=""
new_revision=""
old_revision_url=""
new_revision_url=""
guard_exit_code=""
run_url=""
report_path=""
output_directory=""
status_sha=""
status_context=""
publish=false

while (($# > 0)); do
  case "$1" in
    --provider-id) provider_id="${2:-}"; shift 2 ;;
    --provider-label) provider_label="${2:-}"; shift 2 ;;
    --repository) repository="${2:-}"; shift 2 ;;
    --old-revision) old_revision="${2:-}"; shift 2 ;;
    --new-revision) new_revision="${2:-}"; shift 2 ;;
    --old-revision-url) old_revision_url="${2:-}"; shift 2 ;;
    --new-revision-url) new_revision_url="${2:-}"; shift 2 ;;
    --guard-exit-code) guard_exit_code="${2:-}"; shift 2 ;;
    --run-url) run_url="${2:-}"; shift 2 ;;
    --report) report_path="${2:-}"; shift 2 ;;
    --output-directory) output_directory="${2:-}"; shift 2 ;;
    --status-sha) status_sha="${2:-}"; shift 2 ;;
    --status-context) status_context="${2:-}"; shift 2 ;;
    --publish) publish=true; shift ;;
    --help|-h) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ ! "$provider_id" =~ ^[a-z0-9][a-z0-9-]*$ ]]; then
  echo "Provider ID must contain lowercase letters, numbers, or hyphens." >&2
  exit 2
fi

if [[ -z "$provider_label" || ! "$repository" =~ ^[^/]+/[^/]+$ ]]; then
  echo "Provider label and OWNER/REPOSITORY are required." >&2
  exit 2
fi

if [[ ! "$guard_exit_code" =~ ^[0-3]$ ]]; then
  echo "Guard exit code must be 0, 1, 2, or 3." >&2
  exit 2
fi

if [[ ! "$run_url" =~ ^https:// ]]; then
  echo "Run URL must be an absolute HTTPS URL." >&2
  exit 2
fi

if [[ "$guard_exit_code" =~ ^[01]$ ]]; then
  if [[ ! "$old_revision" =~ ^[0-9a-f]{40}$ || ! "$new_revision" =~ ^[0-9a-f]{40}$ ]]; then
    echo "Completed analyses require full lowercase baseline and candidate commit SHAs." >&2
    exit 2
  fi
fi

output_directory="${output_directory:-artifacts/provider-status/$provider_id}"
status_context="${status_context:-api-compatibility/$provider_id}"
mkdir -p "$output_directory"

short_revision() {
  local revision="$1"
  if [[ "$revision" =~ ^[0-9a-f]{40}$ ]]; then
    printf '%s' "${revision:0:7}"
  else
    printf '%s' "unknown"
  fi
}

read_count() {
  local label="$1"
  local file="$2"
  awk -F '|' -v wanted="$label" '
    {
      name = $2
      value = $3
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", name)
      gsub(/^[[:space:]]+|[[:space:]]+$/, "", value)
      if (name == wanted && value ~ /^[0-9]+$/) {
        print value
        exit
      }
    }
  ' "$file"
}

breaking=0
non_breaking=0
info=0
methods=0

if [[ "$guard_exit_code" =~ ^[01]$ ]]; then
  if [[ -z "$report_path" || ! -s "$report_path" ]]; then
    echo "Completed analyses require a non-empty Markdown report." >&2
    exit 2
  fi

  breaking="$(read_count "Breaking" "$report_path")"
  non_breaking="$(read_count "NonBreaking" "$report_path")"
  info="$(read_count "Info" "$report_path")"
  methods="$(sed -nE 's/^([0-9]+) API methods? (has|have) breaking findings.*$/\1/p' "$report_path" | head -n 1)"

  breaking="${breaking:-0}"
  non_breaking="${non_breaking:-0}"
  info="${info:-0}"
  methods="${methods:-0}"

  if [[ "$guard_exit_code" == "0" && "$breaking" != "0" ]]; then
    echo "Exit code 0 conflicts with the report's breaking-change count." >&2
    exit 2
  fi

  if [[ "$guard_exit_code" == "1" && "$breaking" == "0" ]]; then
    echo "Exit code 1 requires at least one breaking finding in the report." >&2
    exit 2
  fi
fi

old_short="$(short_revision "$old_revision")"
new_short="$(short_revision "$new_revision")"
comparison="$old_short → $new_short"

case "$guard_exit_code" in
  0)
    status="compatible"
    message="$comparison | compatible"
    color="brightgreen"
    ;;
  1)
    status="review-required"
    message="$comparison | review: $breaking breaking · $methods methods"
    color="red"
    ;;
  2)
    status="monitor-error"
    message="$comparison | monitor error"
    color="orange"
    ;;
  3)
    status="analysis-incomplete"
    message="$comparison | analysis incomplete"
    color="orange"
    ;;
esac

jq -n \
  --arg label "$provider_label" \
  --arg message "$message" \
  --arg color "$color" \
  '{schemaVersion: 1, label: $label, message: $message, color: $color}' \
  > "$output_directory/badge.json"

jq -n \
  --arg provider_id "$provider_id" \
  --arg provider_label "$provider_label" \
  --arg accepted_revision "$old_revision" \
  --arg accepted_url "$old_revision_url" \
  --arg checked_revision "$new_revision" \
  --arg checked_url "$new_revision_url" \
  --arg status "$status" \
  --argjson guard_exit_code "$guard_exit_code" \
  --argjson breaking "$breaking" \
  --argjson non_breaking "$non_breaking" \
  --argjson info "$info" \
  --argjson methods "$methods" \
  --arg run_url "$run_url" \
  '{
    schemaVersion: 1,
    provider: {id: $provider_id, label: $provider_label},
    comparison: {
      acceptedRevision: $accepted_revision,
      acceptedUrl: $accepted_url,
      checkedRevision: $checked_revision,
      checkedUrl: $checked_url
    },
    result: {
      status: $status,
      guardExitCode: $guard_exit_code,
      breaking: $breaking,
      nonBreaking: $non_breaking,
      info: $info,
      methodsRequiringReview: $methods
    },
    runUrl: $run_url
  }' > "$output_directory/metadata.json"

if [[ -n "$report_path" && -s "$report_path" ]]; then
  cp "$report_path" "$output_directory/report.md"
else
  cat > "$output_directory/report.md" <<EOF
# $provider_label compatibility monitor

> The compatibility report was not generated. Inspect the [workflow run]($run_url).

| Item | Value |
|---|---|
| Accepted baseline | \`$old_short\` |
| Latest checked | \`$new_short\` |
| Monitor result | $status |
EOF
fi

cat > "$output_directory/README.md" <<EOF
# $provider_label compatibility status

These files are generated workflow artifacts. They do not accept or modify the
reviewed API baseline.

- [Latest compatibility report](report.md)
- [Machine-readable result](metadata.json)
- [Shields endpoint](badge.json)
- [Workflow run]($run_url)
EOF

if [[ "$publish" != true ]]; then
  echo "Generated $provider_label status in $output_directory."
  exit 0
fi

if [[ -z "${GH_TOKEN:-}" ]]; then
  echo "GH_TOKEN is required with --publish." >&2
  exit 2
fi

if [[ ! "$status_sha" =~ ^[0-9a-f]{40}$ ]]; then
  echo "A full lowercase repository commit SHA is required with --publish." >&2
  exit 2
fi

case "$guard_exit_code" in
  0) github_state="success" ;;
  1) github_state="failure" ;;
  2|3) github_state="error" ;;
esac

gh api --method POST "repos/$repository/statuses/$status_sha" \
  -f state="$github_state" \
  -f target_url="$run_url" \
  -f description="$message" \
  -f context="$status_context" >/dev/null

echo "Published $provider_label status on $repository@$status_sha."
