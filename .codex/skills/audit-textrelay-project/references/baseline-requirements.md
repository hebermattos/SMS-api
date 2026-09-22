# TextRelay baseline requirements

Use this checklist when auditing the TextRelay project. The user's current explicit instructions and applicable repository instruction files take precedence over this snapshot.

## Architecture and maintainability

- REST API uses C# and ASP.NET Core.
- PostgreSQL is the application database and Dapper is the data-access library.
- Administration UI uses Angular.
- Code favors simplicity, readability, maintainability, and necessary abstractions only.
- Database migrations are not used; a new database is initialized from scripts.
- `README.md` accurately describes the current setup and behavior.

## Providers and messaging

- Twilio and Bandwidth are supported initially.
- The caller selects the provider through an API request parameter.
- Every provider implements the same generic interface and contract.
- Provider-specific logic is isolated from core business logic.
- New providers can be added without modifying unrelated core behavior.
- The system sends SMS, receives inbound SMS, processes callbacks/delivery status, and exposes authenticated SMS history.
- RabbitMQ messages carry SMS identity rather than the SMS payload; the Worker loads the message and atomically claims it before provider delivery.
- Queue lifecycle state and provider/delivery state remain separate.
- Failed RabbitMQ publication has persisted recovery, and consumers remain idempotent under at-least-once delivery.

## Multi-tenancy and security

- Tenant data isolation is enforced in authentication/authorization and every relevant query or mutation.
- Every protected operation validates authorization and tenant ownership.
- JWT authentication is implemented securely.
- Incoming provider webhooks are authenticated or signature-validated whenever the provider supports it.
- Credentials, tokens, secrets, and unnecessary sensitive customer data are absent from source, responses, and logs.
- Security-relevant configuration fails safely rather than silently weakening protection.

## Tests and CI

- Unit-test coverage is at least 80%, supported by a current coverage result.
- CI builds, tests, and enforces the coverage threshold.
- CI runs only on `main` after merge, not on feature branches or pull requests.
- Each pull request/change set is cohesive, substantial, and does not mix unrelated work.

## Docker and initialization

- Docker Compose is current and usable.
- First startup creates the application database, separate audit/error-log database, and separate reporting database.
- Initialization creates the required schemas and a configured sample tenant.
- Provider test configuration or safe fake data is initialized where required by current project instructions.
- Initialization is idempotent whenever practical and waits for PostgreSQL readiness reliably.

## Logging and observability

- OpenTelemetry covers logs, traces, and metrics.
- Tenant activity and platform error logs are stored in the dedicated PostgreSQL audit/error-log database.
- Technical OpenTelemetry logs, traces, and metrics are exported to ClickStack/ClickHouse rather than PostgreSQL.
- API and Worker telemetry use distinct service identities and per-process instance IDs.
- SMS tracing preserves producer, consumer, atomic-claim, and provider-client boundaries.
- Metric labels remain low-cardinality; tenant IDs, message IDs, phone numbers, and similar identifiers are not metric dimensions.
- Customers can view only their own logs; platform users can access system logs according to authorization.
- System logs contain only error level or higher and are intended only for platform users.
- User logs record customer user activities, including login activity, with tenant identity and appropriate actor/context.
- Logs do not expose secrets, credentials, access tokens, message content unnecessarily, or avoidable personal information.
- Log queries and UI enforce tenant isolation rather than relying only on client-side filtering.

## Administration UI

- Angular provides platform administration and customer-facing administration appropriate to their roles.
- Platform authentication uses username and password as currently required.
- Customer login, session restoration after page refresh, provider selection, user management, and log viewing match current requirements.
- UI authorization is backed by server-side enforcement; route guards alone are insufficient.
