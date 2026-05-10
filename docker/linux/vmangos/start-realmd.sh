#!/bin/sh
set -eu

input="/wwow-input/realmd.conf"
target="/opt/vmangos/config/realmd.conf"

mkdir -p /opt/vmangos/config /opt/vmangos/storage/logs
cp "$input" "$target"

db_host="${WWOW_VMANGOS_DB_HOST:-host.docker.internal}"
db_port="${WWOW_VMANGOS_DB_PORT:-3306}"
db_user="${WWOW_VMANGOS_DB_USER:-root}"
db_password="${WWOW_VMANGOS_DB_PASSWORD:-root}"
realmd_port="${WWOW_VMANGOS_REALMD_PORT:-3724}"

attempts=0
until nc -z "$db_host" "$db_port"; do
  attempts=$((attempts + 1))
  if [ "$attempts" -ge 60 ]; then
    echo "[realmd] Timed out waiting for ${db_host}:${db_port}" >&2
    exit 1
  fi

  sleep 2
done

sed -i \
  -e "s#^LoginDatabaseInfo = .*#LoginDatabaseInfo = \"${db_host};${db_port};${db_user};${db_password};realmd\"#" \
  -e 's#^LogsDir = .*#LogsDir = "/opt/vmangos/storage/logs"#' \
  -e 's#^BindIP = .*#BindIP = "0.0.0.0"#' \
  -e "s#^RealmServerPort = .*#RealmServerPort = ${realmd_port}#" \
  -e 's#^StrictVersionCheck = .*#StrictVersionCheck = 0#' \
  "$target"

# Why StrictVersionCheck=0:
# vmangos's strict check rejects AUTH_LOGON_PROOF when the client's
# crc_hash field doesn't match the server-computed hash of the genuine
# WoW.exe binary. BackgroundBotRunner is a protocol emulator with no
# real WoW.exe; its synthesized crc_hash is not the genuine binary
# hash. With the strict check enabled, every BG-bot login (and Shodan)
# fails with "tried to login with modified client" and the client sees
# WOW_FAIL_VERSION_INVALID (0x09). The Foreground bot path is unaffected
# because real WoW.exe sends the genuine hash. Disabling the check is
# correct for this Docker test environment; production realmd should
# keep it on.

exec realmd -c "$target"
