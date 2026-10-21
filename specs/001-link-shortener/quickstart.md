# Quickstart: Encurtador de links

This guide validates the create, redirect, public statistics, persistence, expiration, and concurrency behavior after implementation.

## Prerequisites

- .NET 10 SDK.
- A persistent writable directory for the SQLite database file.
- The public origin configured as the one canonical short-link host. For local testing, use `https://localhost:5001`.

## Build and test

From the repository root:

```powershell
dotnet restore
dotnet test
```

Expected result: application unit tests and HTTP integration tests pass. The integration suite uses SQLite rather than EF's in-memory provider so unique indexes and atomic updates are exercised.

## Run locally

Configure the public origin and persistent database file, then start the API:

```powershell
$env:ShortLinks__PublicOrigin = "https://localhost:5001"
$env:ConnectionStrings__ShortLinks = "Data Source=shortlinks.db"
dotnet run --project src/LinkShortener.Api --urls https://localhost:5001
```

Expected result: the API starts using the SQLite file `shortlinks.db`; restarting the process with the same connection string retains links and counts.

## Create, redirect, and inspect statistics

Create a link with a generated code:

```powershell
$created = Invoke-RestMethod `
  -Method Post `
  -Uri "https://localhost:5001/api/links" `
  -ContentType "application/json" `
  -Body '{"destinationUrl":"https://example.com/articles/long-path"}'
$created
```

Expected result: HTTP 201, a seven-character code from the non-ambiguous alphabet, and a full `shortUrl`.

Inspect the accumulated count. This endpoint is public and does not use a management key:

```powershell
Invoke-RestMethod -Uri "https://localhost:5001/api/links/$($created.code)/stats"
```

Expected result: `clickCount` is initially `0`.

Request the short URL without following its redirect:

```powershell
curl.exe -k -sS -D - -o NUL --max-redirs 0 $created.shortUrl
```

Expected result: HTTP 302 and a `Location` header matching the destination. Query statistics again; `clickCount` is now `1`.

## Validate rejected requests

Try a code containing an excluded ambiguous character:

```powershell
try {
  Invoke-RestMethod `
    -Method Post `
    -Uri "https://localhost:5001/api/links" `
    -ContentType "application/json" `
    -Body '{"destinationUrl":"https://example.com","code":"lIke"}'
} catch {
  $_.Exception.Response.StatusCode
}
```

Expected result: HTTP 400 with ProblemDetails in Portuguese and no link created.

Create another link with the same custom code (using an allowed code such as `SAVE27` twice).

Expected result: the first request succeeds; the second returns HTTP 409 and does not alter the original link.

## Validate expiration and concurrency

The xUnit integration suite must use a controllable UTC clock to test an expiration exactly at the current instant and verify HTTP 410, no redirect, and no count increment. It must also issue concurrent requests for an active code and verify that every successful 302 increments the final count exactly once. SQLite busy/locked errors may be retried within the configured timeout and retry bound; if contention still fails, the request returns an explicit server error and no redirect.

## Validate persistence across restart

1. Create a link and perform one redirect.
2. Stop the API process without deleting `shortlinks.db`.
3. Start it again with the same connection string and public origin.
4. Query the same code's statistics and follow its short URL.

Expected result: the original destination is returned and the count is at least the pre-restart count; the follow-up valid redirect increments it once more.
