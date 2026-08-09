# SiteWatch — 14 Day Plan

Live: https://sitewatch-production-4647.up.railway.app

Rule: one commit minimum per day. Ideas go to IDEAS.md, not into the code.

## Week 1 — Backend works

### Day 1 — Scaffold + deploy pipeline
- [x] .NET 10 Minimal API, 3 projects, solution
- [x] Dockerfile (playwright noble image, pwuser, PORT-aware)
- [x] /health + /browser-check verified locally
- [x] git init + first commit
- [x] Push to GitHub
- [x] docker build + local container test (built by Railway, not locally — Docker Desktop is not installed on this machine)
- [x] Deploy to Railway, public URL returns Example Domain

### Day 2 — Database
- [x] Neon Postgres project created, connection string in Railway vars
- [x] EF Core + Npgsql in Infra
- [x] Entities: User, Site, Check, CheckResult
- [x] First migration applied to Neon

### Day 3 — Auth
- [x] Register + login endpoints, password hashing
- [x] JWT issuing and validation
- [x] Protected endpoint returns 401 without token

### Day 4 — Sites CRUD
- [x] POST /sites, GET /sites, DELETE /sites/{id}
- [x] Scoped to the authenticated user only

### Day 5 — First Playwright scenario
- [x] Runner service in Infra: open URL, assert page loaded
- [x] Capture screenshot, store path + status + duration

### Day 6 — Checkout flow scenario
- [x] Browse -> add to cart -> reach checkout page
- [x] Passes against a real demo store
- [x] Failure produces a useful error message, not a stack trace

### Day 7 — Scheduling
- [x] Hangfire + Postgres storage
- [x] Daily recurring job per site
- [x] Results written to CheckResult table

## Week 2 — Product

### Day 8 — Alerts
- [x] Resend integration
- [x] Email on failure only, not on success
- [x] Verified by deliberately breaking a target site

### Day 9 — Flutter Web shell
- [x] Project created, routing, API client
- [x] Login screen works against the real API

### Day 10 — Sites screen
- [ ] List sites, add site, delete site

### Day 11 — Site detail
- [ ] Last 7 check results with status and timestamp
- [ ] Screenshot viewer

### Day 12 — Ship it
- [ ] Flutter Web deployed
- [ ] My own account monitoring one real site daily
- [ ] End-to-end verified from phone

### Day 13 — Portfolio assets
- [ ] README: problem, architecture, stack, screenshots
- [ ] 90-second demo video
- [ ] LinkedIn post drafted

### Day 14 — Buffer
- [ ] Catch up on whatever slipped

---

## Non-negotiable
2 hours of job search before any code, every day.
