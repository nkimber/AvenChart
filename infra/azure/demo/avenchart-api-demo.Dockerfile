FROM node:24-alpine AS seed
WORKDIR /src
COPY demo-data ./demo-data
COPY avenchart/scripts ./avenchart/scripts
RUN node avenchart/scripts/generate-postgres-seed.mjs

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY avenchart/backend/src/AvenChart.Api/AvenChart.Api.csproj ./
RUN dotnet restore
COPY avenchart/backend/src/AvenChart.Api/ ./
# Source worktrees can contain Windows-generated obj assets. Recreate package
# assets inside this Linux image before publishing to avoid host fallback paths.
RUN rm -rf bin obj \
    && dotnet restore \
    && dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates postgresql-client \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=seed /src/avenchart/artifacts/postgres/seed-gold.sql ./demo-seed.sql
COPY avenchart/database/migrations ./database/migrations
COPY avenchart/database/bootstrap ./database/bootstrap
COPY infra/azure/demo/avenchart-api-entrypoint.sh ./avenchart-api-entrypoint.sh
RUN chmod +x ./avenchart-api-entrypoint.sh
EXPOSE 8081
ENTRYPOINT ["./avenchart-api-entrypoint.sh"]
