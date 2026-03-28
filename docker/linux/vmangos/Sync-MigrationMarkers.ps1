param(
    [string]$VmangosRepoPath = "D:\vmangos",
    [string]$GitRef = "origin/development",
    [ValidateSet("host", "docker")]
    [string]$DbMode = "docker",
    [string]$DbContainerName = "maria-db",
    [string]$DbHost = "127.0.0.1",
    [int]$DbPort = 3306,
    [string]$MySqlExePath = "",
    [string]$DbUser = "root",
    [string]$DbPassword = "root",
    [switch]$FetchOrigin
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $VmangosRepoPath)) {
    throw "VMaNGOS repo not found: $VmangosRepoPath"
}

if ($DbMode -eq "host" -and -not (Test-Path $MySqlExePath)) {
    throw "mysql.exe not found: $MySqlExePath"
}

if ($DbMode -eq "docker" -and [string]::IsNullOrWhiteSpace($DbContainerName)) {
    throw "DbContainerName is required when DbMode=docker"
}

if ($FetchOrigin) {
    & git -C $VmangosRepoPath fetch origin --force "refs/heads/development:refs/remotes/origin/development"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to fetch VMaNGOS origin in $VmangosRepoPath"
    }
}

$dbMappings = @(
    @{ Suffix = "world"; Database = "mangos" },
    @{ Suffix = "characters"; Database = "characters" },
    @{ Suffix = "logon"; Database = "realmd" },
    @{ Suffix = "logs"; Database = "logs" }
)

function Invoke-MariaDbPipe {
    param([string]$Sql)

    if ($DbMode -eq "docker") {
        $dockerArgs = @(
            "exec",
            "-i",
            $DbContainerName,
            "mariadb",
            "-u$DbUser",
            "-p$DbPassword"
        )

        $Sql | & docker @dockerArgs
        if ($LASTEXITCODE -ne 0) {
            throw "MariaDB command failed in container $DbContainerName"
        }
        return
    }

    $hostArgs = @(
        "-h", $DbHost,
        "-P", $DbPort,
        "-u$DbUser",
        "-p$DbPassword"
    )

    $Sql | & $MySqlExePath @hostArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MariaDB command failed for host $DbHost`:$DbPort"
    }
}

function Invoke-MariaDbQuery {
    param([string]$Sql)

    if ($DbMode -eq "docker") {
        $dockerArgs = @(
            "exec",
            $DbContainerName,
            "mariadb",
            "-N",
            "-B",
            "-u$DbUser",
            "-p$DbPassword",
            "-e",
            $Sql
        )

        $output = & docker @dockerArgs
        if ($LASTEXITCODE -ne 0) {
            throw "MariaDB query failed in container $DbContainerName"
        }

        return @($output)
    }

    $hostArgs = @(
        "-h", $DbHost,
        "-P", $DbPort,
        "-N",
        "-B",
        "-u$DbUser",
        "-p$DbPassword",
        "-e",
        $Sql
    )

    $output = & $MySqlExePath @hostArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MariaDB query failed for host $DbHost`:$DbPort"
    }

    return @($output)
}

function Ensure-MigrationTable {
    param([string]$DatabaseName)

    $sql = "CREATE TABLE IF NOT EXISTS $DatabaseName.migrations (id varchar(255) NOT NULL, PRIMARY KEY (id)) ENGINE=MyISAM DEFAULT CHARSET=utf8;"
    Invoke-MariaDbPipe -Sql $sql
}

function Get-MigrationInventory {
    $paths = & git -C $VmangosRepoPath ls-tree -r --name-only $GitRef -- sql/migrations
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to list migrations from $VmangosRepoPath at $GitRef"
    }

    $migrationFiles = foreach ($path in $paths) {
        if ($path -match '^sql/migrations/(?<id>\d+)_(?<suffix>world|characters|logon|logs)\.sql$') {
            [PSCustomObject]@{
                Id = $Matches["id"]
                Suffix = $Matches["suffix"]
                Path = $path
            }
        }
    }

    return $migrationFiles | Sort-Object Suffix, Id
}

function Get-ExistingMigrationIds {
    param([string]$DatabaseName)

    $rows = Invoke-MariaDbQuery -Sql "SELECT id FROM $DatabaseName.migrations ORDER BY id;"
    $set = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($row in $rows) {
        if ([string]::IsNullOrWhiteSpace($row)) {
            continue
        }

        $set.Add($row) | Out-Null
    }

    Write-Output -NoEnumerate $set
}

function Apply-MigrationFile {
    param(
        [string]$DatabaseName,
        [string]$MigrationPath
    )

    $content = & git -C $VmangosRepoPath show "$GitRef`:$MigrationPath"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to load migration file $MigrationPath from $GitRef"
    }

    $sql = "USE $DatabaseName;" + [Environment]::NewLine + ($content -join [Environment]::NewLine) + [Environment]::NewLine
    Invoke-MariaDbPipe -Sql $sql
}

$migrationInventory = @(Get-MigrationInventory)

foreach ($mapping in $dbMappings) {
    Ensure-MigrationTable -DatabaseName $mapping.Database
}

$existingByDatabase = @{}
foreach ($mapping in $dbMappings) {
    $existingByDatabase[$mapping.Database] = Get-ExistingMigrationIds -DatabaseName $mapping.Database
}

# Repair path for legacy DBs that were marker-seeded before the real logon migration ran.
$allowedClientsExists = (Invoke-MariaDbQuery -Sql "SHOW TABLES IN realmd LIKE 'allowed_clients';").Count -gt 0
if (-not $allowedClientsExists) {
    $null = $existingByDatabase["realmd"].Remove("20221117065844")
    Invoke-MariaDbPipe -Sql "DELETE FROM realmd.migrations WHERE id='20221117065844';"
}

$applied = New-Object System.Collections.Generic.List[string]

foreach ($mapping in $dbMappings) {
    $databaseName = $mapping.Database
    $files = $migrationInventory | Where-Object { $_.Suffix -eq $mapping.Suffix } | Sort-Object Id
    $existingIds = $existingByDatabase[$databaseName]

    foreach ($file in $files) {
        if ($existingIds.Contains($file.Id)) {
            continue
        }

        Apply-MigrationFile -DatabaseName $databaseName -MigrationPath $file.Path
        $existingIds.Add($file.Id) | Out-Null
        $applied.Add("${databaseName}:$($file.Id)")
    }
}

$verificationSql = @"
SELECT 'mangos' AS db_name, COUNT(*) AS migration_count FROM mangos.migrations
UNION ALL
SELECT 'characters' AS db_name, COUNT(*) AS migration_count FROM characters.migrations
UNION ALL
SELECT 'realmd' AS db_name, COUNT(*) AS migration_count FROM realmd.migrations
UNION ALL
SELECT 'logs' AS db_name, COUNT(*) AS migration_count FROM logs.migrations;
"@

if ($applied.Count -gt 0) {
    "Applied migrations:"
    $applied
}
else {
    "Applied migrations:"
    "(none)"
}

Invoke-MariaDbQuery -Sql $verificationSql
