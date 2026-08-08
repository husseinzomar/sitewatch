# SiteWatch

Playwright-based e-commerce monitoring SaaS. Solo project, 14-day build plan.

## Locked stack — do not suggest alternatives
- .NET 10 Minimal API (upgraded from 8; Playwright noble image ships .NET 10)
- Microsoft.Playwright 1.61.0 — NuGet version MUST match Docker image tag
- Docker: mcr.microsoft.com/playwright/dotnet:v1.61.0-noble, runs as pwuser
- PostgreSQL on Neon, Hangfire, Flutter Web, Railway

## Rules
- Minimum viable diffs. No features I didn't ask for.
- Show a plan and wait for confirmation before writing files.
- Verify versions and image tags against the real registry — do not
  rely on training data.
- New ideas go in IDEAS.md, not into the code.

## Neon connection string
Must use Neon's .NET-format snippet from its Connect dialog verbatim —
pooler host (`-pooler` suffix), `SSL Mode=VerifyFull`, and
`Channel Binding=Require`. Plain `Require` without channel binding, or
the non-pooler host, will silently produce auth/parsing failures that
look unrelated to the actual cause.
Set it by editing `secrets.json` directly (find the path via
`dotnet user-secrets list --project src/SiteWatch.Api` or the
`UserSecretsId` in the csproj) — PowerShell string handling mangles the
value at every semicolon when passed as a `dotnet user-secrets set`
argument. Never commit the value; never print it in chat or logs.

## Gotchas
- JwtBearer remaps inbound claims by default (`sub` → `ClaimTypes.NameIdentifier`,
  `email` → `ClaimTypes.Email`, etc). `MapInboundClaims = false` is
  required on `JwtBearerOptions`, or `FindFirstValue(JwtRegisteredClaimNames.Sub)`
  and `.Email` silently return null instead of erroring.

## Status
Day 1 done: scaffold, GitHub push, Railway deploy verified in production
(https://sitewatch-production-4647.up.railway.app).
Day 2 done: Neon Postgres + EF Core model (User, Site, Check,
CheckResult) + InitialCreate migration applied to Neon.
Day 3 done: register/login endpoints, BCrypt password hashing, JWT
issuing and validation, /me protected endpoint, Swagger with Bearer
auth (dev-only).
Day 4 done: Sites CRUD (POST/GET/DELETE /sites, GET /sites/{id}),
ownership-scoped to the authenticated user, 404 not 403 for non-owned
sites, verified with two separate users.
Next: Day 5 — first Playwright scenario in Infra (open URL, assert
page loaded, capture screenshot).
