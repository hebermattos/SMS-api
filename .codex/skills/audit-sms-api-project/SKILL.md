---
name: audit-sms-api-project
description: Audit the sms-api repository against its project instructions and report evidence-backed compliance, gaps, risks, and prioritized next actions. Use when asked to verify, review, or check whether the SMS API project follows its requirements; do not use for implementing fixes unless the user also asks for changes.
---

# Audit SMS API Project

Perform a read-only compliance audit of the repository. Treat the user's current project instructions and repository-scoped instruction files as authoritative. Use [references/baseline-requirements.md](references/baseline-requirements.md) as the fallback checklist and comparison aid; current explicit instructions override it.

## Scope and authority

- Discover and read applicable `AGENTS.md` files, the root `README.md`, solution/project files, configuration, database initialization, Docker Compose, CI workflows, tests, API code, Angular code, and observability code.
- Follow the repository's own execution restrictions. Do not run tests, builds, containers, commits, pushes, pull requests, merges, or other mutations during an audit unless the user explicitly authorizes that action and repository instructions permit it.
- Do not edit code while answering a verification-only request. Separate findings from proposed fixes.
- Never print secret values. Report only the secret location/type and why it is unsafe.
- Ask for clarification only when conflicting instructions or a materially ambiguous requirement prevents a reliable finding.

## Audit method

1. Establish the effective requirements from the current request, applicable instruction files, and the baseline reference.
2. Map the repository before judging it. Locate backend, frontend, tests, SQL initialization, Compose, CI, and documentation.
3. Inspect implementation evidence for every applicable requirement. Prefer direct code/configuration evidence over filenames, comments, or README claims.
4. Trace security-sensitive flows end to end where practical: authentication, authorization, tenant resolution, tenant-filtered queries, provider selection, webhook validation, logging, and log access.
5. Use safe static checks such as searches for migrations, hard-coded credentials, unscoped SQL queries, sensitive logging, provider-specific leakage, and CI triggers.
6. Run permitted non-mutating validation only when needed. Never claim runtime behavior, test success, or coverage from configuration alone. If validation was not run, mark the item `Not verified` and explain why.
7. Cross-check the README and documented setup against the actual implementation.

## Evidence rules

- Cite repository-relative file paths and line numbers whenever possible.
- A requirement is `Compliant` only with sufficient direct evidence.
- Use `Partially compliant` when only part of the requirement is implemented or evidence reveals meaningful gaps.
- Use `Non-compliant` when evidence contradicts the requirement or the required implementation is absent.
- Use `Not verified` when available evidence cannot establish the result.
- Do not infer that tenant isolation is correct merely because a tenant ID exists. Confirm authorization and filtering at protected boundaries and data access.
- Do not infer webhook authenticity from endpoint authentication alone. Verify provider-specific signature validation when supported.
- Do not infer 80% coverage from a threshold setting. Require an actual current coverage result; otherwise use `Not verified`.

## Required output

Lead with the overall verdict and the highest-risk findings. Then provide:

1. A compact summary with counts by status and severity.
2. Findings ordered by severity: `Critical`, `High`, `Medium`, `Low`.
3. For each finding: requirement, status, evidence, impact, and a concrete next action.
4. A requirements matrix covering every baseline category, including compliant items so omissions are visible.
5. Validation performed and validation not performed, with reasons.
6. A prioritized remediation sequence that groups cohesive changes suitable for separate pull requests.

If no violations are found, say that no violations were identified in the inspected evidence; do not claim absolute compliance beyond the audit scope.

## Fix requests

When the user also asks to fix findings, first finish the audit and propose cohesive changes. Implement only the authorized scope, preserve unrelated work, update the README when behavior or setup changes, and verify according to repository instructions. Keep unrelated fixes in separate change sets.
