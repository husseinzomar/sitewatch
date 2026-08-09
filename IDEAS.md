# Parked ideas — do not build during the 14 days

## Parked from Day 1
- Slim the Docker image (aspnet base + Chromium only, drop Firefox/WebKit)
- Railway region is EU West — revisit if monitoring Saudi stores
- Playwright instance is created per request; pool it if load grows
- Solution uses the new .slnx format; may break older CI tooling
- Install Docker Desktop locally when Postgres/Hangfire need it

## Parked from Day 5
- CheckOutcome serializes CheckStatus as an int in API responses; add
  JsonStringEnumConverter so clients see "Passed"/"Failed" instead of
  0/1. DB storage stays int.

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
