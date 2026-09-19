# SMS API

Multi-tenant REST API for sending, receiving, tracking, and querying SMS messages.

## Stack

- ASP.NET Core / .NET 8
- SQL Server and Dapper
- Angular 21 administration console
- JWT authentication
- Twilio and Bandwidth providers
- OpenTelemetry
- Docker Compose for local testing

## Quick start

Requirements: Docker with Docker Compose.

```bash
docker compose up --build
```

Open:

- Console: http://localhost:4200
- API: http://localhost:8080
- SQL Server: localhost,1434

Local credentials:

```text
Client:         example-client / example-secret-change-me
Administrator:  admin / Admin_Local_2026!
```

Override local administrator settings with `ADMIN_USERNAME`, `ADMIN_PASSWORD`, and `ADMIN_EMAIL`.

Reset local databases:

```bash
docker compose down --remove-orphans --volumes
docker compose up --build
```

Docker is for local testing only. The Compose fallback credentials must never be used outside local development.

## Console

The Angular console provides:

- Platform administration: administrators, tenants, API clients, and providers.
- Tenant operations: send SMS, history, status history, reports, configurable alert rules, in-console alerts, and tenant logs.

For frontend development:

```bash
cd ui
npm ci
npm start
```

The API must run on port 8080. Use `SMS_API_URL` to change the development proxy.

## API

Public:

```text
GET  /health
POST /api/v1/auth/token
POST /api/v1/portal/auth/token
POST /api/v1/admin/auth/token
POST /api/v1/webhooks/twilio/inbound
POST /api/v1/webhooks/twilio/status
POST /api/v1/webhooks/bandwidth/inbound
POST /api/v1/webhooks/bandwidth/status
```

Tenant-protected:

```text
POST /api/v1/messages
GET  /api/v1/messages
GET  /api/v1/messages/{id}
GET  /api/v1/messages/{id}/status-history
GET  /api/v1/logs
GET  /api/v1/overview
GET  /api/v1/alerts
GET  /api/v1/alerts/rules
POST /api/v1/alerts/rules
PUT  /api/v1/alerts/rules/{id}
DELETE /api/v1/alerts/rules/{id}
POST /api/v1/alerts/{id}/read
POST /api/v1/alerts/read-all
```

Platform-administrator-protected:

```text
GET  /api/v1/admin/administrators
POST /api/v1/admin/administrators
PUT  /api/v1/admin/administrators/{id}/state
POST /api/v1/admin/administrators/{id}/reset-password
GET  /api/v1/admin/tenants
POST /api/v1/admin/tenants
GET  /api/v1/admin/tenants/{id}
PUT  /api/v1/admin/tenants/{id}
GET  /api/v1/admin/tenants/{id}/clients
POST /api/v1/admin/tenants/{id}/clients
PUT  /api/v1/admin/tenants/{id}/clients/{id}/state
POST /api/v1/admin/tenants/{id}/clients/{id}/rotate-secret
GET  /api/v1/admin/providers/catalog
GET  /api/v1/admin/tenants/{id}/providers
PUT  /api/v1/admin/tenants/{id}/providers/{provider}
GET  /api/v1/admin/system-logs
```

Tenant access is always derived from the authenticated JWT. Callers cannot select a tenant through request parameters.

## Providers

All providers implement `ISmsProvider` and are selected through `ISmsProviderResolver`.

- Twilio: signed callbacks using `X-Twilio-Signature`.
- Bandwidth: OAuth 2.0 Client Credentials and Basic-authenticated callbacks.

Provider credentials are tenant-specific and encrypted at rest. Configure Bandwidth callbacks at:

```text
/api/v1/webhooks/bandwidth/inbound
/api/v1/webhooks/bandwidth/status
```

Use HTTPS for real provider callbacks.

## Configuration

Important environment variables:

```text
ConnectionStrings__SqlServer
ConnectionStrings__LogsSqlServer
Jwt__Issuer
Jwt__Audience
Jwt__Key
Jwt__ExpirationMinutes
Encryption__MasterKey
Sms__DefaultProvider
Sms__PublicBaseUrl
Alerts__EvaluationSeconds
```

`Encryption__MasterKey` must be Base64 for exactly 32 bytes. `Sms__PublicBaseUrl` must be an HTTPS URL without credentials, query strings, or fragments.

## Database

The project does not use migrations. Initialize new databases with:

- `database/schema.sql`
- `database/logs-schema.sql`

The application database stores tenants, users, clients, providers, messages, status history, alert rules, and triggered alerts. The separate `SmsApiLogs` database stores user activity, system logs, traces, and metrics.

All dates are stored in UTC. Each tenant has an IANA time zone for display and date filters.

Alert rules count distinct messages entering the selected status within the configured window. Rules can trigger once per incident or repeat at a configured interval while the condition remains true. The evaluator runs every 60 seconds by default; configure `Alerts__EvaluationSeconds` between 10 and 3600 seconds.

## Security

- JWT authorization separates tenant users, tenant administrators, and platform administrators.
- Tenant isolation is enforced from authenticated claims.
- SMS content and provider secrets use application-side AES-256-GCM encryption.
- Passwords and client secrets use PBKDF2-SHA256.
- Webhooks validate provider credentials or signatures.
- Customer logs are tenant-isolated.
- Technical logs contain only error-level events.
- Secrets, tokens, authorization headers, SMS bodies, and full phone numbers must not be logged.
- Use HTTPS outside local development.
- MFA and self-service password recovery are not implemented.

## Provisioning

Create the first administrator outside Compose:

```bash
dotnet run --project tools/Sms.Provision -- --admin
```

Provide `ConnectionStrings__SqlServer`, `Admin__Username`, `Admin__Password`, and `Admin__Email` through the environment.

Create a tenant through the authenticated platform-administrator API or console. The generated client secret is returned once.

## Tests and CI

Run locally:

```bash
dotnet restore Sms.Api.sln
dotnet build Sms.Api.sln --configuration Release
dotnet test Sms.Api.sln --configuration Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

The CI workflow runs only on `main`, builds the solution, tests the API and Angular console, validates Docker initialization, runs SQL integration tests, and enforces at least 80% line coverage.

## Architecture

```text
src/Sms.Api             HTTP, authentication, authorization
src/Sms.Application     Use cases and contracts
src/Sms.Domain          Domain models
src/Sms.Infrastructure  SQL Server, encryption, providers, observability
ui                      Angular console
database                Canonical schemas and test seeds
tools/Sms.Provision     Bootstrap provisioning
tests                   Unit and SQL integration tests
```
