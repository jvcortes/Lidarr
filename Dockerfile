FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build

RUN apt-get update && apt-get install -y \
        curl git ca-certificates \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && npm install -g yarn \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY . .

# NzbDrone.Core.Test targets net10.0 which is incompatible with the .NET 8 SDK
# required by global.json. Remove it from the solution before building.
RUN dotnet sln src/Lidarr.sln remove src/NzbDrone.Core.Test/Lidarr.Core.Test.csproj

RUN ./build.sh --backend --frontend --packages


FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim

RUN apt-get update && apt-get install -y \
        curl \
        gosu \
        libicu72 \
        sqlite3 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /src/_artifacts/linux-x64/net8.0/Lidarr/ /app/
COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8686
VOLUME ["/config", "/music", "/downloads"]
ENTRYPOINT ["/entrypoint.sh"]
