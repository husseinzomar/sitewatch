# Parked ideas — do not build during the 14 days

## Parked from Day 1
- Slim the Docker image (aspnet base + Chromium only, drop Firefox/WebKit)
- Railway region is EU West — revisit if monitoring Saudi stores
- Playwright instance is created per request; pool it if load grows
- Solution uses the new .slnx format; may break older CI tooling
- Install Docker Desktop locally when Postgres/Hangfire need it

## Parked from Day 5
- CheckoutFlow login failure says "the login form was not found" when the
  real cause is rejected credentials. Distinguish by checking for the
  site's error banner before reporting.

## Parked from Day 8
- Downtime streak lookback is capped at 90 rows for cost reasons; a check
  failing longer than that reports "at least N days" instead of an exact
  figure. If this matters, track streak-start on the Check row instead
  of scanning history — that needs a migration, hence parking it.
- No PUT /sites endpoint — had to edit the URL directly in SQL to test
  recovery.

## Parked from Day 9
- JWT is in-memory only — session dies on refresh. Decide on persistence
  (secure storage vs refresh token) before launch.

## Parked from Day 10
- No PUT /sites endpoint, so a site's URL can't be edited from the UI —
  only delete and recreate.

## Parked from the R2 object storage session
- screenshotUrlProvider is `.autoDispose`, so navigating away from a site
  detail screen and back re-fetches a fresh presigned URL. This does NOT
  cover staying on the same screen with the tab open past the URL's
  1-hour expiry — an old thumbnail on a still-open screen can go dead.
  Low-harm (one broken thumbnail, fixed by a refresh); a timer-based fix
  was considered and deliberately skipped as disproportionate.

## Parked from Day 12
- No startup validation that lists all missing/invalid config keys at
  once — would have saved an hour of crash-loop debugging today (each
  missing Railway variable only surfaces one at a time, as whatever
  breaks first at startup).

## Parked from the AdminDashboardCheck investigation
- westcleanapp.com's own admin dashboard script.js throws an unhandled
  "ReferenceError: sidebarToggle is not defined" at line 13 on every
  page load (confirmed via a Playwright trace's console panel, seen
  twice per load). Worth flagging to their dev team — it broke
  Playwright's accessibility-tree computation for us, and may affect
  real admin users too (e.g. a broken sidebar toggle), not just this
  check.
