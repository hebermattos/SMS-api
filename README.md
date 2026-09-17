# SMS API

Multi-tenant REST API for sending, receiving, tracking, and querying SMS messages through multiple providers.

## Stack

- ASP.NET Core / .NET 8
- SQL Server
- Dapper
- JWT authentication
- Twilio
- Bandwidth

## Core capabilities

- Multi-tenant isolation
- Send SMS
- Receive inbound SMS through provider webhooks
- Delivery/status callbacks
- SMS history
- Provider abstraction so additional SMS providers can be added without changing the API contract
- JWT authentication

## Planned architecture

```text
src/
  Sms.Api/             HTTP endpoints, authentication and middleware
  Sms.Application/     Use cases and application contracts
  Sms.Domain/          Domain models and provider-independent rules
  Sms.Infrastructure/  SQL Server/Dapper and SMS provider integrations
```

The API will keep provider-specific payloads and SDKs inside the infrastructure layer. Application code will communicate through provider-independent interfaces, allowing Twilio, Bandwidth, and future providers to coexist.

## Initial API surface

```text
POST /api/v1/auth/token
POST /api/v1/messages
GET  /api/v1/messages
GET  /api/v1/messages/{id}
POST /api/v1/webhooks/twilio/inbound
POST /api/v1/webhooks/twilio/status
POST /api/v1/webhooks/bandwidth/inbound
POST /api/v1/webhooks/bandwidth/status
```

## Status

Initial project structure is being implemented.
