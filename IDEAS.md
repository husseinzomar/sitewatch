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

## ACTION REQUIRED ON DAY 12 — R2 CORS origin
The R2 bucket's CORS policy currently allows only `http://localhost:5000`.
Once Day 12 picks a production hosting URL for the Flutter Web frontend,
that origin MUST be added to the bucket's `AllowedOrigins`, or screenshot
thumbnails will silently fail to load in production (broken-image icon,
no error surfaced anywhere obvious) while everything else keeps working.

## Parked from the R2 object storage session
- screenshotUrlProvider is `.autoDispose`, so navigating away from a site
  detail screen and back re-fetches a fresh presigned URL. This does NOT
  cover staying on the same screen with the tab open past the URL's
  1-hour expiry — an old thumbnail on a still-open screen can go dead.
  Low-harm (one broken thumbnail, fixed by a refresh); a timer-based fix
  was considered and deliberately skipped as disproportionate.
