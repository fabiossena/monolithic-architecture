# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# E-commerce Monolith — single image, single process, single database.
#
#   build : docker build -t ecommerce-monolith .
#   run   : docker run --rm -p 5088:8080 -v monolith-data:/app/data ecommerce-monolith
# ---------------------------------------------------------------------------

# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
WORKDIR /src

# Restore first, on its own layer: as long as the .csproj files do not change,
# the NuGet packages stay cached in the image and the build is fast.
COPY Monolith.sln ./
COPY src/EcommerceMonolith/EcommerceMonolith.csproj src/EcommerceMonolith/
RUN dotnet restore src/EcommerceMonolith/EcommerceMonolith.csproj

COPY . .

RUN dotnet publish src/EcommerceMonolith/EcommerceMonolith.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS runtime
WORKDIR /app

# curl is used by the container HEALTHCHECK below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The SQLite database lives here so it can be mounted on a volume
# and survive container restarts / image upgrades.
RUN mkdir -p /app/data && chown -R app:app /app
VOLUME ["/app/data"]

COPY --from=build --chown=app:app /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ConnectionStrings__Sqlite="Data Source=/app/data/monolith.db"

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
    CMD curl -fsS http://localhost:8080/health || exit 1

# Run as the non-root "app" user provided by the base image.
USER app

ENTRYPOINT ["dotnet", "EcommerceMonolith.dll"]
