#!/bin/bash
set -e

PUID=${PUID:-1000}
PGID=${PGID:-1000}

getent group  "$PGID"  &>/dev/null || groupadd -g "$PGID" lidarr
getent passwd "$PUID"  &>/dev/null || useradd  -u "$PUID" -g "$PGID" -m -s /bin/bash lidarr

mkdir -p /config
chown -R "$PUID:$PGID" /config

exec gosu "$PUID:$PGID" /app/lidarr/bin/Lidarr -nobrowser -data=/config
