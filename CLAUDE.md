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

## Status
Day 1 done: scaffold, GitHub push, Railway deploy verified in production
(https://sitewatch-production-4647.up.railway.app).
Next: Day 2 — Neon Postgres + EF Core + 4 entities (User, Site, Check,
CheckResult) + first migration.
