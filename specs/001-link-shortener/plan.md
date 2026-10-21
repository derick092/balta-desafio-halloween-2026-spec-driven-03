# Implementation Plan: Encurtador de links

**Branch**: `001-link-shortener` | **Date**: 2026-10-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-link-shortener/spec.md`; planning decisions supplied by the user in this session.

## Summary

Implement a persistent short-link service with .NET 10, C#, Minimal APIs, EF Core and SQLite. Each resource's HTTP routes live in one endpoint file and delegate to the application-layer `LinkService`, which owns validation, code generation, expiration decisions, uniqueness checks, and access behavior. EF Core persistence is isolated behind application-facing interfaces so a later PostgreSQL provider can be selected without moving business rules. A unique database index is the final authority for code uniqueness, while each valid redirect increments its counter through one conditional atomic database update.

The agreed code alphabet excludes visually ambiguous characters (`0`, `1`, `I`, `L`, and `O` regardless of input case); codes are normalized to uppercase. Statistics remain public to anyone who knows the code. No management key is created in this scope because no protected management operation exists.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`).

**Primary Dependencies**: ASP.NET Core Minimal APIs; EF Core 10 SQLite provider. xUnit for tests. `Microsoft.AspNetCore.Mvc.Testing` is a test-only dependency for exercising the actual HTTP boundary. EF Core design-time tooling is used only to create and apply migrations.

**Storage**: SQLite for this release, with EF Core provider selection isolated at application startup. Keep the entity model and application-facing persistence contract within the relational capabilities shared with the intended PostgreSQL provider. PostgreSQL support and its provider/migrations are future work, not part of this release.

**Testing**: xUnit unit tests for application/domain rules; xUnit HTTP integration tests against a temporary SQLite database. Include a file-backed restart test and concurrency tests for uniqueness and click increments.

**Target Platform**: .NET 10 server-side web service on platforms supported by the .NET 10 runtime; initial deployment uses a persistent local SQLite file.

**Project Type**: HTTP web service.

**Performance Goals**: No numeric latency, throughput, or availability target was specified. Tests verify correctness under concurrent requests; SQLite's serialized writes are an accepted initial-provider constraint.

**Constraints**: Business rules reside in `LinkService`, not endpoint handlers. Endpoints call application services and never access the database. Access increments must be a single atomic database update conditioned on the link being active. Codes are unique across the system and case-insensitive after canonicalization. Do not add rate limiting, bot filtering, authentication, or a management key in this version.

**Scale/Scope**: No usage-volume target was specified. Assume a modest initial deployment using SQLite; do not claim horizontal write scalability. The feature includes creation, public redirect, and public access-count lookup only.

## Constitution Check

### Pre-Design Gate

| Principle | Status | Plan alignment |
|---|---|---|
| Application Boundary | PASS | Minimal API handlers delegate to `LinkService`; EF Core access is behind application-facing persistence interfaces. |
| Replaceable Persistence | PASS | EF Core provider configuration is at the host boundary; SQLite is the initial provider and application rules use provider-neutral contracts. |
| Unit-Tested Business Rules | PASS | xUnit unit tests exercise validation, code selection, expiration, conflicts, and access outcomes without HTTP or a live database. |
| Happy-Path Endpoint Integration Tests | PASS | Each endpoint has a successful HTTP-boundary integration test using the test host and SQLite. |
| Consistent API Errors and Naming | PASS | Validation returns HTTP 400 ProblemDetails in Portuguese; endpoint, application, and persistence types use English names. |
| Technology Constraints | PASS | .NET 10, C#, and Minimal APIs are used; dependency additions are justified below. |

No constitution violations require an exception.

## Phase 0: Outline & Research

See [research.md](./research.md) for technology findings and decisions.

Decisions recorded from the user:

1. Use .NET 10, C#, Minimal APIs, and one endpoint file per resource.
2. Use EF Core with SQLite initially and isolate the provider for a later PostgreSQL transition.
3. Put business rules in application-layer `LinkService`.
4. Enforce code uniqueness both in the service and with a database unique index to handle races.
5. Increment access count atomically in the database; never read and rewrite the count in application code.
6. Exclude visually ambiguous characters from generated and custom codes.
7. Do not store a management-key hash: statistics are public by explicit decision and no other management operation is in scope, so such a key would protect nothing.

Dependency justifications:

- `Microsoft.EntityFrameworkCore.Sqlite`: required for the selected persistent SQLite provider.
- `Microsoft.EntityFrameworkCore.Design`: design-time support for provider-specific EF migrations; referenced by Infrastructure and the API startup project so `dotnet ef` can discover the configured context; not used by runtime business logic.
- `SQLitePCLRaw.lib.e_sqlite3`: pin the native SQLite bundle to a patched version because the EF SQLite provider's transitive version has a known security advisory.
- `xunit` and the .NET test SDK: required by the project constitution and the user's testing decision.
- `Microsoft.AspNetCore.Mvc.Testing`: test-only HTTP host for endpoint integration tests; no production runtime dependency.
- No PostgreSQL provider is added in this release; add and verify a compatible provider version when the provider transition is scheduled.

## Phase 1: Design & Contracts

### Project Structure

```text
src/
├── LinkShortener.Api/
│   ├── Endpoints/
│   │   └── LinkEndpoints.cs
│   ├── Configuration/
│   └── Program.cs
├── LinkShortener.Application/
│   └── Links/
│       ├── LinkService.cs
│       ├── ILinkRepository.cs
│       ├── LinkRequests.cs
│       └── ShortLink.cs
└── LinkShortener.Infrastructure/
    └── Persistence/
        ├── LinkDbContext.cs
        ├── EfLinkRepository.cs
        └── SqliteMigrations/
tests/
├── LinkShortener.Application.Tests/
└── LinkShortener.Api.IntegrationTests/
specs/001-link-shortener/
├── contracts/
│   └── openapi.yaml
├── data-model.md
├── plan.md
├── quickstart.md
└── research.md
```

**Structure Decision**: Use three production projects to keep HTTP, business behavior, and provider-specific persistence separated without adding an independent domain project. Store link rules and the `LinkService` in the application project. Keep all routes for this resource in `LinkEndpoints.cs`. Separate unit and API integration test projects to match the constitution's test boundaries.

### Design Decisions

- Canonicalize codes to uppercase invariant before checking or storing them. Accept only ASCII alphanumeric characters other than `0`, `1`, `I`, `L`, and `O`; the canonical generated alphabet is `23456789ABCDEFGHJKMNPQRSTUVWXYZ`.
- Select generated characters with the platform cryptographic random-number generator using unbiased bounded selection; no extra random-number package is needed.
- `LinkService` performs an early repository existence check for useful custom-code errors. The unique index on canonical `Code` remains authoritative; persistence translates a unique-constraint failure into a duplicate-code result. For generated-code collisions, retry with a newly generated code a bounded number of times; fail explicitly if retries are exhausted.
- Store expiration as UTC `DateTime` in the provider-neutral model. Accept ISO 8601 timestamps with offsets at the HTTP boundary and normalize to UTC, avoiding SQLite `DateTimeOffset` ordering limitations.
- Use the built-in `TimeProvider` abstraction in `LinkService` for expiration decisions and deterministic boundary tests.
- Resolve the public origin from required host configuration to build returned short URLs; this is one canonical origin, not a custom-domain feature.
- Read only a projection of the immutable destination and expiry needed for the redirect; never retrieve `ClickCount`. Then count a click with one conditional `ExecuteUpdateAsync` equivalent in infrastructure: increment the current database value only when the canonical code matches, `ExpiresAtUtc` is null or greater than the captured UTC instant, and `ClickCount` is below its maximum. Only redirect if exactly one row was incremented.
- Configure a five-second SQLite busy timeout so serialized concurrent writes can complete. Retry only recognized transient SQLite busy/locked failures at most three times, with 25 ms, 50 ms, and 100 ms delays after timeout; after exhaustion surface an explicit persistence-unavailable error (HTTP 503) and do not redirect. Never retry a permanent database failure or report a successful redirect without a confirmed increment.
- Use a signed 64-bit non-negative count. If it reaches its representable maximum, fail the access without redirecting or wrapping/decreasing the counter; return a server ProblemDetails response and log the operational failure.
- No management-key hash or secret is persisted because the approved public statistics flow does not require one and no private management operation is defined.

### Error and HTTP Behavior

- All error responses use ProblemDetails with Portuguese messages, as required by the constitution.
- Create validation errors: HTTP 400.
- Custom-code conflict: HTTP 409; the existing link is unchanged.
- Unknown code: HTTP 404.
- Expired link: HTTP 410 and no click increment.
- Active redirect: HTTP 302 Found after a successful atomic increment.
- If generated-code retry attempts are exhausted, click-counter capacity is exhausted, or transient persistence retries are exhausted, return HTTP 503 ProblemDetails; never return a link that was not persisted or a redirect that was not counted. Other unexpected persistence errors return a logged HTTP 500 ProblemDetails.
- The public count endpoint remains available for expired links and reports the accumulated count.

### Post-Design Constitution Check

| Principle | Status | Evidence in design |
|---|---|---|
| Application Boundary | PASS | `LinkEndpoints.cs` maps requests to `LinkService`; database operations remain in `EfLinkRepository`. |
| Replaceable Persistence | PASS | Application uses `ILinkRepository`; provider setup and SQLite-specific migrations reside in Infrastructure/host composition. |
| Unit-Tested Business Rules | PASS | `LinkShortener.Application.Tests` tests the service rules without HTTP/database dependencies. |
| Happy-Path Endpoint Integration Tests | PASS | `LinkShortener.Api.IntegrationTests` covers each successful route through the HTTP boundary. |
| Consistent API Errors and Naming | PASS | Contract uses ProblemDetails and Portuguese messages; code identifiers and filenames are English. |
| Technology Constraints | PASS | Design uses .NET 10, C#, Minimal APIs; each additional dependency and its purpose is listed above. |

No constitution violations remain.

## Generated Artifacts

- [research.md](./research.md) — technology decisions and verified references.
- [data-model.md](./data-model.md) — link attributes, validation, and lifecycle.
- [contracts/openapi.yaml](./contracts/openapi.yaml) — HTTP contract for create, redirect, and statistics.
- [quickstart.md](./quickstart.md) — validation scenarios and expected outcomes.

Implementation tasks are intentionally left for `/speckit-tasks`.

## Complexity Tracking

No constitution violations or unjustified complexity were identified.
