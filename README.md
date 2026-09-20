# SMS API

[![Proudly Vibe Coded](https://vibecoded.fyi/badges/flat/main/proudly-vibe-coded.svg)](https://vibecoded.fyi/)
![Built with Codex](https://img.shields.io/badge/Built%20with-Codex-000000?style=flat-square&logo=openai&logoColor=white)
![AI Reviewed](https://img.shields.io/badge/AI%20Generated-Human%20Reviewed-blue?style=flat-square)

Multi-tenant REST API for sending, receiving, scheduling, tracking, and querying SMS messages.

## Stack

- ASP.NET Core / .NET 8
- PostgreSQL 17 + Dapper
- Angular 21
- JWT authentication
- Twilio and Bandwidth
- RabbitMQ + MassTransit
- Redis
- OpenTelemetry + ClickStack (ClickHouse)
- Ollama + Qwen2.5 0.5B for local message assistance
- Docker Compose for local testing

## Quick start

Requires Docker with Docker Compose.

```bash
docker compose up --build
```

Local services:

- UI: http://localhost:4200
- API: http://localhost:8080
- Swagger: http://localhost:8080/swagger
- Health: http://localhost:8080/health
- PostgreSQL: localhost:5432
- Redis: localhost:6379
- ClickStack / HyperDX: http://localhost:8081 (Basic Auth)
- ClickHouse HTTP: http://localhost:18123
- OTLP: localhost:4317 (gRPC) / localhost:4318 (HTTP)

Local credentials:

```text
Client:    client / client
Platform:  platform / platform
HyperDX:   HyperDX / HyperDX
RabbitMQ:  sms / sms
```

Override administrator defaults with `ADMIN_USERNAME`, `ADMIN_PASSWORD`, and `ADMIN_EMAIL`. Override the local HyperDX access credentials with `HYPERDX_USERNAME` and `HYPERDX_PASSWORD`.

Reset the local environment:

```bash
docker compose down --remove-orphans --volumes
docker compose up --build
```

Database backups are stored outside Docker volumes in `./backups` by default, so `docker compose down --volumes` does not delete them. Set `POSTGRES_BACKUP_PATH` to an absolute path on independent storage for stronger protection.

Docker is intended for local testing only. Never use fallback Compose credentials outside development.

Docker Compose defines soft memory reservations for each service:

| Service | Memory reservation |
| --- | ---: |
| PostgreSQL | 128 MB reserved / 256 MB limit |
| RabbitMQ | 256 MB |
| Redis | 64 MB |
| Ollama | 768 MB |
| API | 256 MB |
| ClickStack | 512 MB |
| HyperDX authentication proxy | 32 MB |
| UI | 32 MB |
| Database initialization | 128 MB |
| PostgreSQL backup | 64 MB |
| Provider initialization | 128 MB |
| Webhook tests | 256 MB |

These values are resource reservations, not hard memory limits. Docker may allow a container to use more memory when the host has capacity. The UI starts independently from the API; its Nginx proxy resolves the API dynamically, so the UI container can remain available while backend dependencies are still starting. API requests return a gateway error until the API becomes reachable.

## Features

### Tenant

- Send and schedule SMS messages. Outbound messages may be associated with a tenant user; portal sends are associated automatically, while API clients may supply an optional `userId`.
- Use the local AI assistant to improve and validate SMS/template text without sending message content to a hosted AI service.
- Create reusable message templates with `{{variableName}}` variables. System variables include `{{recipientName}}`, `{{recipientPhone}}`, and `{{tenantName}}`; custom variables can be supplied by API, CSV, or UI workflows.
- Query message and status history.
- Reports and overview dashboards, with CSV download of the currently displayed report table.
- Configurable alert rules and in-UI alerts.
- Opt-out management with CSV import/export.
- Tenant user administration.
- Tenant-isolated activity logs.

### Platform

- Manage platform administrators.
- Manage tenants, API clients, and SMS providers.
- View platform reports and system logs.

Swagger documents the complete API surface.

## Local AI message assistant

Docker Compose runs Ollama locally with `qwen2.5:0.5b`, a small model intended for lightweight message assistance. The API exposes authenticated `POST /api/v1/message-assistant/improve` and `POST /api/v1/message-assistant/validate` endpoints.

Ollama model initialization runs independently from the API startup. A slow or failed model pull does not prevent the API from starting; AI assistance becomes available after `ollama-init` successfully downloads the model.

The improve operation makes SMS text shorter and clearer while instructing the model to preserve template variables exactly. Validation checks clarity, spelling, ambiguous wording, and malformed template placeholders. It does not make legal/compliance decisions.

SMS/template content sent to these endpoints stays inside the local Ollama deployment. AI output should be treated as a suggestion and reviewed before sending. Platform administrators can configure separate improve and validation prompts for each tenant from the company settings screen. Tenant users can use **Improve with AI** and **Validate** directly from Send SMS and the template editor.

## Messaging

Outbound provider calls use a platform-wide transient-failure retry policy. HTTP 429, HTTP 5xx, and provider network failures are retried with exponential backoff. Permanent provider errors, invalid configuration, invalid numbers, and opt-out failures are not retried. The defaults are 3 retries with an initial 60-second interval (approximately 1, 2, and 4 minutes). Configure globally with `SmsRetry__MaxAttempts` and `SmsRetry__InitialIntervalSeconds`; the effective policy is available to platform administrators at `GET /api/v1/admin/sms-retry`.



The provider is selected per request. All providers implement `ISmsProvider`, while provider-specific code remains isolated from the application core.

- **Twilio:** signed callbacks using `X-Twilio-Signature`.
- **Bandwidth:** OAuth 2.0 Client Credentials and authenticated callbacks. OAuth access tokens are cached in Redis until shortly before their reported expiration.

Tenant configuration is also cached in Redis. Tenant metadata, time zone, API-client authentication data, and SMS-provider configuration have no time-based cache expiration and are invalidated only after a persisted configuration change. Provider secrets remain encrypted while cached.

Immediate messages are queued through RabbitMQ/MassTransit. Scheduled messages are stored in UTC and queued when due.

To schedule a message, send `scheduledAt` as a local date/time without an offset. The API converts it using the tenant IANA time zone. API clients may also send an optional `userId`; when provided, it must identify an active user in the authenticated tenant.

```json
{
  "to": "+15551234567",
  "body": "Your appointment is tomorrow.",
  "provider": "Twilio",
  "scheduledAt": "2026-10-20T09:30:00"
}
```

Inbound `STOP`, `UNSUBSCRIBE`, and `CANCEL` opt the number out; `START` removes the block.

## Configuration

Main environment variables:

```text
ConnectionStrings__Postgres
ConnectionStrings__LogsPostgres
ConnectionStrings__ReportingPostgres
ConnectionStrings__Redis
Cache__Enabled
Jwt__Issuer
Jwt__Audience
Jwt__Key
Jwt__ExpirationMinutes
Encryption__MasterKey
Sms__DefaultProvider
Sms__PublicBaseUrl
RabbitMq__Host
RabbitMq__Port
RabbitMq__User
RabbitMq__Password
HYPERDX_USERNAME
HYPERDX_PASSWORD
OTEL_EXPORTER_OTLP_ENDPOINT
OTEL_EXPORTER_OTLP_PROTOCOL
OTEL_SERVICE_NAME
```

`Cache__Enabled` defaults to `true`. Set it to `false` to bypass all Redis-backed caching, including tenant configuration and Bandwidth OAuth tokens; when disabled, the Redis connection string is not required by the API. `Encryption__MasterKey` must be Base64 for exactly 32 bytes. Use HTTPS for real provider callbacks and outside local development. Redis should be reachable only from trusted application infrastructure.

## Health

`GET /health` checks the API dependencies and returns their individual status and latency.

- `postgres.application` — application database; failure makes the API unhealthy.
- `postgres.observability` — tenant activity and platform error-log database.
- `postgres.reporting` — reporting database.
- `redis` — Redis connectivity used by the caches; reports `Healthy` with `Cache is disabled.` when caching is disabled.
- `rabbitmq` — RabbitMQ TCP connectivity.
- `twilio` — Twilio API reachability.
- `bandwidth` — Bandwidth API reachability.

The endpoint returns HTTP `503` when a critical internal dependency is unhealthy. Reporting, observability, Redis, and external provider failures are reported as `Degraded` without taking the API out of rotation.

Provider checks validate network/API reachability only. They do not validate tenant-specific credentials and do not expose connection strings, credentials, tokens, or exception details.

## Database

The project uses Dapper and does not use migrations. Runtime SQL is stored under `src/Sms.Infrastructure/Sql`.

Fresh databases are initialized from:

- `database/schema.sql` — application data
- `database/logs-schema.sql` — tenant activity and platform error logs
- `database/reporting-schema.sql` — reporting projections

### Automatic backup and point-in-time recovery

Docker Compose protects PostgreSQL with two complementary mechanisms:

- A physical base backup created every 24 hours with `pg_basebackup`.
- Continuous WAL archiving, with PostgreSQL forcing an archive segment switch at least every 60 seconds by default.

Together, the base backup and archived WAL files support **Point-in-Time Recovery (PITR)**. A restore can replay database changes after the latest base backup up to a selected UTC recovery timestamp. This avoids the previous design's potential loss of up to 24 hours of data.

The defaults can be changed with:

```text
POSTGRES_BACKUP_PATH=./backups
BACKUP_INTERVAL_SECONDS=86400
BACKUP_RETENTION_DAYS=7
WAL_ARCHIVE_TIMEOUT_SECONDS=60
```

`POSTGRES_BACKUP_PATH` is a host bind mount rather than a Docker named volume. Therefore, `docker compose down --volumes` does not remove the backups. For production, set it to storage independent from the PostgreSQL data disk/host and copy or replicate it to off-site storage.

Base backups are written to a temporary directory and renamed only after `pg_basebackup` succeeds. WAL files are archived continuously under `<backup-path>/wal`. Both base backups and archived WAL files use the configured retention period.

List the backups and archived WAL files:

```bash
ls -lh ./backups/base
ls -lh ./backups/wal
```

For PITR, restore the selected physical base backup into an empty PostgreSQL data directory, make the corresponding archived WAL files available, configure `restore_command` to copy WAL files from the archive, set `recovery_target_time` to the required UTC timestamp, create `recovery.signal`, and start PostgreSQL. Recovery must be tested regularly before relying on the backup as a disaster-recovery mechanism.

The default `WAL_ARCHIVE_TIMEOUT_SECONDS=60` bounds how long a low-traffic server can keep an unarchived partial WAL segment before PostgreSQL forces a switch. It is an **RPO target, not a zero-data-loss guarantee**: host/storage failure can still lose WAL that has not reached independent storage. Guaranteed zero data loss across a complete database-host failure requires synchronous replication to another PostgreSQL instance in addition to backups.

All dates are stored in UTC. Each tenant has an IANA time zone used for display, filters, and scheduled delivery.

Application data, tenant activity logs, reporting data, opt-outs, provider credentials, alerts, and message processing are tenant-isolated.

## Observability

Observability is deliberately split between **audit data** and **technical telemetry**:

| Data | Destination | Access |
| --- | --- | --- |
| Tenant user activity | PostgreSQL `sms_api_logs` | Tenant users, filtered by tenant |
| Platform error logs | PostgreSQL `sms_api_logs` | Platform users |
| OpenTelemetry logs | ClickStack / ClickHouse | Technical operations |
| OpenTelemetry traces | ClickStack / ClickHouse | Technical operations |
| OpenTelemetry metrics | ClickStack / ClickHouse | Technical operations |

The API exports technical telemetry over OTLP to ClickStack. ClickStack bundles the OpenTelemetry Collector, ClickHouse storage, and HyperDX UI. This keeps high-volume telemetry writes out of the PostgreSQL audit database while preserving the existing authorization and tenant-isolation model for user activity logs.

In Docker Compose the API sends OTLP/HTTP protobuf to `http://clickstack:4318`. This avoids HTTP/2 gRPC transport issues such as `ENHANCE_YOUR_CALM` / `too_many_pings` while preserving the same OpenTelemetry logs, traces, and metrics pipeline. The local ClickStack container runs its HyperDX UI without built-in authentication and is reachable only inside the Compose network. A small Caddy proxy exposes HyperDX at `http://localhost:8081` with Basic Auth, defaulting to `HyperDX / HyperDX`; override those development defaults with `HYPERDX_USERNAME` and `HYPERDX_PASSWORD`. OTLP/gRPC and OTLP/HTTP remain exposed directly on ports `4317` and `4318`, and the ClickHouse HTTP endpoint is mapped to `18123`.

ClickStack is technical infrastructure and must not be exposed as a tenant-facing log source. Secrets, access tokens, authorization headers, SMS bodies, and full phone numbers must never be emitted as telemetry.

## Security

- Tenant ownership is derived from authenticated JWT claims.
- JWT separates tenant users, tenant administrators, and platform administrators.
- Tenant portal users authenticate with tenant code + username + password, so usernames may be reused safely across tenants.
- SMS content and provider secrets use AES-256-GCM encryption.
- Passwords and client secrets use PBKDF2-SHA256 with at least 600,000 iterations.
- Provider webhooks are authenticated whenever supported.
- Technical logs store only error-level events.
- Secrets, tokens, authorization headers, SMS bodies, and full phone numbers must not be logged.
- MFA and self-service password recovery are not yet implemented.

## Provisioning

Create the first platform administrator outside Compose:

```bash
dotnet run --project tools/Sms.Provision -- --admin
```

Provide `ConnectionStrings__Postgres`, `Admin__Username`, `Admin__Password`, and `Admin__Email` through the environment.

Tenants can then be created through the platform administration UI or API. Generated client secrets are returned once.

## Tests and CI

```bash
dotnet restore Sms.Api.sln
dotnet build Sms.Api.sln --configuration Release
dotnet test Sms.Api.sln --configuration Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

Pushes to `main` build and test the backend and Angular UI and require at least **80% backend line coverage**.

PostgreSQL integration tests and the Docker Compose bootstrap run only from a manually started workflow.

## Architecture

### Docker Compose

![SMS API Docker Compose architecture](docs/images/sms-api-architecture-v2.svg)

The diagram reflects the current Docker Compose topology and startup dependencies. PostgreSQL hosts the application, audit/error-log, and reporting databases. Redis provides caching, RabbitMQ handles asynchronous messaging, and ClickStack receives technical OpenTelemetry logs, traces, and metrics over OTLP and persists them in ClickHouse.

The API waits for database/provider initialization, RabbitMQ, Redis, and ClickStack before starting. HyperDX browser access is exposed separately through the Basic Auth proxy. The optional `webhook-tests` service is enabled through the `tests` profile.

```text
src/Sms.Api              HTTP, authentication, authorization
src/Sms.Application      Use cases and contracts
src/Sms.Domain           Domain models
src/Sms.Infrastructure   SQL, providers, encryption, observability
ui                       Angular UI
database                 Database schemas and test seeds
tools/Sms.Provision      Bootstrap provisioning
tests                    Unit and integration tests
```

## RabbitMQ monitoring

The API collects RabbitMQ queue metrics from the Management API every five minutes and exports them through the existing OpenTelemetry pipeline to ClickStack/HyperDX.

- `rabbitmq.queue.messages.ready`: messages waiting for a consumer.
- `rabbitmq.queue.messages.unacknowledged`: messages currently being processed.
- `rabbitmq.queue.consumers`: active consumers per queue.

Metrics include the `rabbitmq.queue` attribute for filtering. The monitored queues are `sms.send`, `sms.alert.evaluation`, and `sms.reporting.overview`. Collection failures are logged as errors.

## Roadmap

### Account security

- [ ] MFA for platform and tenant administrators.
- [ ] Self-service password recovery.
- [ ] Session management and token revocation.
- [ ] Configurable password and lockout policies.
- [ ] Login history and active-session visibility.
- [ ] Optional IP allowlists for API clients.
- [ ] API credential rotation and expiration policies.

### Operations

- [ ] Email and webhook alert delivery.
- [ ] Alert retry history and dead-letter handling.
- [ ] Operational dashboards for queues, backlog, provider latency, and failures.
- [ ] Configurable data retention.
- [ ] Backup verification and restore drills.
- [ ] Off-site backup storage.
- [ ] Backup failure notifications.
- [x] RabbitMQ queue monitoring.
- [ ] RabbitMQ dead-letter management.
- [ ] PostgreSQL health, storage, and slow-query monitoring.
- [ ] Tenant usage quotas and consumption dashboards.

### Messaging

- [x] Scheduled delivery in the tenant time zone.
- [ ] Bulk sends with validation, progress, cancellation, and per-recipient results.
- [x] Tenant message templates with system and custom variables.
- [ ] Inbound auto-replies and routing rules.
- [ ] Use templates directly from the Send SMS screen.
- [ ] Template variables populated from bulk CSV columns.
- [ ] Template preview with sample variable values.
- [x] Local AI assistance for message/template improvement and validation.
- [ ] Template duplication and version history.
- [ ] Message drafts.
- [ ] Recurring scheduled messages.
- [ ] Conversation view grouped by phone number.
- [ ] Search and filtering by recipient, provider, status, and date.
- [ ] Delivery retry policies for retryable provider failures.
- [ ] Per-tenant sending windows and quiet hours.

### Extensibility

- [ ] Provider failover and routing policies.
- [ ] Provider health and cost reporting.
- [ ] Provider extension guide and contract tests.
- [ ] Additional provider implementation.
- [ ] Weighted provider routing.
- [ ] Per-tenant provider priority.
- [ ] Provider-specific rate-limit awareness.
- [ ] Webhook signing guide for new providers.

### Compliance and governance

- [ ] Configurable opt-out keywords.
- [ ] Consent tracking and audit history.
- [ ] Tenant-configurable message retention.
- [ ] Data export for a tenant.
- [ ] Tenant data deletion workflow.
- [ ] Audit export for compliance reviews.

### Developer experience

- [ ] Public API documentation with request and response examples.
- [ ] Webhook integration examples.
- [ ] API client examples for C#, JavaScript, and curl.
- [ ] Sandbox mode for integration testing.
- [ ] Idempotency keys for send requests.
- [ ] API versioning and deprecation policy.
