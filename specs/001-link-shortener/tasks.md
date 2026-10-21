# Tasks: Encurtador de links

**Input**: Design documents in `specs/001-link-shortener/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, and `contracts/openapi.yaml`

**Tests**: Included because the project constitution requires unit tests for business rules and happy-path integration tests for every endpoint; the plan specifies xUnit.

**Organization**: Tasks are grouped by user story so each increment can be validated independently after its shared prerequisites.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: The task can be performed in parallel because it affects separate files and has no incomplete-task dependency.
- **[Story]**: User-story traceability label; setup, foundational, and polish tasks have no story label.
- Every task includes its concrete project path.

## Phase 1: Setup

**Purpose**: Create the .NET solution and project skeleton described in `plan.md`.

- [X] T001 Create `LinkShortener.sln` and `src/LinkShortener.Api/LinkShortener.Api.csproj`, `src/LinkShortener.Application/LinkShortener.Application.csproj`, `src/LinkShortener.Infrastructure/LinkShortener.Infrastructure.csproj`, `tests/LinkShortener.Application.Tests/LinkShortener.Application.Tests.csproj`, and `tests/LinkShortener.Api.IntegrationTests/LinkShortener.Api.IntegrationTests.csproj` with the .NET 10 templates.
- [X] T002 [P] Set `net10.0`, nullable reference types, implicit usings, and consistent language settings in `Directory.Build.props`.
- [X] T003 [P] Add project references among `src/LinkShortener.Api/LinkShortener.Api.csproj`, `src/LinkShortener.Application/LinkShortener.Application.csproj`, and `src/LinkShortener.Infrastructure/LinkShortener.Infrastructure.csproj`; add EF Core SQLite and Design dependencies to Infrastructure and the Design tooling package to the API startup project.
- [X] T004 [P] Add project references, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.AspNetCore.Mvc.Testing`, xUnit, and the .NET test SDK to `tests/LinkShortener.Application.Tests/LinkShortener.Application.Tests.csproj` and `tests/LinkShortener.Api.IntegrationTests/LinkShortener.Api.IntegrationTests.csproj`.

## Phase 2: Foundational

**Purpose**: Implement persistence and API foundations required by all user stories.

**⚠️ CRITICAL**: Complete this phase before implementing any user story.

- [X] T005 Define `ShortLink` in `src/LinkShortener.Application/Links/ShortLink.cs` with `Id` as a generated UUID; `Code` as canonical uppercase ASCII alphanumeric of 4–20 characters excluding `0`, `1`, `I`, `L`, and `O` (case-insensitive) and exactly 7 characters when generated; `DestinationUrl` as an immutable absolute HTTP or HTTPS URL of at most 2048 characters; nullable UTC `ExpiresAtUtc`; and non-negative signed 64-bit `ClickCount` initialized to zero.
- [X] T006 Define the provider-neutral `ILinkRepository` contract in `src/LinkShortener.Application/Links/ILinkRepository.cs` for code existence, persistence, destination/expiry projection, conditional access increment, and statistics lookup; do not expose `DbContext` or provider exceptions.
- [X] T007 Configure `LinkDbContext` and entity mapping in `src/LinkShortener.Infrastructure/Persistence/LinkDbContext.cs`, including a database unique index on canonical `Code`, check constraints for 4–20 character codes and 2048-character destinations, nullable UTC expiry, and a non-negative 64-bit click count.
- [X] T008 Implement `EfLinkRepository` in `src/LinkShortener.Infrastructure/Persistence/EfLinkRepository.cs` against `ILinkRepository`; translate only recognized unique-code constraint violations to an application duplicate-code result and let unrelated persistence errors surface explicitly.
- [X] T009 Configure SQLite connection selection, persistent database path, a five-second busy timeout, provider-neutral service registrations, and the initial and field-length migrations in `src/LinkShortener.Infrastructure/Persistence/DependencyInjection.cs` and `src/LinkShortener.Infrastructure/Persistence/SqliteMigrations/`.
- [X] T010 Configure Minimal API startup, Portuguese ProblemDetails responses, endpoint registration, required public-origin configuration, and application/infrastructure dependency injection in `src/LinkShortener.Api/Program.cs` and `src/LinkShortener.Api/Configuration/ApiConfiguration.cs`.
- [X] T011 Create reusable API integration-test hosting and an isolated temporary SQLite database per test in `tests/LinkShortener.Api.IntegrationTests/LinkApiFactory.cs`.
- [X] T012 Verify the solution restores and builds with `dotnet build LinkShortener.sln`; correct project references or package configuration only in the affected `.csproj` or `Directory.Build.props` files.

## Phase 3: User Story 1 - Criar e compartilhar um link curto (Priority: P1) 🎯 MVP

**Goal**: Create and return a durable short link with a generated or user-selected code, validated destination, and optional expiration.

**Independent Test**: POST a valid HTTP(S) URL, verify the `201` response includes the full short URL and a seven-character non-ambiguous code, then create with an allowed custom code and verify invalid and duplicate inputs are rejected without altering stored links.

### Tests for User Story 1

- [X] T013 [P] [US1] Add `LinkServiceCreateTests` and `ShortCodeGeneratorTests` in `tests/LinkShortener.Application.Tests/Links/` covering generated code length/alphabet, custom code length boundaries 4 and 20, case normalization, ambiguous-character rejection, URL schemes and 2048/2049-character boundaries, past expiration rejection, duplicate custom codes, and generated-code collision retries.
- [X] T014 [P] [US1] Add create endpoint integration tests in `tests/LinkShortener.Api.IntegrationTests/Links/CreateLinkTests.cs` verifying HTTP 201 response shape, persisted data, HTTP 400 Portuguese ProblemDetails for invalid requests, and HTTP 409 for a duplicate custom code.

### Implementation for User Story 1

- [X] T015 [US1] Implement `ShortCodeGenerator` in `src/LinkShortener.Application/Links/ShortCodeGenerator.cs` using cryptographic random selection from exactly `23456789ABCDEFGHJKMNPQRSTUVWXYZ` without modulo bias to generate seven-character codes.
- [X] T016 [US1] Implement link creation and validation in `src/LinkShortener.Application/Links/LinkService.cs`: accept only absolute HTTP or HTTPS URLs with at most 2048 characters; normalize custom codes to uppercase and require 4–20 characters from the non-ambiguous alphanumeric alphabet; reject past expiration; pre-check custom-code uniqueness; set count to zero; retry generated-code collisions a bounded number of times; and return an explicit error when retries are exhausted.
- [X] T017 [US1] Add create request/response DTOs and `POST /api/links` in `src/LinkShortener.Api/Endpoints/LinkEndpoints.cs`, delegating creation to `LinkService` and returning HTTP 201 with `code`, `shortUrl`, `destinationUrl`, and `expiresAt`.
- [X] T018 [US1] Return HTTP 400 Portuguese ProblemDetails for invalid URL, code, and expiration inputs and HTTP 409 Portuguese ProblemDetails for a custom-code conflict from `src/LinkShortener.Api/Endpoints/LinkEndpoints.cs`.

**Checkpoint**: US1 is independently testable: a valid link is persisted and returned; invalid and duplicate requests do not create or overwrite links.

## Phase 4: User Story 2 - Acessar o destino e contabilizar visitas (Priority: P1)

**Goal**: Redirect through active links and count each successful redirect once, without counting expired or missing links.

**Independent Test**: Follow an active code and verify HTTP 302 plus one increment; verify expired and unknown codes do not redirect or increment; issue concurrent requests and verify no successful increment is lost.

### Tests for User Story 2

- [X] T019 [P] [US2] Add redirect service unit tests in `tests/LinkShortener.Application.Tests/Links/LinkServiceRedirectTests.cs` using `TimeProvider` to cover active, exact-expiry, expired, unknown-code, `Int64.MaxValue`, and concurrent increment outcomes without reading and rewriting `ClickCount` in application code.
- [X] T020 [P] [US2] Add redirect endpoint integration tests in `tests/LinkShortener.Api.IntegrationTests/Links/RedirectLinkTests.cs` verifying HTTP 302 and `Location` for active codes, HTTP 410 for expired codes, HTTP 404 for unknown codes, unchanged counts for unsuccessful redirects, and concurrent access counts.

### Implementation for User Story 2

- [X] T021 [US2] Add a destination-and-expiry-only projection to `EfLinkRepository` in `src/LinkShortener.Infrastructure/Persistence/EfLinkRepository.cs`; do not materialize or expose the `ClickCount` to the application service during redirect handling.
- [X] T022 [US2] Implement the conditional atomic click increment in `src/LinkShortener.Infrastructure/Persistence/EfLinkRepository.cs` as one database update that matches the canonical code, requires `ExpiresAtUtc` to be null or later than the captured UTC time, requires `ClickCount` to be below its maximum, and sets the value to the current stored value plus one; return the affected-row count and never read then rewrite the counter in application code.
- [X] T023 [US2] Implement redirect orchestration in `src/LinkShortener.Application/Links/LinkService.cs` using `TimeProvider`: distinguish unknown from expired codes, increment only active links, return the destination only after exactly one row was incremented, and return an explicit failure without redirect on a database error or counter exhaustion.
- [X] T024 [US2] Add retry handling for only SQLite busy/locked failures in `src/LinkShortener.Infrastructure/Persistence/SqliteTransientRetryPolicy.cs`, using at most three retries with 25 ms, 50 ms, and 100 ms delays after the five-second busy timeout; after retry exhaustion propagate an explicit failure and never redirect.
- [X] T025 [US2] Add `GET /{code}` to the existing resource endpoint file `src/LinkShortener.Api/Endpoints/LinkEndpoints.cs`, returning HTTP 302 only after a successful increment, HTTP 410 for expired links, and HTTP 404 for unknown codes.

**Checkpoint**: US2 is independently verifiable using a persisted link fixture; redirects only happen after a confirmed atomic increment, and expired links never increment.

## Phase 5: User Story 3 - Consultar acessos do link (Priority: P2)

**Goal**: Allow anyone with a link code to inspect its accumulated count, including for expired links.

**Independent Test**: Query a link with zero and nonzero counts, including after expiration, and verify an unknown code returns HTTP 404; no authentication or management key is required.

### Tests for User Story 3

- [X] T026 [P] [US3] Add statistics service unit tests in `tests/LinkShortener.Application.Tests/Links/LinkServiceStatsTests.cs` verifying existing links return their accumulated count including zero and expired links, while unknown codes return not found.
- [X] T027 [P] [US3] Add public statistics endpoint integration tests in `tests/LinkShortener.Api.IntegrationTests/Links/GetLinkStatsTests.cs` verifying HTTP 200 response fields, no authentication or management key requirement, expired-link availability, and HTTP 404 for unknown codes.

### Implementation for User Story 3

- [X] T028 [US3] Implement statistics lookup in `src/LinkShortener.Application/Links/LinkService.cs` and `src/LinkShortener.Infrastructure/Persistence/EfLinkRepository.cs`, returning code, current `ClickCount`, and optional expiry without changing the stored count.
- [X] T029 [US3] Add `GET /api/links/{code}/stats` to `src/LinkShortener.Api/Endpoints/LinkEndpoints.cs`, make it public, normalize the code, return HTTP 200 for existing links even when expired, and return HTTP 404 Portuguese ProblemDetails for an unknown code.

**Checkpoint**: US3 is independently verifiable using seeded links, including expired ones; statistics are public and read-only.

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verify durability, keep the public contract aligned with behavior, and complete the runnable validation path.

- [X] T030 [P] Add a file-backed SQLite restart integration test in `tests/LinkShortener.Api.IntegrationTests/Links/LinkPersistenceTests.cs` that creates a link, records a redirect, recreates the API host using the same database file, and verifies the destination and accumulated count persist.
- [X] T031 [P] Add concurrent creation integration coverage for a custom-code race in `tests/LinkShortener.Api.IntegrationTests/Links/CreateLinkConcurrencyTests.cs`, asserting the unique index allows only one link for the canonical code and the losing request receives a conflict without modifying the winner.
- [X] T032 Align response codes, schemas, code alphabet, public statistics behavior, expiry semantics, and Portuguese ProblemDetails in `specs/001-link-shortener/contracts/openapi.yaml` with the implemented routes and endpoint tests.
- [X] T033 [P] Add SQLite database files and transient test artifacts to the repository `.gitignore` so persistent or test data is not accidentally checked in.
- [X] T034 Run the end-to-end scenarios in `specs/001-link-shortener/quickstart.md` and update that guide only if actual startup settings, expected status codes, or validation steps differ.
- [X] T035 Run `dotnet test LinkShortener.sln` and `dotnet build LinkShortener.sln`; resolve failures in the specific source or test files and confirm all three user-story checkpoints remain green.

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; creates the solution and project files.
- **Foundational (Phase 2)**: Depends on Setup; blocks all user-story implementation.
- **US1 (Phase 3)**: Depends on the application model, repository, database mapping, host configuration, and integration-test factory from Phase 2.
- **US2 (Phase 4)**: Depends on Phase 2 and on the shared `LinkService.cs` and `LinkEndpoints.cs` created in US1; do not implement the shared-file changes before US1 is complete.
- **US3 (Phase 5)**: Depends on Phase 2 and on the shared `LinkService.cs` and `LinkEndpoints.cs` from US1; sequence after US2 to avoid concurrent edits to shared files.
- **Polish (Phase 6)**: Depends on all desired user stories; restart and race tests require creation and redirect flows.

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Phase 2; no dependency on another story. Creates the link data and endpoint foundations that later story operations extend.
- **User Story 2 (P1)**: Starts after US1 because it extends the same `LinkService.cs` and `LinkEndpoints.cs` resource files and requires created links.
- **User Story 3 (P2)**: Starts after US2 to avoid overlapping changes to shared files; its read-only behavior is independently testable with seeded data and does not depend on redirect semantics.

### Parallel Opportunities

- T002, T003, and T004 can run in parallel after T001 creates the project files.
- T005 through T011 are sequenced by their type and registration dependencies; keep the entity, repository contract, mapping, provider setup, host, and test factory in dependency order.
- Within each user-story phase, the unit-test file and endpoint-integration-test file tasks are independent and marked `[P]`; implementers can write them in parallel before the service/endpoint code.
- The tests for each user story can be authored in parallel; implementation begins only after those tests demonstrate the expected initial failures.
- T030, T031, and T033 touch separate files and can run in parallel after their dependent stories are complete.
- User-story phases are intentionally sequenced because each extends the single `LinkService.cs` and the one-endpoint-file-per-resource `LinkEndpoints.cs` required by the plan.

## Parallel Example: User Story 1

```text
After T001:
  Task: T002 Configure shared .NET build settings in Directory.Build.props
  Task: T003 Add production project references and EF Core packages in the production .csproj files
  Task: T004 Add test project references and xUnit packages in the test .csproj files

After Phase 2:
  Task: T013 Write create business-rule tests in tests/LinkShortener.Application.Tests/Links/LinkServiceCreateTests.cs
  Task: T014 Write create HTTP integration tests in tests/LinkShortener.Api.IntegrationTests/Links/CreateLinkTests.cs

After T013 and T014 demonstrate the expected initial failures:
  Task: T015 Implement the code generator in src/LinkShortener.Application/Links/ShortCodeGenerator.cs
```

## Implementation Strategy

### MVP First (User Story 1)

1. Complete Phase 1 and Phase 2 to establish a buildable .NET solution and persistent storage.
2. Complete Phase 3 to create validated links with generated or custom codes.
3. **Stop and validate**: run the US1 unit and HTTP integration tests; verify link creation, persistence, code constraints, and conflict behavior.
4. The shortener's core creation MVP is ready; complete US2 next to make created links usable for redirects.

### Incremental Delivery

1. Setup + Foundation → solution builds with persistent provider and shared test host.
2. Add US1 → independently test link creation and shareable URL.
3. Add US2 → independently test redirect, expiry, and concurrency-safe counts.
4. Add US3 → independently test public read-only statistics.
5. Polish → validate restart durability, uniqueness race behavior, OpenAPI alignment, and quickstart.

## Notes

- Every task follows `- [ ] Tnnn [P?] [USn?] Description with a concrete file path`.
- `[P]` is used only for work in separate files without an unmet dependency.
- User-story test tasks are included because the constitution requires both unit and endpoint integration tests.
- The plan intentionally excludes PostgreSQL provider installation, rate limiting, bot filtering, authentication, management-key storage, destination edits, deletion, and geographic reports.
