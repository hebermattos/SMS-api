---
name: sql-query-performance
description: Measure and compare SQL Server query performance for this SMS API using repeatable STATISTICS IO/TIME results and execution plans. Use when reviewing a Dapper query, investigating a slow database operation, checking a SQL performance regression, or validating an optimization; do not use for general code review without a query-performance question.
---

# SQL Query Performance

Produce an evidence-backed performance report for queries under `src/Sms.Infrastructure/Sql`. Measure before recommending changes, preserve tenant isolation and correctness, and prefer the smallest improvement supported by the results.

## Safety and scope

- Read applicable `AGENTS.md` files before acting.
- Never run diagnostic queries against production unless the user explicitly authorizes the exact database and operation.
- Prefer a disposable local or integration SQL Server populated with representative, non-sensitive data.
- Do not print connection strings, credentials, encryption keys, message contents, phone numbers, tokens, or other sensitive values.
- Use parameterized execution and realistic, sanitized parameter values. Never concatenate untrusted values into a test script.
- Treat query plans and SQL output as potentially sensitive because literals and object names may be embedded in them.
- Keep tenant predicates, authorization boundaries, encryption behavior, result ordering, and pagination semantics unchanged.
- Do not add indexes, hints, isolation-level changes, cached-plan workarounds, or query rewrites without evidence and an explicit request to implement a fix.
- A performance-review request is read-only. Modify files only when the user also asks for changes.

## Locate the real query

1. Identify the `.sql` resource and every C# caller that loads it through `SqlQuery`.
2. Trace Dapper parameters, command timeout, transaction context, expected cardinality, and result mapping.
3. Inspect the relevant tables, indexes, constraints, and estimated data distribution in `database/schema.sql` or `database/logs-schema.sql`.
4. Confirm which database owns the query. Application and observability queries must be measured against their respective schemas.
5. When a query is assembled from multiple trusted SQL resources, measure the final SQL text executed by the caller, not an isolated fragment.

## Establish a repeatable baseline

Record enough context to reproduce the result:

- query file and caller;
- SQL Server version and compatibility level;
- row counts and relevant value distribution;
- parameter set and whether it represents a typical or worst-case request;
- existing indexes;
- warm or cold cache state;
- number of measured runs.

Use at least one warm-up execution and three measured executions when the environment permits. Do not clear the server-wide plan cache or buffer pool on a shared server. If cold-cache behavior matters, use a disposable isolated database and state exactly how the cache was controlled.

Wrap only the diagnostic session with:

```sql
SET NOCOUNT ON;
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

-- Parameter declarations with sanitized representative values.
-- Exact project query text.

SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
```

Capture an actual execution plan only in an authorized non-production environment. Otherwise use an estimated plan and disclose that limitation. Do not include `GO` inside SQL sent through Dapper.

## Analyze the evidence

Report, per statement and important table:

- logical and physical reads;
- scan count;
- CPU time and elapsed time;
- returned row count;
- estimated versus actual rows when an actual plan is available;
- scans, seeks, lookups, sorts, hashes, spills, warnings, implicit conversions, and memory grants;
- blocking or waits when observed separately from query cost.

Do not label a query slow from elapsed time alone. Separate database work from blocking, compilation, network transfer, connection setup, and client-side mapping. Treat SQL Server's missing-index suggestions as hypotheses, not instructions.

Check common project risks:

- a missing or late `TenantId` predicate;
- non-sargable filters or implicit conversions;
- pagination that sorts or scans an unbounded tenant data set;
- repeated queries or N+1 access in the caller;
- wide projections or unnecessary decrypted data;
- parameter-sensitive plans and skewed tenant/provider/status distributions;
- aggregations over raw history when an existing summary table should be used;
- indexes whose key order does not support equality predicates, range predicates, and ordering in the measured query.

## Compare a proposed improvement

Use the same schema, representative data, parameters, cache state, and run count for baseline and candidate. Verify identical results before comparing performance. Prefer medians; include ranges when runs vary materially.

Calculate changes as:

```text
change % = ((candidate - baseline) / baseline) * 100
```

An optimization is supported only when it preserves behavior and improves a relevant measured metric without an unacceptable tradeoff. For a new or changed index, also discuss storage and write costs for SMS inserts, status callbacks, outbox/inbox operations, and observability ingestion.

## Required report

Lead with the verdict: `Regression`, `Improvement`, `No material change`, or `Not measurable with current evidence`.

Include:

1. **Scope** — query files, callers, database, parameters, and data profile.
2. **Method** — environment, cache state, run count, and plan type.
3. **Results** — baseline and candidate medians for logical reads, CPU, elapsed time, and rows returned; include per-table reads where useful.
4. **Plan findings** — the operators or estimate errors that explain the result.
5. **Correctness and isolation** — result equivalence and tenant filtering.
6. **Recommendation** — smallest evidence-supported next action, with tradeoffs.
7. **Limitations** — missing production-scale data, actual plan, concurrency test, or other uncertainty.

Never claim an improvement from static inspection alone. If execution is unavailable, provide a static-risk assessment and a ready-to-run diagnostic script, clearly marked as unmeasured.
