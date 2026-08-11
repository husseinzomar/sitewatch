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
- Playwright timeouts (navigation, `WaitForURLAsync`, locator actions)
  throw `System.TimeoutException`, not `Microsoft.Playwright.PlaywrightException`
  — verified against the installed 1.61.0 assembly. Both must be caught,
  or a genuinely-Failed check (site down, login rejected) gets
  misclassified as Error.
- R2 rejects AWSSDK.S3 v4's defaults: needs `RequestChecksumCalculation`
  and `ResponseChecksumValidation` set to `WHEN_REQUIRED` on
  `AmazonS3Config`, plus `UseChunkEncoding = false` and
  `DisablePayloadSigning = true` on `PutObjectRequest` — otherwise R2
  rejects the upload ("STREAMING-AWS4-HMAC-SHA256-PAYLOAD[-TRAILER] not
  implemented").
- Browsers won't let JS read the Location header of a cross-origin 302
  redirect (or follow it usefully from Flutter Web). The screenshot
  endpoint returns the presigned URL as JSON, not a redirect.
- Railway's Raw Editor REPLACES all environment variables, it does not
  merge — always paste the complete set, not just the ones you're adding
  or changing.
- Railway needs the ADO.NET connection string format, same as local — a
  libpq `postgresql://` URI crashes Hangfire at startup there too.
- `GetByRole` can hang indefinitely (even with `Force = true` on the
  subsequent click) if the target page has unhandled JS errors that
  disrupt Playwright's accessibility-tree computation — the element is
  visually present and clickable, but locator resolution itself never
  finishes. If a locator hangs on a real, visually-present element,
  check the page's console for JS errors before assuming the element or
  the check logic is wrong, and try a CSS/data-attribute selector
  instead of `GetByRole`.

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
Day 5 done: PlaywrightCheckRunner in Infra for CheckType.PageLoad —
singleton browser, per-check context, 30s total budget with a 20s
per-operation cap, Failed/Error split by failure location, screenshots
on failure only. Temporary POST /sites/{id}/run-check (dev-only) for
manual testing until Hangfire lands.
Day 6 done: CheckType.CheckoutFlow against saucedemo.com (hardcoded
target, ignores Site.Url), step-named Failed messages, shared
ExecuteAsync wrapper with PageLoad, TimeoutException classification fix
applied to both scenarios. run-check now takes ?type= (defaults to
PageLoad).
Day 7 done: Hangfire + Hangfire.PostgreSql on the "hangfire" schema
(separate from public), CheckExecutionService persists CheckResult rows
from both the daily recurring job and manual /run-check, per-job DI
scoping verified empirically, dashboard at /hangfire (dev-only).
POST /sites creates PageLoad (scheduled) + CheckoutFlow (disabled,
manual-only) checks; DELETE /sites removes their recurring jobs.

Week 1 complete: backend works end-to-end — auth, Sites CRUD, both check
scenarios, daily scheduling, persisted results.

Day 8 done: Resend email alerts on state transitions only (down/recovery),
IAlertSender in Core with ResendAlertSender/NullAlertSender in Infra —
missing Resend:ApiKey degrades to no-op with a startup warning rather
than failing to boot. Verified end-to-end with real emails.

Day 9 done: Flutter Web shell in /web (web-only platform, Riverpod
no-codegen, go_router with auth-driven redirect), ApiClient with
login()/getMe() and hand-written response models, JsonStringEnumConverter
added to the API so CheckStatus decodes by name not index. Dev-only CORS
policy for http://localhost:5000. Verified end-to-end against the real
API.

Day 10 done: sites list screen (loading/empty/error states, pull-to-refresh),
add-site dialog with client + server validation, delete with irreversible-
history-loss confirmation, all Riverpod-provider-driven (sitesProvider +
sitesControllerProvider invalidation, no setState list). ApiClient gained
a single-choke-point 401 handler (UnauthorizedException -> AuthController
.logout(), idempotent against concurrent 401s) distinct from wrong-password
and validation failures.

Day 11 done: site detail screen (/sites/:id, SiteResponse passed via
go_router extra rather than a redundant fetch, with a clean "Site not
found" fallback when extra is missing). Last 20 check results newest
first, colour-coded status badges, hand-written relative time + UTC
tooltip, non-interactive screenshot-captured indicator (no image
rendering yet — filesystem screenshots aren't servable and don't survive
a Railway deploy).

R2 object storage done: screenshots upload to R2 from memory (no local
disk), IScreenshotStore in Core with R2ScreenshotStore/NullScreenshotStore
in Infra (same degraded-mode pattern as Resend), GET
/sites/{id}/results/{resultId}/screenshot returns the presigned URL as
JSON, Flutter renders a real thumbnail that opens full-size on tap. Old
filesystem-path rows correctly fall back to "not available" via the
"screenshots/" key-prefix check. Verified end-to-end with real uploads.

Day 12 done: Flutter Web deployed to Cloudflare Pages
(https://sitewatch-a77.pages.dev), CORS policy made configurable
(Cors:AllowedOrigins, unconditional, no AllowAnyOrigin) and set for the
production origin, R2 bucket CORS updated for the production origin too,
all Railway variables set. Verified end-to-end in production, including
from phone.

AdminDashboardCheck done: CheckType.AdminDashboardCheck built, debugged,
and verified against a real production site (West Clean admin panel,
read-only — never clicks Edit/Save). Root cause of a multi-hour
investigation was the target site's own unhandled JS error
("sidebarToggle is not defined") breaking Playwright's accessibility-tree
resolution, making GetByRole hang indefinitely on a visually-present,
clickable element; worked around with a CSS selector on the affected
step (documented in code and IDEAS.md). Investigation also produced a
lasting improvement: DescribeFailure/DescribeStepFailure now preserve
Playwright's own timeout call-log detail instead of collapsing every
timeout to the same generic message.

Next: Day 13 — README and demo video.
