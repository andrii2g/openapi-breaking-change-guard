#!/usr/bin/env bash

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_directory="$(mktemp -d)"
trap 'rm -rf "$test_directory"' EXIT

old_revision="f9400172ebe08522ab228b771d885e3bd5456e42"
new_revision="f85dbe223d40e1a31cba812ab2d755c7e98a92a3"
run_url="https://github.com/example/guard/actions/runs/1"

cat > "$test_directory/compatible.md" <<'EOF'
| Classification | Count |
|---|---:|
| Breaking | 0 |
| NonBreaking | 3 |
| Info | 1 |
EOF

generate() {
  local exit_code="$1"
  local report="$2"
  local output="$test_directory/$exit_code"

  local report_arguments=()
  if [[ -n "$report" ]]; then
    report_arguments=(--report "$report")
  fi

  bash "$repository_root/scripts/provider-status.sh" \
    --provider-id openai \
    --provider-label "OpenAI API" \
    --repository example/guard \
    --old-revision "$old_revision" \
    --new-revision "$new_revision" \
    --guard-exit-code "$exit_code" \
    --run-url "$run_url" \
    --output-directory "$output" \
    "${report_arguments[@]}"
}

generate 0 "$test_directory/compatible.md"
generate 1 "$repository_root/examples/expected-report.md"
generate 2 ""
generate 3 ""

jq -e \
  '.message == "f940017 → f85dbe2 | compatible" and .color == "brightgreen"' \
  "$test_directory/0/badge.json" >/dev/null

jq -e \
  '.message == "f940017 → f85dbe2 | review: 6 breaking · 2 methods" and .color == "red"' \
  "$test_directory/1/badge.json" >/dev/null

jq -e \
  '.result.breaking == 6 and
   .result.nonBreaking == 4 and
   .result.methodsRequiringReview == 2 and
   .result.status == "review-required"' \
  "$test_directory/1/metadata.json" >/dev/null

jq -e \
  '.message == "f940017 → f85dbe2 | monitor error" and .color == "orange"' \
  "$test_directory/2/badge.json" >/dev/null

jq -e \
  '.message == "f940017 → f85dbe2 | analysis incomplete" and .color == "orange"' \
  "$test_directory/3/badge.json" >/dev/null

mock_directory="$test_directory/mock-bin"
status_capture="$test_directory/status-call.txt"
status_sha="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
mkdir -p "$mock_directory"
cat > "$mock_directory/gh" <<'EOF'
#!/usr/bin/env bash
printf '%s\n' "$@" > "$STATUS_CAPTURE"
EOF
chmod +x "$mock_directory/gh"

GH_TOKEN=test \
STATUS_CAPTURE="$status_capture" \
PATH="$mock_directory:$PATH" \
bash "$repository_root/scripts/provider-status.sh" \
  --provider-id openai \
  --provider-label "OpenAI API" \
  --repository example/guard \
  --old-revision "$old_revision" \
  --new-revision "$new_revision" \
  --guard-exit-code 1 \
  --run-url "$run_url" \
  --report "$repository_root/examples/expected-report.md" \
  --output-directory "$test_directory/published" \
  --status-sha "$status_sha" \
  --publish

grep -Fx "repos/example/guard/statuses/$status_sha" "$status_capture" >/dev/null
grep -Fx "state=failure" "$status_capture" >/dev/null
grep -Fx "context=api-compatibility/openai" "$status_capture" >/dev/null
if grep -F "/git/" "$status_capture" >/dev/null; then
  echo "The publisher attempted to mutate Git repository contents." >&2
  exit 1
fi


if bash "$repository_root/scripts/provider-status.sh" \
  --provider-id openai \
  --provider-label "OpenAI API" \
  --repository example/guard \
  --old-revision "$old_revision" \
  --new-revision "$new_revision" \
  --guard-exit-code 1 \
  --run-url "$run_url" \
  --report "$test_directory/compatible.md" \
  --output-directory "$test_directory/inconsistent"; then
  echo "The generator accepted an exit code that conflicted with its report." >&2
  exit 1
fi

echo "Provider status tests passed."
