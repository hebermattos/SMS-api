---
name: simple-maintainable-code
description: Review, write, refactor, or improve code with simplicity, readability, and maintainability as the highest priorities. Use for code reviews, implementation, refactoring, bug fixes, test design, and architecture decisions in this project, especially when choosing between a straightforward solution and abstractions, optimizations, or patterns.
---

# Simple and Maintainable Code

Apply this skill whenever changing or reviewing project code.

## Priorities

Use this order when making decisions:

1. Correctness and security.
2. Simplicity and readability.
3. Maintainability and testability.
4. Performance when evidence shows it matters.
5. Abstraction, extensibility, and cleverness only when they reduce real complexity.

Prefer code that a competent developer can understand quickly without reconstructing hidden behavior.

## Workflow

1. Understand the existing behavior and constraints before changing code.
2. Make the smallest cohesive change that solves the problem.
3. Keep the main path obvious and linear.
4. Reuse existing project conventions before introducing a new pattern.
5. Add or update focused tests for behavior and important edge cases.
6. Review the final diff for unnecessary complexity, duplication, and unrelated changes.

## Rules

- Prefer descriptive names and small methods with one clear responsibility.
- Prefer explicit control flow over dense expressions, clever shortcuts, or deeply nested conditionals.
- Keep abstractions justified by repeated behavior or a clear boundary; do not abstract speculation.
- Avoid introducing a framework, pattern, helper, or dependency for a single simple use.
- Keep data flow and error handling visible at the call site.
- Avoid hidden side effects, global mutable state, magic values, and unexplained configuration.
- Do not optimize without evidence from measurements, profiling, or a known bottleneck.
- Prefer existing language and framework features when they make the code clearer.
- Keep public APIs and contracts minimal and explicit.
- Preserve security, tenant isolation, validation, and authorization even when simplifying code.
- Do not weaken tests, coverage thresholds, validation, or security to make a change easier.
- Keep comments for decisions and non-obvious constraints; do not comment code that is already self-explanatory.
- Treat readability and maintainability as valid reasons to reject a technically shorter solution.

## Review checklist

Before finishing, verify:

- Can the behavior be explained in a few sentences?
- Are names, conditions, and boundaries clear?
- Is there a simpler implementation with the same behavior?
- Does each abstraction earn its complexity?
- Are validation, authorization, tenant context, and failure behavior explicit?
- Are tests focused on externally visible behavior?
- Did the change avoid unrelated refactoring?
- Is any performance claim supported by evidence?

## Output format for reviews

Report findings in priority order:

1. Correctness, security, or data-isolation problems.
2. Maintainability and readability problems.
3. Testability and missing coverage.
4. Performance concerns only when supported by evidence.

For each finding, include the location, why it matters, and the smallest clear improvement. If there are no findings, state that clearly and mention any remaining uncertainty.
