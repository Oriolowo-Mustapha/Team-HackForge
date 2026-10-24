# Release verification

This file replaces an audit of the earlier API-only implementation. The historical limitations in that audit no longer describe the current source.

Current controls and release requirements are documented in SECURITY.md and DEPLOYMENT.md. The API contract is described in API.md and generated OpenAPI. Automated checks live in .github/workflows/verify.yml.

A release must pass backend unit/integration tests, frontend type and lint checks, streaming-body tests, production builds, dependency audits, full-stack browser workflows, and container builds. Local database/browser verification does not replace live provider, domain, TLS, email-delivery, backup/restore, or operational checks in the target environment.