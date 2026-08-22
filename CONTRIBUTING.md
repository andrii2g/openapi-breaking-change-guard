# Contributing

## Local checks

```bash
dotnet restore OpenApiBreakingChangeGuard.slnx
dotnet build OpenApiBreakingChangeGuard.slnx --configuration Release --no-restore
dotnet test --solution OpenApiBreakingChangeGuard.slnx --configuration Release --no-build
dotnet format OpenApiBreakingChangeGuard.slnx --verify-no-changes --no-restore
```

## Adding a compatibility rule

1. Document the rule in `docs/compatibility-rules.md`.
2. Add a stable rule ID to `RuleIds`.
3. Add the rule implementation without embedding reporting behavior.
4. Add focused old/new fixtures and expected changes.
5. Add request- and response-direction tests when the rule is schema-related.
6. Update the supported-feature matrix when necessary.

Rules must be deterministic and must not depend on dictionary enumeration
order, current time, current culture, network access, or absolute file paths.

## Updating the OpenAI baseline

Do not replace `baselines/openai.json` as part of routine maintenance. Follow
[docs/openai-monitoring.md](docs/openai-monitoring.md), pin a full upstream
commit, verify its SHA-256, run a self-comparison, and commit the specification
and provenance lock together after human review.

