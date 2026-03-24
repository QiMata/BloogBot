#!/bin/sh
set -eu

input="/wwow-input/mangosd.conf"
target="/opt/vmangos/config/mangosd.conf"

mkdir -p /opt/vmangos/config /opt/vmangos/storage/logs /opt/vmangos/storage/honor
cp "$input" "$target"

db_host="${WWOW_VMANGOS_DB_HOST:-host.docker.internal}"
db_port="${WWOW_VMANGOS_DB_PORT:-3306}"
db_user="${WWOW_VMANGOS_DB_USER:-root}"
db_password="${WWOW_VMANGOS_DB_PASSWORD:-root}"
world_port="${WWOW_VMANGOS_WORLD_PORT:-8085}"
soap_port="${WWOW_VMANGOS_SOAP_PORT:-7878}"

attempts=0
until nc -z "$db_host" "$db_port"; do
  attempts=$((attempts + 1))
  if [ "$attempts" -ge 60 ]; then
    echo "[vmangos-mangosd] Timed out waiting for ${db_host}:${db_port}" >&2
    exit 1
  fi

  sleep 2
done

sed -i \
  -e 's#^DataDir = .*#DataDir = "/opt/vmangos/storage/data"#' \
  -e 's#^LogsDir = .*#LogsDir = "/opt/vmangos/storage/logs"#' \
  -e 's#^HonorDir = .*#HonorDir = "/opt/vmangos/storage/honor"#' \
  -e "s#^LoginDatabase.Info *= .*#LoginDatabase.Info              = \"${db_host};${db_port};${db_user};${db_password};realmd\"#" \
  -e "s#^WorldDatabase.Info *= .*#WorldDatabase.Info              = \"${db_host};${db_port};${db_user};${db_password};mangos\"#" \
  -e "s#^CharacterDatabase.Info *= .*#CharacterDatabase.Info          = \"${db_host};${db_port};${db_user};${db_password};characters\"#" \
  -e "s#^LogsDatabase.Info *= .*#LogsDatabase.Info               = \"${db_host};${db_port};${db_user};${db_password};logs\"#" \
  -e 's#^BindIP = .*#BindIP = "0.0.0.0"#' \
  -e "s#^WorldServerPort = .*#WorldServerPort = ${world_port}#" \
  -e 's#^SOAP.Enabled = .*#SOAP.Enabled = 1#' \
  -e 's#^SOAP.IP = .*#SOAP.IP = 0.0.0.0#' \
  -e "s#^SOAP.Port = .*#SOAP.Port = ${soap_port}#" \
  "$target"

exec mangosd -c "$target"
