---
name: architecture-improvement-review
description: Analyze the sms-api repository architecture and produce evidence-backed, prioritized improvement suggestions. Use when asked to review, assess, improve, or rethink the project architecture; do not modify code unless the user separately asks for implementation.
---

# Architecture Improvement Review

Perform a read-only architecture review of the repository and recommend only improvements that are justified by the current code, requirements, and operational model.

The goal is not to maximize abstraction or introduce patterns for their own sake. Prefer simple boundaries, explicit dependencies, maintainable code, secure tenant isolation, and changes that reduce real architectural risk or complexity.

## Scope and authority

- Read applicable `AGENTS.md` files and current project instructions before judging the architecture.
- Inspect the root `README.md`, solution/project files, dependency injection, API startup, controllers, middleware, application services, domain model, infrastructure, persistence, SQL, providers, background services, messaging, caching, observability, Docker Compose, CI, tests, and Angular UI where relevant.
- Treat explicit project requirements as architectural constraints, including multi-tenancy, provider isolation, security, UTC/time-zone handling, observability, test coverage, and simple maintainable code.
- A review request is read-only. Do not edit files, run builds/tests/containers, create commits, branches, pull requests, or merge changes unless the user explicitly asks for implementation.
- Never expose secrets, credentials, tokens, encryption keys, SMS bodies, phone numbers, or customer data.
- Do not recommend a rewrite when a smaller structural change can solve the problem.
- Do not recommend microservices, CQRS, event sourcing, additional brokers, additional databases, or new frameworks unless repository evidence shows a concrete problem that those choices solve.

## Review method

1. Establish the effective requirements and repository constraints.
2. Map the current architecture before proposing changes:
   - projects and modules;
   - dependency direction;
   - DI lifetimes;
   - API boundaries;
   - application/domain/infrastructure responsibilities;
   - persistence and database ownership;
   - providers and external integrations;
   - background workers and message flows;
   - cache and invalidation;
   - observability and operational dependencies;
   - frontend/backend boundaries.
3. Trace the most important end-to-end flows:
   - authentication and tenant resolution;
   - send SMS;
   - scheduled and bulk send when present;
   - inbound SMS and provider callbacks;
   - delivery-status updates;
   - provider configuration and selection;
   - reporting and history;
   - user and system logging;
   - alerts and background processing.
4. Identify architectural problems from evidence, not preference.
5. Distinguish:
   - **Defect** — architecture can cause incorrect, insecure, or unreliable behavior.
   - **Structural risk** — design creates avoidable coupling, unclear ownership, fragile lifetimes, or operational risk.
   - **Maintainability issue** — code organization makes safe change harder than necessary.
   - **Opportunity** — improvement is useful but not currently urgent.
6. For each finding, propose the smallest architectural change that addresses the root cause.
7. Group proposed changes into cohesive pull requests that can be implemented independently.

## Architecture checklist

### Boundaries and dependency direction

Check for:

- controllers or middleware containing business rules that belong in application services;
- infrastructure concerns leaking into domain or application code;
- application code depending directly on provider-specific types;
- circular or unclear project dependencies;
- oversized classes that coordinate unrelated responsibilities;
- duplicated orchestration logic across endpoints or background consumers;
- interfaces that exist only as unnecessary indirection;
- abstractions that hide simple behavior instead of clarifying boundaries.

Prefer clear dependency direction and explicit composition in dependency injection.

### Dependency injection and lifetimes

Check for:

- singleton services capturing scoped dependencies;
- service-locator usage;
- runtime feature flags scattered across consumers instead of resolved at composition boundaries where practical;
- duplicated registration logic;
- constructors with excessive unrelated dependencies;
- provider-specific registration leaking outside the provider boundary;
- service lifetime choices that can retain tenant, request, cache, or user state incorrectly.

### Multi-tenancy

Treat tenant isolation as a first-class architecture requirement.

Check:

- how tenant identity enters the system;
- authorization before tenant-scoped operations;
- tenant filters in persistence and reporting;
- tenant-safe cache keys;
- tenant-safe background messages;
- tenant context crossing async/background boundaries;
- provider configuration ownership;
- logs, reports, alerts, and administration boundaries;
- platform-user versus tenant-user separation.

Do not consider the design safe merely because entities contain a tenant ID.

### Provider architecture

Check that Twilio, Bandwidth, Mock, and future providers:

- implement the same generic application contract;
- isolate provider-specific requests, authentication, settings, callbacks, and parsing;
- do not leak provider DTOs into core business logic;
- have clear configuration validation;
- have consistent transient/permanent failure semantics;
- can be added without changing unrelated application code.

Prefer provider-specific adapters behind small stable contracts.

### Persistence and database architecture

Inspect:

- repository responsibility and query ownership;
- transaction boundaries;
- whether application workflows require inconsistent multi-step writes;
- Dapper mapping complexity;
- duplicated SQL behavior;
- reporting tables versus transactional tables;
- idempotency and concurrency boundaries;
- UTC storage and tenant time-zone conversion boundaries;
- separate operational/observability database responsibilities;
- schema initialization and bootstrap ownership.

Do not recommend an ORM or repository rewrite without a concrete maintainability or correctness problem.

### Messaging and background processing

Inspect RabbitMQ, hosted services, scheduling, retry, alerts, and outbox-like flows for:

- clear ownership of publish versus consume responsibilities;
- idempotent consumers;
- retry semantics and poison-message behavior;
- duplicate delivery handling;
- ordering assumptions;
- unbounded concurrency;
- coupling between database transactions and message publication;
- polling intervals and unnecessary background activity;
- tenant identity included explicitly in messages;
- failure recovery after process restarts.

### Cache architecture

Check:

- whether cache is an optimization rather than a source of truth;
- feature-flag handling at composition boundaries;
- cache key tenant isolation;
- expiration and invalidation ownership;
- duplicate cache policy logic;
- behavior when Redis is unavailable or disabled;
- whether health checks match the configured cache mode.

Avoid adding cache where no repeated expensive read is evident.

### Security architecture

Inspect:

- authentication and authorization boundaries;
- JWT and refresh-token lifecycle;
- tenant ownership validation;
- secret and SMS-content encryption;
- webhook authentication;
- rate limiting;
- audit logging;
- sensitive-data handling in logs and telemetry;
- credentials in configuration and bootstrap;
- platform versus tenant administrative capabilities.

Architecture suggestions must never weaken security for convenience.

### Observability and operations

Check:

- separation between system logs, user activity logs, traces, and metrics;
- tenant isolation for customer-visible logs;
- OpenTelemetry ownership and exporter configuration;
- health checks matching optional dependencies;
- useful correlation identifiers;
- excessive coupling between application startup and observability systems;
- failure behavior when observability dependencies are unavailable;
- Docker Compose bootstrap responsibility;
- operational dependencies that are started even when a feature is disabled.

### API and frontend architecture

Check:

- API contract consistency;
- pagination and filtering boundaries;
- UTC versus client time-zone responsibility;
- duplication of business rules between Angular and API;
- frontend service organization;
- platform versus tenant administration separation;
- whether UI behavior depends on provider-specific implementation details.

### Testing and evolvability

Check whether architecture-critical behavior is protected by focused tests:

- tenant isolation;
- provider resolution;
- DI composition and service lifetimes;
- cache enabled/disabled composition;
- retries and idempotency;
- webhook validation;
- authorization;
- background consumer behavior;
- report isolation;
- UTC/time-zone conversion.

Do not recommend tests solely for coverage percentage; prioritize architectural contracts and failure modes.

## Evidence and prioritization

Every finding must include repository evidence.

Classify severity:

- **Critical** — likely tenant data exposure, credential exposure, systemic data corruption, or architecture that can bypass a required security boundary.
- **High** — reliability, isolation, dependency-lifetime, transaction, or coupling problem that can materially affect production behavior.
- **Medium** — maintainability or structural problem that increases change risk or operational complexity.
- **Low** — localized architecture cleanup with limited impact.
- **Opportunity** — reasonable future improvement without a demonstrated current problem.

Also report:

- **Confidence**: High, Medium, or Low.
- **Effort**: Small, Medium, or Large.
- **Change risk**: Low, Medium, or High.

Do not create numerical architecture scores. Avoid pretending that subjective architecture preferences are objective defects.

## Required output

Lead with a concise overall assessment and the most important architectural risks.

Then provide:

1. **Scope reviewed** — modules, flows, and infrastructure inspected.
2. **Current architecture** — concise component/dependency map. Use Mermaid only when it materially improves understanding.
3. **Findings** — ordered by severity. For each finding include:
   - category;
   - severity;
   - evidence with repository-relative file paths;
   - current design;
   - why it matters;
   - smallest recommended improvement;
   - confidence;
   - effort;
   - change risk.
4. **Good architecture already present** — important boundaries or design choices that should be preserved.
5. **Prioritized improvement plan** — recommended order of implementation.
6. **Pull-request plan** — cohesive changes, one architectural concern per PR.
7. **What not to change** — areas where added abstraction or redesign would not currently be justified.
8. **Validation and uncertainty** — what was inspected, what was not verified, and which findings require runtime evidence.

If no meaningful architecture problem is found, say so directly and list only justified opportunities. Do not invent findings to make the report look complete.

## Implementation requests

If the user asks to implement one or more findings:

- keep each change cohesive and minimal;
- preserve unrelated behavior;
- prefer existing project conventions;
- update tests for the architectural contract being changed;
- update the README when setup, responsibilities, dependencies, or operational behavior changes;
- do not combine unrelated architecture improvements in the same pull request;
- do not weaken the 80% coverage requirement;
- follow repository restrictions for builds, tests, commits, CI, pull requests, and merges.
