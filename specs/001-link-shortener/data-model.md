# Data Model: Encurtador de links

## ShortLink

Represents the durable association between a short code and its destination.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `Id` | UUID | Yes | Persistence identity; generated when a link is created. |
| `Code` | String (4–20 characters) | Yes | Canonical uppercase ASCII alphanumeric; excludes `0`, `1`, `I`, `L`, and `O` regardless of input case; globally unique with a database unique index. Automatically generated codes have exactly 7 characters. |
| `DestinationUrl` | String (maximum 2048 characters) | Yes | Must be a valid absolute URL using `http` or `https`. Immutable after creation. |
| `ExpiresAtUtc` | UTC date/time | No | Null means no expiration. A link is expired when current UTC time is greater than or equal to this value. |
| `ClickCount` | Signed 64-bit integer | Yes | Starts at zero, never decreases, and increments atomically once for each successful redirect. |

### Code Canonicalization

- Accept letters without regard to case; normalize to uppercase before service checks, reads, and persistence.
- The canonical alphabet is `23456789ABCDEFGHJKMNPQRSTUVWXYZ` (31 characters).
- The unique database index is on the canonical `Code`.
- The application checks for duplicates to provide a useful conflict response, but the database index handles concurrent creation races.

### State and Transitions

- **Active**: `ExpiresAtUtc` is null or current UTC time is earlier than `ExpiresAtUtc`.
- **Expired**: `ExpiresAtUtc` is set and current UTC time is greater than or equal to it. Expired links remain stored and their statistics remain queryable.
- **Creation**: A valid request creates a link with `ClickCount = 0`; a past expiration is rejected.
- **Successful access**: A single conditional persistence update verifies the code and active state, then increments `ClickCount` by one. The service redirects only after the update reports one affected row.
- **Expired access**: No increment occurs; the user receives an expired response rather than a redirect.
- Destination changes and link deletion are not part of this feature.

## Relationships

No additional business entities or relationships are required. Click events are represented only by the monotonic aggregate `ClickCount`; individual access records, deduplication, and geographic metadata are out of scope.

## Persistence Constraints

- Persist links across application restarts using the SQLite database file.
- Unique index on canonical `Code`.
- Non-negative `ClickCount`; do not wrap or decrease at signed 64-bit maximum.
- Keep provider-specific schema/migrations in Infrastructure. No management key or management-key hash is stored.
