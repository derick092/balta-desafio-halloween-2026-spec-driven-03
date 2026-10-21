<!--
Sync Impact Report
Version change: uninitialized -> 1.0.0
Modified principles: Initial establishment; no previously named principles existed.
Added sections: Architectural and Technology Constraints; Delivery Workflow and Quality Gates.
Removed sections: None.
Follow-up TODOs: Confirm the original ratification date.
-->
# Encurtador Constitution

## Core Principles

### I. Application Boundary
HTTP endpoints MUST delegate to application services and MUST NOT access the database
directly. Business rules MUST reside in the application/domain layer, never in an endpoint
or HTTP handler. This separation keeps transport concerns independent from application
behavior and makes business rules directly testable.

### II. Replaceable Persistence
Persistence MUST use Entity Framework Core through replaceable providers. Application
behavior MUST NOT depend on a specific database provider, and SQL MUST NOT be scattered
through application code. This preserves portability and keeps persistence concerns
isolated.

### III. Unit-Tested Business Rules
Every business rule MUST have unit tests using xUnit. Tests MUST exercise the rule through
the application/domain boundary without requiring an HTTP request or a live database.

### IV. Happy-Path Endpoint Integration Tests
Every endpoint MUST have at least one integration test covering a successful request and
response. The test MUST exercise the endpoint through the application's HTTP boundary so
routing, binding, and service integration are covered.

### V. Consistent API Errors and Naming
Validation failures MUST return HTTP 400 with ProblemDetails. Error messages MUST be in
Portuguese; class and file names MUST be in English. These conventions keep the API
predictable for clients and consistent for maintainers.

## Architectural and Technology Constraints

The API MUST use .NET, C#, and Minimal APIs. New dependencies MUST NOT be introduced
unless their purpose and justification are written in the implementation plan before
adoption. Dependencies are architectural decisions and must remain reviewable.

## Delivery Workflow and Quality Gates

Changes MUST include tests required by the Core Principles. Reviews MUST verify endpoint
boundaries, business-rule placement, persistence-provider independence, API error
contracts, naming, and dependency justifications against this constitution.

Commit messages MUST use the format `type(scope): requirement-id description`, where
`requirement-id` identifies the requirement being implemented or changed.

## Governance

This constitution is the governing standard for project design, implementation, and
review. Amendments MUST be proposed as explicit changes to this document, reviewed for
their impact on existing requirements and code, and reflected in the constitution
version and amendment date. Code changes MUST NOT silently override these rules.

The constitution follows semantic versioning. A MAJOR increment is required for
backward-incompatible removals or redefinitions of principles; a MINOR increment is
required for new principles, sections, or materially expanded requirements; a PATCH
increment is required for clarifications and non-semantic wording changes.

Each implementation plan and code review MUST assess compliance with this constitution.
Non-compliance MUST be corrected or resolved by an approved amendment before the
affected change is accepted.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE) | **Last Amended**: 2026-10-21
