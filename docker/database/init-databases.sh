#!/bin/bash
set -eu

# Create VMaNGOS databases and the mangos user
echo "[init] Creating VMaNGOS databases..."
mysql -uroot -p"${MARIADB_ROOT_PASSWORD}" <<-EOSQL
  CREATE DATABASE IF NOT EXISTS realmd;
  CREATE DATABASE IF NOT EXISTS mangos;
  CREATE DATABASE IF NOT EXISTS characters;
  CREATE DATABASE IF NOT EXISTS logs;
  CREATE USER IF NOT EXISTS 'mangos'@'%' IDENTIFIED BY 'mangos';
  GRANT ALL PRIVILEGES ON realmd.* TO 'mangos'@'%';
  GRANT ALL PRIVILEGES ON mangos.* TO 'mangos'@'%';
  GRANT ALL PRIVILEGES ON characters.* TO 'mangos'@'%';
  GRANT ALL PRIVILEGES ON logs.* TO 'mangos'@'%';
  FLUSH PRIVILEGES;
EOSQL

# Import dumps (the entrypoint runs .sh before .sql, so we import manually)
echo "[init] Importing logon.sql -> realmd..."
mysql -uroot -p"${MARIADB_ROOT_PASSWORD}" realmd < /docker-entrypoint-initdb.d/dumps/logon.sql

echo "[init] Importing mangos.sql -> mangos (this may take a minute)..."
mysql -uroot -p"${MARIADB_ROOT_PASSWORD}" mangos < /docker-entrypoint-initdb.d/dumps/mangos.sql

echo "[init] Importing characters.sql -> characters..."
mysql -uroot -p"${MARIADB_ROOT_PASSWORD}" characters < /docker-entrypoint-initdb.d/dumps/characters.sql

echo "[init] Importing logs.sql -> logs..."
mysql -uroot -p"${MARIADB_ROOT_PASSWORD}" logs < /docker-entrypoint-initdb.d/dumps/logs.sql

echo "[init] VMaNGOS database import complete."
