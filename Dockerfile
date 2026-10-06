# syntax=docker/dockerfile:1

# One image with the Web API and the web UI. The two build stages run on the build machine's
# own platform; only the runtime stage is platform-specific. The .NET output is portable
# (framework-dependent, no runtime identifier), so the same build serves amd64 and arm64.

# 1. Web UI: Vite writes the production build into the API's wwwroot.
FROM --platform=$BUILDPLATFORM node:22-alpine AS web
WORKDIR /repo/src/SmtOrderManager.Web
COPY src/SmtOrderManager.Web/package.json src/SmtOrderManager.Web/package-lock.json ./
RUN npm ci
COPY src/SmtOrderManager.Web/ ./
RUN npm run build

# 2. API: restore with only the project files first, so the restore layer is cached until a
#    project or package version changes.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY .editorconfig Directory.Build.props Directory.Packages.props ./
COPY src/SmtOrderManager.Domain/SmtOrderManager.Domain.csproj src/SmtOrderManager.Domain/
COPY src/SmtOrderManager.Application/SmtOrderManager.Application.csproj src/SmtOrderManager.Application/
COPY src/SmtOrderManager.Infrastructure/SmtOrderManager.Infrastructure.csproj src/SmtOrderManager.Infrastructure/
COPY src/SmtOrderManager.Api/SmtOrderManager.Api.csproj src/SmtOrderManager.Api/
RUN dotnet restore src/SmtOrderManager.Api/SmtOrderManager.Api.csproj
COPY src/ src/
COPY --from=web /repo/src/SmtOrderManager.Api/wwwroot src/SmtOrderManager.Api/wwwroot
RUN dotnet publish src/SmtOrderManager.Api/SmtOrderManager.Api.csproj \
    --configuration Release --no-restore --output /app -p:UseAppHost=false

# 3. Runtime: ASP.NET Core only, as the non-root user the image provides.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# All state lives under /data. The directory belongs to the app user, so a named volume
# mounted there inherits the permissions.
RUN mkdir /data && chown app:app /data

COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Persistence__Provider=Sqlite \
    Persistence__Sqlite__DatabasePath=/data/smt-order-manager.db \
    JsonStorage__DataDirectory=/data \
    SimulatedSmtLine__InboxDirectory=/data/inbox \
    Auth__KeysDirectory=/data/keys

USER app
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["dotnet", "SmtOrderManager.Api.dll", "health-check"]

ENTRYPOINT ["dotnet", "SmtOrderManager.Api.dll"]
