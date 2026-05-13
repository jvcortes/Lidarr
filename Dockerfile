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

# Build backend for linux-x64 only (avoids failures building for win/osx/arm targets)
RUN ./build.sh --backend -r linux-x64 -f net8.0

# Build frontend
RUN ./build.sh --frontend

# Assemble the linux-x64 package (mirrors what build.sh --packages does for PackageLinux)
RUN mkdir -p _artifacts/linux-x64/net8.0/Lidarr && \
    cp -r _output/net8.0/linux-x64/publish/* _artifacts/linux-x64/net8.0/Lidarr/ && \
    cp -r _output/Lidarr.Update/net8.0/linux-x64/publish _artifacts/linux-x64/net8.0/Lidarr/Lidarr.Update && \
    cp -r _output/UI _artifacts/linux-x64/net8.0/Lidarr/ && \
    cp LICENSE.md _artifacts/linux-x64/net8.0/Lidarr/ && \
    rm -f _artifacts/linux-x64/net8.0/Lidarr/ServiceUninstall.* \
          _artifacts/linux-x64/net8.0/Lidarr/ServiceInstall.* \
          _artifacts/linux-x64/net8.0/Lidarr/Lidarr.Windows.* && \
    cp _artifacts/linux-x64/net8.0/Lidarr/Lidarr.Mono.* _artifacts/linux-x64/net8.0/Lidarr/Lidarr.Update/ && \
    cp _artifacts/linux-x64/net8.0/Lidarr/Mono.Posix.NETStandard.* _artifacts/linux-x64/net8.0/Lidarr/Lidarr.Update/ && \
    cp _artifacts/linux-x64/net8.0/Lidarr/libMonoPosixHelper.* _artifacts/linux-x64/net8.0/Lidarr/Lidarr.Update/


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
