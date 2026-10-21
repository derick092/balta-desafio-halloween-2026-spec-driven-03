# Research: Encurtador de links

**Date**: 2026-10-21  
**Scope**: .NET 10 Minimal APIs, EF Core with SQLite initially, provider portability, uniqueness, and atomic click counting.

## .NET 10 and Minimal APIs

**Decision**: Target `net10.0` and keep route handlers thin, mapping HTTP requests to application services.

**Rationale**: This matches the user's explicit platform choice and the constitution's requirement to keep HTTP handling separate from business rules. .NET 10 is an LTS release; the official support policy lists support through November 14, 2028.

**Alternatives considered**: Controllers or an older .NET target. Neither matches the requested Minimal API architecture and .NET 10 target.

**References**:
- [.NET and .NET Core support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [ASP.NET Core Minimal APIs, .NET 10](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)

## EF Core Provider Boundary

**Decision**: Put EF Core and provider-specific implementation in Infrastructure; expose persistence operations through an application-facing repository contract. Select `UseSqlite` in the host/composition root for this release.

**Rationale**: This preserves the ability to replace SQLite with PostgreSQL without moving business rules into HTTP or provider-specific code. EF Core contexts are scoped per request/unit of work and are not thread-safe.

**Alternatives considered**: Injecting `DbContext` into `LinkService` would simplify the first version but couple the application layer to EF Core; direct context use is rejected in favor of the stated provider-replacement goal.

**References**:
- [DbContext configuration and lifetime](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [EF Core database providers](https://learn.microsoft.com/en-us/ef/core/providers/)

## SQLite-Compatible Expiration

**Decision**: Store expiration as UTC `DateTime`; accept offset-aware ISO 8601 input at the API boundary and normalize it to UTC before persistence and comparison.

**Rationale**: EF Core's SQLite provider documents limitations for comparison and ordering of `DateTimeOffset`, and recommends `DateTime` with UTC values. A single UTC representation makes expiration predicates portable.

**Alternatives considered**: Persisting arbitrary offsets in `DateTimeOffset` would be more expressive but can require client-side evaluation for SQLite comparisons, undermining the atomic expiry predicate.

**References**:
- [EF Core SQLite provider limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations#query-limitations)

## Code Uniqueness and Collision Handling

**Decision**: Canonicalize each code to uppercase, check for an existing code in `LinkService`, and enforce a unique database index on the canonical code. Translate the provider's unique-constraint failure to a duplicate-code result. Retry generated-code collisions a bounded number of times; return a clear failure if retries are exhausted.

**Rationale**: A service-level existence check alone races under concurrent creation. The database constraint is the final authority. Canonicalization avoids reliance on provider collation differences. The selected canonical alphabet excludes `0`, `1`, `I`, `L`, and `O` regardless of input case and is `23456789ABCDEFGHJKMNPQRSTUVWXYZ`; custom codes use the same allowed characters, normalized to uppercase. Use the built-in cryptographic random-number generator with unbiased bounded selection for automatic codes; no extra random dependency is needed.

**Alternatives considered**: Relying only on the pre-check is unsafe under concurrency. Provider-specific case-insensitive collation is rejected because PostgreSQL and SQLite need not behave identically.

**References**:
- [EF Core index uniqueness](https://learn.microsoft.com/en-us/ef/core/modeling/indexes#index-uniqueness)
- [EF Core concurrency handling](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [System.Security.Cryptography.RandomNumberGenerator](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator?view=net-10.0)

## Atomic Click Counting and Expiry

**Decision**: Implement a single conditional database update that increments the existing `ClickCount` and matches only a link with no expiry or an expiry later than the captured UTC time. Redirect only when one row was affected.

**Rationale**: `ExecuteUpdate` generates an update at the database and allows expressions based on the current stored value. This prevents the lost increments caused by read-modify-write application logic and ensures expired links are not counted. The affected-row count distinguishes successful increments from expired/missing records.

**Alternatives considered**: Reading `ClickCount`, adding one in C#, and saving it can lose concurrent increments. Optimistic concurrency with read/retry adds a read and retry path. A transaction is unnecessary for this single conditional update; use one only if future requirements couple the increment atomically with other database writes.

**References**:
- [ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete#executeupdate)
- [Rows affected and concurrency control](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete#concurrency-control-and-rows-affected)
- [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)

## SQLite Write Contention

**Decision**: Configure a five-second SQLite busy timeout and retry only recognized transient busy/locked failures at most three times, with 25 ms, 50 ms, and 100 ms delays after timeout. If the contention remains after retries, return an explicit HTTP 503 and do not redirect.

**Rationale**: SQLite serializes writes. A busy timeout lets ordinary concurrent click updates wait for the active writer; bounded retries handle transient lock conflicts without hiding permanent database failures. Because the service redirects only after a confirmed successful update, a failed attempt is never counted as a successful redirect.

**Alternatives considered**: Returning a redirect before the database confirms the update can lose counts. Unbounded retries can hang requests and mask operational problems.

**References**:
- [SQLite isolation and write serialization](https://www.sqlite.org/isolation.html)
- [Microsoft.Data.Sqlite connection strings and default timeout](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings)

## Provider Transition and SQLite Operational Limit

**Decision**: Deliver SQLite support only. Keep entity/query features in a common relational subset and maintain provider-specific migration sets if PostgreSQL is added. Verify the Npgsql provider version explicitly supports the selected EF Core 10 version before the transition.

**Rationale**: EF migrations are generated for the active provider. Microsoft recommends separate migration sets for applications supporting multiple providers. SQLite serializes writes, so sustained click traffic can become a contention bottleneck; this is acceptable for the unspecified, modest initial deployment but not a claim of high-write horizontal scalability.

**Alternatives considered**: Adding PostgreSQL now would exceed the explicitly requested initial scope. A provider-neutral schema does not eliminate provider-specific migrations or provider-version compatibility checks.

**References**:
- [EF Core migrations with multiple providers](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers)
- [EF Core provider list and compatibility guidance](https://learn.microsoft.com/en-us/ef/core/providers/)
- [Npgsql EF Core provider](https://www.npgsql.org/efcore/)
- [SQLite isolation and write serialization](https://www.sqlite.org/isolation.html)

## Management Key Decision

**Decision**: Do not create or persist a management key in this release.

**Rationale**: The user confirmed that access counts are public and chose to remove the key because no protected management operation is in scope. Hashing an unused secret would add data and handling requirements without protecting a feature.

**Alternatives considered**: Issuing a per-link secret hash for statistics conflicts with the confirmed public-statistics behavior. Adding deletion or other management operations would expand the approved feature scope.
