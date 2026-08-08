# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/SiteWatch.Api/SiteWatch.Api.csproj",   "src/SiteWatch.Api/"]
COPY ["src/SiteWatch.Core/SiteWatch.Core.csproj", "src/SiteWatch.Core/"]
COPY ["src/SiteWatch.Infra/SiteWatch.Infra.csproj","src/SiteWatch.Infra/"]
RUN dotnet restore "src/SiteWatch.Api/SiteWatch.Api.csproj"

COPY . .
RUN dotnet publish "src/SiteWatch.Api/SiteWatch.Api.csproj" \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
# Playwright noble image ships .NET 10 SDK + Chromium/Firefox/WebKit + all
# their Linux dependencies. pwuser is pre-created with correct permissions on
# /ms-playwright (where browsers live).
FROM mcr.microsoft.com/playwright/dotnet:v1.61.0-noble AS final
WORKDIR /app

COPY --from=build --chown=pwuser:pwuser /app/publish .

USER pwuser

# Railway injects PORT at runtime; fall back to 8080 locally.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet SiteWatch.Api.dll"]
