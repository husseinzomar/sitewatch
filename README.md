# SiteWatch

A website monitoring app. Register, add a site, and SiteWatch runs scheduled browser checks with Playwright, stores the results and screenshots, and can send alerts.

- **Backend:** ASP.NET Core Minimal API (.NET 10), C#
- **Browser checks:** Microsoft.Playwright (Chromium)
- **Database and jobs:** PostgreSQL with EF Core, Hangfire for recurring checks
- **Auth:** JWT bearer tokens, BCrypt password hashing
- **Alerts and storage:** Resend email alerts and R2 screenshot storage (null implementations when not configured)
- **Frontend:** Flutter Web
- **Deploy:** Docker image, Railway

## Architecture

```
src/
  SiteWatch.Api      HTTP endpoints, auth, Hangfire setup
  SiteWatch.Core     Entities (User, Site, Check, CheckResult) and interfaces
  SiteWatch.Infra    EF Core + migrations, PlaywrightCheckRunner, alert sender, screenshot store, password hasher
web/                 Flutter Web client
Dockerfile
```

## API

| Method | Path | Purpose |
|---|---|---|
| GET | `/health` | health check |
| GET | `/browser-check` | confirms Playwright Chromium works in the container |
| POST | `/auth/register`, `/auth/login` | create account, get JWT |
| GET | `/me` | current user |
| POST / GET | `/sites` | add a site, list sites |
| GET / DELETE | `/sites/{id}` | site details, remove a site |
| GET | `/sites/{id}/results` | check history |
| GET | `/sites/{id}/results/{resultId}/screenshot` | screenshot of a check |
| POST | `/sites/{id}/run-check` | run a check now |

## Run locally

Prerequisites: .NET 10 SDK, PostgreSQL, Flutter SDK, PowerShell 7+ (or the bundled node option below), Docker Desktop for container builds.

Configuration needed: `ConnectionStrings:Postgres` and `Jwt:Key` (required). `Resend:ApiKey` is optional.

```bash
dotnet build
pwsh src/SiteWatch.Api/bin/Debug/net10.0/playwright.ps1 install chromium   # one-time
dotnet run --project src/SiteWatch.Api                                       # API on :5129
cd web && flutter run -d chrome --web-port=5000                              # Flutter Web
```

The Flutter dev port must be 5000: the API's dev CORS policy allows only `http://localhost:5000`.

## Docker

```bash
docker build -t sitewatch .
docker run --rm -p 8080:8080 -e PORT=8080 sitewatch
curl http://localhost:8080/health
curl http://localhost:8080/browser-check
```

The image is based on `mcr.microsoft.com/playwright/dotnet:v1.61.0-noble` (about 2-3 GB).

## Deploy to Railway

```bash
railway login
railway init
railway up
```
