---
name: runtime-resource-performance
description: Statically review this SMS API for opportunities to reduce CPU usage, memory allocations, memory retention, and resource pressure. Use when investigating high CPU or memory usage, reviewing hot paths, or looking for low-risk runtime efficiency improvements; do not benchmark or execute the application unless explicitly requested.
---

# Runtime Resource Performance

Review the repository for CPU and memory efficiency risks in the ASP.NET Core API, application services, Dapper infrastructure, background services, provider integrations, observability pipeline, and Angular UI when relevant. Prefer small, measurable, maintainable improvements and preserve correctness, security, tenant isolation, and readability.

## Safety and scope

- Read applicable `AGENTS.md` files before acting.
- A review request is read-only. Modify files only when the user explicitly asks for implementation.
- By default, perform static analysis only: do not run the application, tests, benchmarks, profilers, load tests, Docker Compose, or database queries.
- Do not claim measured CPU, allocation, GC, latency, or throughput improvements from static analysis.
- Never expose secrets, tokens, credentials, SMS contents, phone numbers, encryption keys, or customer data.
- Do not trade tenant isolation, authorization, webhook validation, encryption, idempotency, reliability, or observability correctness for performance.
- Avoid speculative micro-optimizations. Prioritize code paths that can plausibly execute frequently, process many records, allocate large objects, retain objects for long periods, or perform repeated work.
- Prefer simple changes over new caching layers, pools, custom allocators, or abstractions unless repository evidence clearly justifies them.

## Review method

1. Identify likely hot paths: HTTP middleware/controllers, message send/receive flows, provider callbacks, bulk and scheduled processing, alert evaluation, reporting/history queries, logging/telemetry, serialization, encryption, and background workers.
2. Trace each candidate through its callers and dependencies before reporting it.
3. Determine whether the issue is primarily CPU, allocation rate, retained memory, I/O amplification, concurrency, or resource lifetime.
4. Check whether the framework or library already manages the resource efficiently before suggesting custom pooling or caching.
5. Prefer improvements with a clear mechanism and bounded behavioral risk.
6. Separate confirmed code patterns from hypotheses that require runtime measurement.

## CPU checklist

Inspect for:

- repeated parsing, formatting, encryption/decryption, hashing, serialization, regex work, reflection, or mapping inside loops;
- repeated LINQ enumeration, unnecessary `ToList`/`ToArray`, nested scans, avoidable sorting, grouping, or dictionary reconstruction;
- synchronous blocking over async work, `.Result`, `.Wait()`, unnecessary `Task.Run`, or excessive parallelism;
- busy loops, aggressive polling, retry loops without appropriate delay/backoff, or background workers that wake unnecessarily;
- duplicate database/API calls or N+1 patterns;
- expensive work performed before authentication, authorization, tenant filtering, validation, or cheap rejection checks;
- unbounded batch processing or concurrency that can saturate CPU;
- excessive logging/telemetry enrichment or serialization on high-volume paths;
- repeated service/provider resolution or configuration transformation that could safely be reused;
- Angular change-detection, subscription, rendering, or repeated transformation patterns when UI CPU is in scope.

## Memory checklist

Inspect for:

- unbounded collections, queues, channels, caches, dictionaries, static state, or in-memory history;
- loading complete result sets when pagination, streaming, or bounded batches are appropriate;
- large temporary strings, byte arrays, JSON documents, CSV buffers, or repeated copies;
- avoidable materialization and intermediate LINQ collections;
- large object graph retention through singleton/background services, closures, events, timers, or subscriptions;
- `IDisposable`/`IAsyncDisposable` resources whose lifetime is incorrect, including streams, HTTP responses, database connections/readers, cancellation registrations, and timers;
- creating `HttpClient` or other expensive transport objects per operation instead of using the configured factory/lifetime;
- caches without explicit bounds, expiration, tenant-safe keys, or invalidation;
- channels/queues without capacity or backpressure;
- background work retaining request-scoped services or payloads;
- reading large uploads, bulk-send files, or provider payloads fully into memory when bounded/streamed processing is practical;
- Angular subscriptions or retained component state that can survive component destruction.

## .NET-specific checks

- Respect dependency-injection lifetimes; do not capture scoped services in singletons.
- Prefer async I/O and propagate `CancellationToken`.
- Use Dapper buffered results only when the expected result set is bounded and materialization is useful.
- Check pagination and configured maximum page/batch sizes before recommending streaming.
- Treat `ArrayPool<T>`, object pooling, `Span<T>`, `Memory<T>`, custom serializers, and manual buffer management as advanced optimizations requiring strong evidence.
- Consider GC pressure from high-frequency allocations, but do not recommend allocation tricks that materially reduce readability without evidence.
- Check cancellation, timeout, retry, and concurrency limits for external provider calls and hosted services.

## Observability checks

- Ensure performance-related logs do not introduce high-volume allocation or serialization overhead.
- Prefer structured logging templates over preformatted/interpolated strings on frequently executed paths.
- Check that error-only system logging and user activity logging remain consistent with project requirements.
- Do not recommend removing required traces, metrics, audit events, or security logs solely to reduce CPU or memory.
- When runtime validation is requested, prefer existing OpenTelemetry/runtime metrics before adding custom instrumentation.

## Prioritization

Classify findings by likely impact:

- **High** — unbounded growth, resource leak/lifetime bug, busy loop, uncontrolled concurrency, repeated expensive work on a high-volume path, or full materialization of potentially large data.
- **Medium** — recurring avoidable allocation/CPU work with credible frequency or scale.
- **Low** — localized inefficiency with limited expected effect.
- **Not actionable** — theoretical micro-optimization without enough evidence.

Also assign confidence as `High`, `Medium`, or `Low` based on repository evidence.

## Required report

Lead with a concise overall assessment and explicitly state whether the review is static or backed by runtime measurements.

Include:

1. **Scope** — projects, files, and execution paths reviewed.
2. **Findings** — ordered by likely impact; include file references, CPU/memory mechanism, impact classification, and confidence.
3. **Recommended changes** — smallest safe implementation for each actionable finding, including tradeoffs.
4. **Safety/correctness constraints** — tenant isolation, security, disposal, concurrency, ordering, retries, and behavior that must remain unchanged.
5. **Runtime validation** — only when useful, describe what should be measured to confirm the hypothesis (for example allocation rate, GC heap size, Gen 2 collections, thread-pool queue length, process CPU, working set, request rate, or queue depth). Do not fabricate values.
6. **Limitations** — identify findings that cannot be confirmed without profiling or load data.

If no meaningful CPU or memory issue is visible, say so directly. Do not invent recommendations to populate the report.
