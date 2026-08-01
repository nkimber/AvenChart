# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/AvenChart.Api/AvenChart.Api.csproj backend/src/AvenChart.Api/
RUN dotnet restore backend/src/AvenChart.Api/AvenChart.Api.csproj

COPY backend/src/AvenChart.Api/ backend/src/AvenChart.Api/
WORKDIR /src/backend/src/AvenChart.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
COPY database/migrations ./database/migrations
EXPOSE 8080

ENTRYPOINT ["dotnet", "AvenChart.Api.dll"]
