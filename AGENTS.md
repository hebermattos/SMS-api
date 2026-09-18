# AGENTS.md

This file applies to the entire repository. Follow it for every change unless a more specific `AGENTS.md` exists in a subdirectory.

## Project goal

Build a secure multi-tenant REST API for sending, receiving, tracking, and querying SMS messages. Twilio and Bandwidth are the initial providers, but provider-specific behavior must remain behind common application contracts so additional providers can be added without changing core use cases.

## Technology and structure

- Use C# and .NET 8 with ASP.NET Core controllers.
- Use SQL Server and Dapper. Do not introduce Entity Framework.
- Preserve the current dependency direction:
  - `Sms.Domain`: domain models and enums with no infrastructure dependencies.
  - `Sms.Application`: use cases and provider-independent interfaces.
  - `Sms.Infrastructure`: Dapper repositories, encryption, observability, and provider integrations.
  - `Sms.Api`: HTTP endpoints, authentication, authorization, middleware, and composition root.
- Keep controllers thin. Business rules belong in application services; external API and database details belong in infrastructure.
- Use asynchronous APIs for HTTP and database I/O and propagate `CancellationToken`.
- Keep the implementation as simple and readable as possible. Avoid unnecessary abstractions and complexity.

## SMS providers

- Every provider must implement `ISmsProvider` and be resolved through `ISmsProviderResolver`.
- Never branch on provider names in controllers or core use cases when polymorphism can handle the behavior.
- Provider credentials and configuration are tenant-specific.
- Twilio inbound and status callbacks must validate `X-Twilio-Signature` using the configured public HTTPS callback URL.
- Bandwidth authentication uses OAuth 2.0 Client Credentials.
- A tenant must not override a sender number that is not configured for that tenant.
- Webhooks must be idempotent and must resolve the owning tenant from validated provider data, never from an untrusted tenant identifier supplied by the caller.

## Multi-tenancy and authentication

- Protected endpoints use JWT bearer authentication.
- The authenticated `tenant_id` claim is the only source of tenant identity for client-facing operations.
- Do not accept a tenant ID from route, query, header, or body when the tenant can be derived from the JWT.
- Every tenant-owned query and mutation must filter by `TenantId` at the database boundary.
- Never expose records, message history, provider settings, or logs belonging to another tenant.
- API client secrets must remain PBKDF2-SHA256 hashes with unique random salts. Return a generated secret only once during provisioning.

## Security and sensitive data

- Encrypt provider secrets and sensitive provider settings in the application with AES-256-GCM before database persistence.
- Keep the Base64 master key outside SQL Server and outside the repository. Plaintext secrets may exist only in memory while required.
- Never commit credentials, tokens, production connection strings, encryption keys, or real customer data.
- Use fixed-time comparison for secrets and webhook signatures where applicable.
- Do not return provider error bodies directly to API clients.
- Do not log SMS bodies, authorization headers, credentials, access tokens, secrets, or phone numbers.
- Validate inputs, bound pagination, and avoid disclosing whether resources exist in another tenant.
- Anonymous endpoints are permitted only for authentication/bootstrap requirements or validated provider webhooks.

## Database rules

- Do not create or use migrations. Always treat the target database as new.
- `database/schema.sql` is the complete canonical schema for the application database.
- `database/logs-schema.sql` is the complete canonical schema for the observability database.
- Update the appropriate complete schema whenever persistence changes.
- Keep the example tenant seed under `database/seeds/` and use it only for local/test bootstrap.
- Use parameterized Dapper queries. Never build SQL from untrusted values.
- Prefer explicit columns and enforce important invariants with SQL constraints and indexes.

## Observability and customer-visible logs

- Use `ILogger<T>` and OpenTelemetry for structured logs, traces, and metrics.
- Store operational logs in the separate database configured by `ConnectionStrings__LogsSqlServer`; do not mix them with SMS application tables.
- Include safe correlation fields where available: `TenantId`, `MessageId`, `Provider`, `TraceId`, and `SpanId`.
- Customer log access must be authenticated and filtered by the JWT tenant claim.
- Tenant-less technical events are support-only and must not be returned by customer-facing endpoints.
- Preserve the separation between technical observability, customer audit events, and SMS message/status history.

## Docker and configuration

- Docker Compose is for local and automated tests only, not the production deployment model.
- Compose must recreate both databases from the complete schema files and provision the example tenant on first startup.
- Keep development fallback secrets clearly non-production and documented.
- Use ASP.NET Core configuration and environment variables for deployment-specific values.
- `Sms:PublicBaseUrl` must remain an externally reachable absolute HTTPS URL and must not be derived from untrusted proxy headers.

## Tests and quality gate

- Add or update tests for every behavior change and bug fix.
- Maintain at least 80% line coverage as measured by the repository Coverlet settings.
- Test tenant isolation, authorization failures, invalid input, provider failures, webhook signature validation, idempotency, and secret-handling paths when relevant.
- Do not weaken coverage exclusions or assertions merely to make CI pass.
- Run before opening a PR:

```bash
dotnet restore Sms.Api.sln
dotnet build Sms.Api.sln --configuration Release --no-restore
dotnet test Sms.Api.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

- When persistence behavior changes, also validate it against SQL Server; unit tests alone are not sufficient for Dapper SQL correctness.

## Codex IDE restrictions

When Codex is being used through an IDE integration:

- Do not run tests.
- Do not create commits.
- Leave changes uncommitted for the user to review and handle. These restrictions override repository workflow instructions that would otherwise require Codex to run tests or create commits.

## Change and PR discipline

- Keep each PR focused on one cohesive functionality or correction. Do not combine unrelated features.
- Use a feature/fix branch; never commit directly to `main`.
- Update `README.md` and Docker test configuration when behavior, configuration, API surface, or bootstrap requirements change.
- Do not merge until the GitHub Actions build, tests, and coverage gate succeed.
- Prefer squash merge so `main` retains one focused commit per PR.
- Preserve existing user changes and avoid unrelated refactors.
- Complete every authorized repository change through the full workflow: create a focused branch, implement and validate the change, open a pull request, wait for all required CI checks to pass, and merge it into `main`.
- Each pull request must contain enough related work to represent a meaningful, cohesive change, while never combining unrelated features or fixes.
- When a requirement is unclear, incomplete, contradictory, or permits materially different implementations, ask the user for clarification before proceeding.

## Current known gaps

- The Angular administration UI is not implemented yet.

Treat these as separate future PRs. Do not silently implement them as part of an unrelated task.
