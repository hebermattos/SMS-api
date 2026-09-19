---
name: sql-query-performance
description: Statically review the text of SQL Server queries in this SMS API and suggest performance improvements. Use when reviewing Dapper SQL, investigating potential query inefficiencies, or checking query text for performance risks; do not execute queries or require database access.
---

# SQL Query Performance

Review queries under `src/Sms.Infrastructure/Sql` by inspecting their text, callers, and relevant schema definitions. Identify likely performance risks and suggest the smallest safe improvements while preserving behavior, tenant isolation, and readability.

## Safety and scope

- Read applicable `AGENTS.md` files before acting.
- Do not connect to a database, execute queries, collect runtime metrics, or generate execution plans as part of this skill.
- Do not print connection strings, credentials, encryption keys, message contents, phone numbers, tokens, or other sensitive values.
- Keep tenant predicates, authorization boundaries, encryption behavior, result ordering, and pagination semantics unchanged.
- Do not present static findings as measured performance facts.
- Do not add indexes, hints, isolation-level changes, or rewrite queries unless the user explicitly asks for implementation.
- A performance-review request is read-only. Modify files only when the user also asks for changes.

## Locate the real query

1. Identify the `.sql` resource and every C# caller that loads it through `SqlQuery`.
2. Trace Dapper parameters, transaction context, result mapping, expected cardinality, and whether input remains parameterized.
3. Inspect the relevant tables, indexes, constraints, and column types in `database/schema.sql` or `database/logs-schema.sql`.
4. Confirm which database owns the query.
5. When a query is assembled from multiple trusted SQL resources, review the final logical statement and how the caller combines the fragments.

## Static review checklist

Inspect the query text for:

- missing or late `TenantId` predicates;
- `SELECT *`, unnecessarily wide projections, or decrypted data that the caller does not need;
- non-sargable predicates, functions applied to filtered columns, leading-wildcard searches, and avoidable implicit conversions;
- predicates whose parameter and column types do not match;
- joins without clear keys, accidental many-to-many expansion, correlated subqueries, repeated scalar subqueries, or likely N+1 access in the caller;
- `OR`, `IN`, `NOT IN`, `DISTINCT`, `UNION`, window functions, and aggregations whose shape may create unnecessary scans, sorts, or duplicate work;
- pagination that orders or scans an unbounded tenant data set, unstable ordering, or large-offset pagination;
- unnecessary `ORDER BY`, redundant joins, repeated expressions, and avoidable round trips;
- unsafe dynamic SQL or values that should be Dapper parameters;
- index key order that appears inconsistent with equality predicates, range predicates, joins, and ordering;
- aggregations over raw history when an existing summary table is intended for that workload.

Account for correctness before suggesting changes:

- preserve returned columns, null behavior, ordering, pagination, and transaction semantics;
- preserve every tenant and authorization boundary;
- consider the write and storage cost of a suggested index;
- avoid hints and vendor-specific workarounds unless the query text shows a concrete need and the tradeoff is explained;
- label findings as `High`, `Medium`, or `Low` confidence based only on evidence visible in the repository.

## Required report

Lead with a concise overall assessment. State explicitly that the review is static and does not prove runtime performance.

Include:

1. **Scope** — query files, callers, and relevant schema objects.
2. **Findings** — ordered by likely impact, with file references, the problematic SQL shape, and why it may be expensive.
3. **Suggestions** — concrete revised SQL or index shape when justified by repository evidence, including tradeoffs.
4. **Correctness and isolation** — behavior and tenant constraints that each suggestion must preserve.
5. **Limitations** — uncertainties that cannot be resolved from query text and schema alone.

If no meaningful issue is visible, say so directly. Do not invent recommendations merely to populate the report, and do not produce diagnostic scripts unless the user separately requests them.
