# SMS API

Multi-tenant REST API for sending, receiving, tracking, and querying SMS messages through provider-independent contracts.

## Stack

- ASP.NET Core / .NET 8
- SQL Server + Dapper
- JWT bearer authentication
- Twilio and Bandwidth
- OpenTelemetry logs, traces and metrics
- Docker Compose
- xUnit + Coverlet

Bandwidth outbound SMS uses OAuth 2.0 Client Credentials. Inbound and delivery-status webhooks use tenant-specific HTTP Basic authentication.

## Administration console

Platform actions are audited through `PlatformAuditMiddleware` into `SmsApiLogs.dbo.UserActivityLogs`. Events include the controller action, verified administrator ID (or bootstrap/unauthenticated actor), outcome, HTTP status, and valid target tenant/client IDs from the route. Login attempts (including rate limits), company creation and updates, client creation/state changes/secret rotation, provider configuration, system-log reads, and administrative reads are recorded. Bodies, headers, credentials, provider input and exception details are excluded. Platform activity has no `TenantId` and is never returned by tenant queries. Successful administrator logins and subsequent requests record the individual administrator ID. Failed logins never record a caller-supplied username. Company creation has no target ID in the route. Browser-only actions such as logout are not recorded. Delivery uses the existing batched log exporter, not a durable transactional audit ledger.

The Angular 21 console in `ui/` provides two separate workspaces, in English:

- **Platform administrators:** manage administrator accounts, list/create companies, edit names, suspend/reactivate access, create/disable/reactivate API clients, rotate client secrets, and configure tenant-specific Twilio/Bandwidth credentials and default senders.
- **Tenant users:** view message totals, send SMS, page through sent/received messages, inspect status history, and query operational logs by date range. The API derives their tenant solely from their JWT.

Start the local stack with `docker compose up --build` and open `http://localhost:4200`. The console proxies `/api/` to the API, so no permissive CORS policy is needed. Use the seeded `example-client` / `example-secret-change-me` for **Client**, or username `admin` and password `Admin_Local_2026!` for **Administrator**. Override the initial administrator through `ADMIN_USERNAME` and `ADMIN_PASSWORD`
`ADMIN_EMAIL` before initializing the local stack. These credentials are local-only. TLS termination is required in shared or production environments; Compose is not a production deployment model.

For frontend development, use Node.js 20.19+ or 22.12+ (Angular 21 compatible), run `npm ci` and `npm start` from `ui/`, and keep the API running on port 8080. The development proxy defaults to `http://localhost:8080`; set `SMS_API_URL` to change its upstream. `npm run build` compiles the production bundle into `ui/dist/console/browser`. Serve it with an SPA fallback and a same-origin `/api/` reverse proxy. The Angular project is built separately from `Sms.Api.sln`.

Administrator login accepts `username` and `password` at `POST /api/v1/admin/auth/token`. Accounts are stored in `PlatformAdministrators`; passwords use PBKDF2-SHA256 with 600,000 iterations and a unique random salt. Usernames are case-insensitive. Provisioned passwords must contain 15–128 characters. Successful login returns a 15-minute JWT with the administrator ID, username, and `platform_admin` privilege, with no tenant claim. The API rechecks account activation on every authenticated administrator request, so disabling an account also blocks existing tokens. Unknown users, incorrect passwords, and inactive accounts all receive HTTP 401; the existing IP login rate limiter remains enabled. Tenant tokens cannot access administrative endpoints; administrator tokens cannot access tenant messages or logs. Old shared-key administrator sessions are rejected and must sign in again. The `X-Admin-Key` tenant-bootstrap endpoint remains supported separately; its key can no longer be exchanged for a console token.

The responsive Angular sign-in screen provides separate client and administrator access, labeled fields, credential visibility controls, Caps Lock feedback, and accessible validation, loading, and error states. A valid existing session redirects to the appropriate workspace without another login request.

Browser sessions use `sessionStorage` to preserve the token, portal role, and display identity across page reloads in the same tab. The console restores valid sessions before checking protected routes; expired or malformed saved sessions are discarded. Logout, token expiry, and API rejection (HTTP 401) clear the saved session. Passwords and client secrets are never stored in browser storage, and password inputs are cleared after submissions. If browser storage is blocked, login still works in memory but cannot survive refresh. Session storage is accessible to same-origin JavaScript; it is not an HttpOnly cookie. The API remains responsible for all authorization.

The API never returns stored provider secrets; blank password fields preserve saved values, and newly generated API-client secrets are displayed once. Disabling a tenant or client blocks existing tokens on subsequent API requests. Rotating a client secret prevents new logins with the old secret; already issued tokens remain valid until expiry unless that client is disabled. Suspending a tenant does not delete messages or stop validated provider callbacks.

No database migration is needed: all portal operations use the canonical schema. Default-provider changes are serialized per tenant and committed atomically. SQL integration tests in `AdministrationSqlTests` exercise tenant/client operations, encrypted settings, default switching, and isolation. To run them when tests are permitted, set `SMS_TEST_SQLSERVER` to a disposable database initialized from `database/schema.sql` and use `dotnet test Sms.Api.sln --filter FullyQualifiedName~AdministrationSqlTests`. Frontend test sources can be run with `npm test` inside `ui/`.

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
database/
  schema.sql            Complete schema for a new database
  seeds/               Test-only bootstrap data
```

All SMS providers implement the same `ISmsProvider` interface and are selected through `ISmsProviderResolver`. Provider credentials are tenant-specific and secrets are encrypted at rest using AES-256-GCM. SMS sender, recipient and body are also encrypted by the application before SQL persistence and decrypted only after tenant-scoped retrieval.

## Implemented API

```text
GET  /health

POST /api/v1/auth/token

POST /api/v1/messages
GET  /api/v1/messages
GET  /api/v1/messages/{id}
GET  /api/v1/messages/{id}/status-history
GET  /api/v1/logs

POST /api/v1/webhooks/twilio/inbound
POST /api/v1/webhooks/twilio/status
POST /api/v1/webhooks/bandwidth/inbound
POST /api/v1/webhooks/bandwidth/status

POST /api/v1/admin/auth/token
GET  /api/v1/admin/administrators
POST /api/v1/admin/administrators
PUT  /api/v1/admin/administrators/{administratorId}/state
POST /api/v1/admin/administrators/{administratorId}/reset-password
GET  /api/v1/admin/tenants
POST /api/v1/admin/tenants
GET  /api/v1/admin/tenants/{tenantId}
PUT  /api/v1/admin/tenants/{tenantId}
GET  /api/v1/admin/tenants/{tenantId}/clients
POST /api/v1/admin/tenants/{tenantId}/clients
PUT  /api/v1/admin/tenants/{tenantId}/clients/{clientId}/state
POST /api/v1/admin/tenants/{tenantId}/clients/{clientId}/rotate-secret
GET  /api/v1/admin/providers/catalog
GET  /api/v1/admin/tenants/{tenantId}/providers
PUT  /api/v1/admin/tenants/{tenantId}/providers/{provider}
GET  /api/v1/overview
```

Message endpoints require a JWT. The authenticated `tenant_id` claim controls database access; callers do not supply a tenant ID.

Each message status transition is stored in `SmsMessageStatusHistory`. Authenticated clients can query the chronological status history only for messages belonging to their tenant.

OpenTelemetry stores logs, traces, and metrics in the separate SQL Server database configured through `ConnectionStrings__LogsSqlServer`. Logs use `UserActivityLogs` and `SystemLogs`, spans use `Traces`, and metric points use `Metrics`. Trace and metric exporters persist only an allowlist of safe HTTP attributes; raw URLs, query strings, authorization values, credentials, tokens, message bodies, and phone numbers are excluded. Authenticated clients can query `GET /api/v1/logs`; the `tenant_id` JWT claim is always applied by the server. Traces and metrics are platform observability data and are not exposed by customer endpoints.

The observability database separates user activity from technical failures. `UserActivityLogs` records client and platform audit events at every severity; tenant users can query only rows carrying their authenticated `TenantId`. `SystemLogs` stores only `Error` and `Critical` technical events and is available only to authenticated platform administrators through **System logs** or `GET /api/v1/admin/system-logs`. The API also writes logs to the console, with one event per line and a UTC timestamp, severity, and category. Console output is independent of SQL log persistence, so it can help diagnose missing database events. `Logging__Console__LogLevel__Sms` controls application console verbosity (default: `Information`).

To apply an API logging update locally without rerunning database initialization, rebuild only the API and follow its output:

```bash
git pull
docker compose up -d --build --no-deps api
docker compose logs -f --tail 100 api
```

Sign in again and perform an action to generate new audit events; existing sessions do not replay their login event.

Twilio webhook endpoints are anonymous by design and validate `X-Twilio-Signature` using the tenant provider secret.
Twilio accounts may be shared by multiple tenants. Callback ownership is resolved by the unique active combination of provider, account ID, and configured sender number. A tenant cannot override its configured sender number when sending.

The tenant provisioning endpoint uses `X-Admin-Key`. It is intended as bootstrap administration and should not be exposed publicly without additional administrative access controls.

## Bandwidth webhooks

Configure the Bandwidth Messaging Application with these URLs under your externally reachable HTTPS base URL:

- Inbound Callback URL: `/api/v1/webhooks/bandwidth/inbound`
- Status Callback URL: `/api/v1/webhooks/bandwidth/status`

Use a separate Messaging Application per tenant so each application has its own callback credentials, even when tenants share an OAuth account. Enable Basic authentication on both callbacks. Use the tenant provider configuration's `AccountId` (the OAuth **client ID**, not the Bandwidth messaging account ID) as the callback username. Generate a strong, separate callback password for each tenant and set the same password in both Bandwidth callback configurations and `Settings.webhookPassword`. Do not reuse the OAuth client secret.

The existing `TenantSmsProviderConfiguration` uses `Provider = "Bandwidth"`, `AccountId` for the OAuth client ID, `ApiSecret` for the OAuth client secret, and `FromNumber` for the tenant's Bandwidth number. Its `Settings` JSON must include:

```json
{
  "accountId": "<Bandwidth messaging account ID>",
  "applicationId": "<Bandwidth Messaging Application ID>",
  "webhookPassword": "<separate generated callback password>"
}
```

Provision this configuration through `ITenantSmsProviderRepository.UpsertAsync` in trusted administrative code; it encrypts `ApiSecret` and the complete `Settings` value with AES-256-GCM. The administration console provisions provider settings through authenticated administrative endpoints. The tenant-creation HTTP endpoint and CLI themselves create only the tenant and its initial client. Never insert plaintext secrets directly into SQL Server. Existing outbound-only configurations continue to send messages, but callbacks fail authentication until `webhookPassword` is configured.

Callbacks accept JSON arrays of 1–100 events, with a maximum request body of 1 MiB. The receiver validates every event's credentials, application ID, owner number, recipient and direction before writing any event. The tenant comes from the authenticated provider configuration; payload tenant identifiers are ignored. Missing or invalid credentials receive `401` with a Basic challenge, malformed or unsupported events receive `400`, and non-JSON requests with credentials receive `415`. Successful callbacks, including duplicates and valid status callbacks for unknown message IDs, receive `204`.

Supported events are `message-received` (Received), `message-sending` and `message-sent` (Sent), `message-delivered` (Delivered), and `message-failed` (Failed). Configure only these status events. Duplicate inbound messages and repeated status transitions do not create duplicate history. Late intermediate events cannot overwrite a terminal status. A database failure leaves the request unsuccessful so Bandwidth can retry; any already-persisted events are safe to replay. The API stores the inbound owner's copy of group-message text, not media attachments.

See Bandwidth's [callback payload reference](https://dev.bandwidth.com/docs/messaging/webhooks/) and [callback authentication documentation](https://dev.bandwidth.com/docs/numbers/webhooks/#authentication).

## Test environment with Docker

Requirements: Docker with Docker Compose.

Start the complete environment:

```bash
docker compose up --build
```

Docker Compose is for local testing only and is not the production deployment model. SQL Server data is stored in the named `sqlserver-data` volume. On first initialization, `db-init` creates `SmsApi` and `SmsApiLogs`, applies the complete schemas, and seeds the example tenant. Subsequent `docker compose up --build` runs preserve existing databases; the idempotent `provider-init` service ensures the local administrator and provider fixtures are present. To intentionally reset local databases and remove the volume, run:

```bash
docker compose down --remove-orphans --volumes
docker compose up --build
```

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
ADMIN_USERNAME
ADMIN_PASSWORD
```

After `db-init`, the one-shot `provider-init` service runs the application seed in `database/seeds/Sms.Seed`. It creates the initial platform administrator if that username does not exist, without overwriting an existing password. It saves both providers through the existing repository, encrypting their API secrets and settings with the same `ENCRYPTION_MASTER_KEY` used by the API. The API waits for this step to succeed. The seed uses fictional credentials only and makes no calls to provider APIs.

| Provider | Account/client ID | Sender | Default |
| --- | --- | --- | --- |
| Twilio | `AC00000000000000000000000000000000` | `+15005550006` | Yes |
| Bandwidth | `fake-bandwidth-client-id` | `+12025550101` | No |

Both configurations are active and visible in the console, but **fictional credentials cannot send real SMS**. They are not a mock delivery service. Select `Twilio` or `Bandwidth` in the send request's `provider` parameter as usual. Replace credentials and sender settings through the administration console before testing delivery with a real account. Bandwidth also needs its messaging account ID, application ID, and a separate webhook password.

Test-account availability checked on September 18, 2026:

- [Twilio test credentials](https://www.twilio.com/docs/iam/test-credentials) are account-specific. Existing legacy-console test credentials continue to work; the documentation says new ones cannot be created in the new console and directs new users to a trial account. Test credentials simulate SMS without delivery or status callbacks. The seeded SID/token are fictional, even though the sender is Twilio's documented test number.
- [Bandwidth offers a trial](https://www.bandwidth.com/request-trial/) for its messaging and other APIs, requested through registration. No public shared credentials were found for automatic bootstrap; the seed uses a fictional OAuth client, messaging account, application, and webhook password.

`provider-init` is intended for a new local/test database. Rerunning it replaces the example tenant's provider settings with the fixtures. To populate an existing local test database deliberately without recreating it, run `docker compose run --build --rm --no-deps provider-init` while SQL Server is running. Inspect failures with `docker compose logs provider-init`.

The API is exposed on port `8080`, and SQL Server is exposed on host port `1434`.

If an older Compose stack reports that `SmsApi` or `SmsApiLogs` cannot be opened, perform the explicit reset above. Database creation is performed by the one-shot `db-init` service with the SQL Server 2022 `sqlcmd` tools. If initialization still fails, inspect its output with `docker compose logs db-init`.

The initializer connects to `tcp:sqlserver,1433` inside the Compose network; host applications such as SSMS use `localhost,1434`. Its Bash command must remain a single list item so `bash -c` receives the complete script. A scalar command can lose the connection arguments and produce a login timeout against the initializer container hostname. The password is supplied through `SQLCMDPASSWORD`, rather than inserted into shell code. After updating an older checkout, use the explicit reset commands above only when the local database must be recreated; this clears local test data.

CI checks the actual Compose initializers against SQL Server, including both schemas, the example tenant/client, and both encrypted provider configurations. SQL integration tests also verify seed repetition and decryption with a custom encryption key.

Run the Bandwidth SQL Server integration tests in an isolated local Compose project:

```bash
docker compose -p sms-bandwidth-test --profile tests run --build --rm webhook-tests
docker compose -p sms-bandwidth-test --profile tests down
```

This starts only SQL Server, database initialization and the test runner. It recreates the test databases using the canonical schemas and verifies concurrent callback retries, status history, tenant isolation and encrypted provider settings. Host port 1434 must be available. No live Bandwidth account is needed. Configure public HTTPS callbacks separately when testing with a real provider account.

## Database initialization

This project does not use migrations. Treat every target database as new and apply the complete `database/schema.sql` script once during provisioning. The example tenant seed is for Docker-based tests only and must not be executed in production.

## Authentication

Client login attempts are audited in `SmsApiLogs.dbo.UserActivityLogs`, including validation failures, rejected credentials, rate limits and server errors. Successful logins record the verified `ClientId` and `TenantId`; failed attempts remain support-only, without a tenant or caller-supplied identifier. Authenticated HTTP activity includes the client identifier from the validated JWT subject, which is visible in the activity log message. Neither secrets nor tokens are logged. Login events use the existing batched exporter and its delivery limitations.

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
ConnectionStrings__LogsSqlServer
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

## Administrator provisioning

Outside local Compose, initialize a new database from the canonical schema, then provide `ConnectionStrings__SqlServer`, `Admin__Username`, and `Admin__Password` securely in the provisioning process environment and run:

```bash
dotnet run --project tools/Sms.Provision -- --admin
```

The command creates one administrator with a random password salt. It refuses duplicate usernames and never prints the password. Remove the provisioning password from the process environment afterward. No administrator is created automatically by the API, and it has no production fallback password.

This update adds `PlatformAdministrators` to the complete schema. Following the project's no-migrations rule, an older local test database must be recreated with `docker compose down --remove-orphans` followed by `docker compose up -d --build`. **This clears local test data.** Merely rebuilding the API does not create the new table. The existing `provider-init` command requires the updated schema.

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

The CI workflow builds the solution, runs tests, generates Cobertura coverage and enforces a minimum 80% line-coverage threshold. SQL Server/Dapper persistence adapters are reserved for integration testing and excluded from the unit-test coverage calculation. Coverage reports are uploaded as workflow artifacts. A separate required-to-pass `bandwidth-sql` job initializes SQL Server with the canonical schema and exercises Bandwidth persistence. Locally, SQL tests are skipped unless `SMS_TEST_SQLSERVER` points to a disposable database initialized with `database/schema.sql`; the Compose test profile supplies it automatically.

## Security notes

- Tenant isolation is derived from authenticated claims.
- Customer-visible logs are isolated by the authenticated tenant claim and stored in a separate database.
- Log events must never contain SMS bodies, authorization headers, credentials, tokens, or phone numbers.
- Provider API secrets are encrypted at rest.
- SMS sender, recipient and body are encrypted at rest with tenant-derived AES-256-GCM keys and authenticated field binding.
- API client secrets use PBKDF2-SHA256.
- Secret/signature comparisons use fixed-time comparison where applicable.
- Twilio error response bodies are not propagated to API callers.
- Development Docker secrets must never be reused in production.
- Docker Compose and the example tenant are for testing only.
- HTTPS termination is required outside local development.

## Current limitations

- Administrator MFA and self-service password recovery are not implemented. Authenticated platform administrators can manage accounts through the console; command-line provisioning remains available for bootstrap access.

## Contributing

Keep changes focused enough for one pull request. New providers must implement the common provider contract, preserve tenant isolation, include tests, keep coverage at or above 80%, and update this README and Docker configuration when behavior or deployment requirements change.
