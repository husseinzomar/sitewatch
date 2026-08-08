# SiteWatch

Minimal API that proves Playwright Chromium works inside a container.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — Windows install: `winget install Microsoft.DotNet.SDK.10`
- [PowerShell 7+](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-windows) (pwsh) — needed to run `playwright.ps1`; install with `winget install Microsoft.PowerShell`
  - **Alternative (no pwsh required):** the NuGet package bundles `node.exe` — see the install step below
- Docker Desktop (for container builds)
- [Railway CLI](https://docs.railway.app/develop/cli) (for deployment)

## Run locally

```bash
# 1. Build the project
dotnet build

# 2. Install Playwright's Chromium browser (one-time)
# Option A — if pwsh (PowerShell 7+) is installed:
pwsh src/SiteWatch.Api/bin/Debug/net10.0/playwright.ps1 install chromium

# Option B — no pwsh required (uses node.exe bundled in the NuGet package):
$node = "$env:USERPROFILE\.nuget\packages\microsoft.playwright\1.61.0\.playwright\node\win32_x64\node.exe"
$cli  = "$env:USERPROFILE\.nuget\packages\microsoft.playwright\1.61.0\.playwright\package\cli.js"
& $node $cli install chromium

# 3. Run
dotnet run --project src/SiteWatch.Api
```

Endpoints:
- `GET http://localhost:5000/health`
- `GET http://localhost:5000/browser-check`

## Build and test the Docker image locally

```bash
# Build
docker build -t sitewatch .

# Run
docker run --rm -p 8080:8080 -e PORT=8080 sitewatch

# Smoke-test (separate terminal)
curl http://localhost:8080/health
curl http://localhost:8080/browser-check
```

> **Image size note:** The final image is based on `mcr.microsoft.com/playwright/dotnet:v1.61.0-noble`,
> which includes the .NET 10 SDK and all three Playwright browsers. Expect ~2–3 GB on disk.
> Railway's build cache makes subsequent deploys faster.

## Deploy to Railway

```bash
# First-time setup
railway login
railway init        # run inside the SiteWatch folder; choose "Empty Project"

# Deploy
railway up

# View logs
railway logs
```

Railway auto-detects the `Dockerfile` and injects the `PORT` environment variable at runtime.
