FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build

RUN apt-get update && apt-get install -y \
        curl ca-certificates \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && npm install -g yarn \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY . .

# Build frontend
RUN yarn install --frozen-lockfile --network-timeout 120000 && \
    yarn run build --env production

# Build self-contained backend for linux-x64 only
RUN dotnet publish src/NzbDrone.Console/Lidarr.Console.csproj \
        -c Release -r linux-x64 --self-contained true -p:Platform=Posix && \
    dotnet publish src/NzbDrone.Update/Lidarr.Update.csproj \
        -c Release -r linux-x64 -f net8.0 --self-contained true -p:Platform=Posix

# Assemble package
RUN mkdir -p /output/Lidarr.Update && \
    cp -r _output/net8.0/linux-x64/publish/* /output/ && \
    cp -r _output/Lidarr.Update/net8.0/linux-x64/publish/* /output/Lidarr.Update/ && \
    cp -r _output/UI /output/ && \
    cp LICENSE.md /output/ && \
    rm -f /output/ServiceUninstall.* /output/ServiceInstall.* /output/Lidarr.Windows.*


FROM debian:bookworm-slim

RUN apt-get update && apt-get install -y \
        curl \
        gosu \
        libchromaprint-tools \
        libicu72 \
        sqlite3 \
        xmlstarlet \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /output/ /app/lidarr/bin/
COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8686
VOLUME /config
ENTRYPOINT ["/entrypoint.sh"]
