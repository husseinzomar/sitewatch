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

## Parked from Day 6
- Login failure reports "login form was not found" when credentials
  were actually rejected; detect the site's error banner instead
- DescribeFailure doesn't map net:: error codes (e.g. ERR_EMPTY_RESPONSE);
  raw Chromium text leaks into alert messages
- Untested: a site that hangs until the per-operation timeout

