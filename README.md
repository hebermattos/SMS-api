# SMS API

Multi-tenant REST API for sending, receiving, tracking, and querying SMS messages through provider-independent contracts.

## Stack

- ASP.NET Core / .NET 8
- SQL Server + Dapper
- JWT bearer authentication
- Twilio
- Docker Compose
- xUnit + Coverlet

Bandwidth is planned but its transport and webhook endpoints are not implemented yet.

## Architecture

```text
src/
  Sms.Api/             HTTP endpoints, JWT authentication and tenant context
  Sms.Application/     Use cases and provider-independent contracts
  Sms.Domain/          Domain models
  Sms.Infrastructure/  SQL Server/Dapper, encryption and provider integrations
tests/
  Sms.Infrastructure.Tests/
tools/
  Sms.Provision/
  Sms.Migrate/
database/
  migrations/          Versioned production schema migrations
  seeds/               Test-only bootstrap data
```

All SMS providers implement the same `ISmsProvider` interface and are selected through `ISmsProviderResolver`. Provider credentials are tenant-specific and secrets are encrypted at rest using AES-256-GCM.

## Implemented API

```text
GET  /health

POST /api/v1/auth/token

POST /api/v1/messages
GET  /api/v1/messages
GET  /api/v1/messages/{id}
GET  /api/v1/messages/{id}/status-history

POST /api/v1/webhooks/twilio/inbound
POST /api/v1/webhooks/twilio/status

POST /api/v1/admin/tenants
```

Message endpoints require a JWT. The authenticated `tenant_id` claim controls database access; callers do not supply a tenant ID.

Each message status transition is stored in `SmsMessageStatusHistory`. Authenticated clients can query the chronological status history only for messages belonging to their tenant. Technical application logs remain separate from customer-visible message history and must not expose message bodies, credentials, tokens, or complete phone numbers.

Twilio webhook endpoints are anonymous by design and validate `X-Twilio-Signature` using the tenant provider secret.
Twilio accounts may be shared by multiple tenants. Callback ownership is resolved by the unique active combination of provider, account ID, and configured sender number. A tenant cannot override its configured sender number when sending.

The tenant provisioning endpoint uses `X-Admin-Key`. It is intended as bootstrap administration and should not be exposed publicly without additional administrative access controls.

## Test environment with Docker

Requirements: Docker with Docker Compose.

Start the complete environment:

```bash
docker compose up --build
```

Docker Compose is for local testing only and is not the production deployment model. The migration service creates SQL Server database objects, records applied migrations in `dbo.SchemaMigrations`, and provisions the test tenant. Repeated startups apply only pending migrations, verify that previously applied files were not modified, and safely re-run the idempotent test seed.

Development bootstrap credentials:

```text
Tenant:        Example Tenant
Client ID:     example-client
Client Secret: example-secret-change-me
```

These credentials and the Compose fallback secrets are for local development only. Override them through environment variables before using the stack in any shared environment:

```text
SQL_SA_PASSWORD
JWT_KEY
ENCRYPTION_MASTER_KEY
ADMIN_PROVISIONING_KEY
```

The API is exposed on port `8080`.

## Database migrations

Production migrations do not depend on Docker. Publish or run `tools/Sms.Migrate` before starting a new API version:

```bash
export ConnectionStrings__SqlServer='Server=...;Database=SmsApi;...'
dotnet run --project tools/Sms.Migrate -- --migrations database/migrations
```

The target database is created when it does not exist. Every migration runs transactionally and is recorded with a SHA-256 content hash. Applied migration files must never be edited; add a new numbered file instead.

When adopting the migrator on a database created by older project versions, existing schema objects are detected and recorded as the corresponding baseline migrations. The example tenant seed is intentionally excluded from production migrations and is executed only by Docker Compose.

## Authentication

Request a token:

```http
POST /api/v1/auth/token
Content-Type: application/json

{
  "clientId": "example-client",
  "clientSecret": "example-secret-change-me"
}
```

Use the returned token on protected endpoints:

```text
Authorization: Bearer <access_token>
```

Client secrets are stored as PBKDF2-SHA256 hashes with per-client random salts. JWTs include the authenticated tenant ID.

## Configuration

Runtime configuration is supplied using ASP.NET Core configuration/environment variables. Important settings include:

```text
ConnectionStrings__SqlServer
Jwt__Issuer
Jwt__Audience
Jwt__Key
Jwt__ExpirationMinutes
Encryption__MasterKey
Admin__ProvisioningKey
Sms__DefaultProvider
Sms__PublicBaseUrl
```

`Encryption__MasterKey` must be Base64 encoding of exactly 32 bytes. Do not commit production keys.
`Sms__PublicBaseUrl` must be the externally reachable HTTPS base URL. It is used both when requesting provider status callbacks and when validating webhook signatures, so callback validation does not trust proxy-provided host or scheme headers.

## Tenant provisioning

A tenant and its initial API client are created transactionally. The generated client secret is returned once and cannot be recovered from the database.

For HTTP provisioning, configure `Admin__ProvisioningKey` and call `POST /api/v1/admin/tenants` with `X-Admin-Key`.

A command-line provisioning tool also exists under `tools/Sms.Provision`.

## Tests and CI

Run locally:

```bash
dotnet restore Sms.Api.sln
dotnet build Sms.Api.sln --configuration Release
dotnet test Sms.Api.sln --configuration Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

The CI workflow builds the solution, runs tests, generates Cobertura coverage and enforces a minimum 80% line-coverage threshold. SQL Server/Dapper persistence adapters are reserved for integration testing and excluded from the unit-test coverage calculation. Coverage reports are uploaded as workflow artifacts.

## Security notes

- Tenant isolation is derived from authenticated claims.
- Provider API secrets are encrypted at rest.
- API client secrets use PBKDF2-SHA256.
- Secret/signature comparisons use fixed-time comparison where applicable.
- Twilio error response bodies are not propagated to API callers.
- Development Docker secrets must never be reused in production.
- Docker Compose and the example tenant are for testing only.
- HTTPS termination is required outside local development.

## Current limitations

- Bandwidth provider transport is still a stub.
- Bandwidth inbound/status webhooks are not implemented.
- Angular administration UI is not implemented.

## Contributing

Keep changes focused enough for one pull request. New providers must implement the common provider contract, preserve tenant isolation, include tests, keep coverage at or above 80%, and update this README and Docker configuration when behavior or deployment requirements change.
