# Security policy

## Supported versions

Until the first tagged release, security fixes target the `main` branch. After
release, only the latest released version is supported.

## Reporting a vulnerability

Open a private GitHub security advisory for vulnerabilities that could cause
untrusted specifications to execute code, read arbitrary files, perform
network access, exhaust memory without bounds, or corrupt reports.

Do not attach confidential API specifications to public issues. Reduce a
problem to a minimal synthetic fixture whenever possible.

## Trust boundary

OpenAPI documents are untrusted input. The CLI must never execute extensions,
follow remote references, or interpret descriptions as instructions. Apply
document-size, reference-depth, and diagnostic-count limits before traversing
input.

