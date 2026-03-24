$ErrorActionPreference = "Stop"

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Wait-ForPort {
    param(
        [Parameter(Mandatory = $true)]
        [int] $Port,
        [int] $TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $client = New-Object System.Net.Sockets.TcpClient
        try {
            $iar = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
            if ($iar.AsyncWaitHandle.WaitOne(500) -and $client.Connected) {
                $client.EndConnect($iar)
                return
            }
        }
        catch {
        }
        finally {
            $client.Dispose()
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for port $Port"
}

$mysqlExe = "C:\Mangos\mysql\bin\mysqld.exe"
$mysqlArgs = "--defaults-file=C:/Mangos/mysql/my.ini --console"
$realmdExe = "C:\Mangos\server\realmd.exe"
$mangosdExe = "C:\Mangos\server\mangosd.exe"

Assert-PathExists -Path $mysqlExe -Description "MySQL server executable"
Assert-PathExists -Path $realmdExe -Description "realmd executable"
Assert-PathExists -Path $mangosdExe -Description "mangosd executable"
Assert-PathExists -Path "C:\Mangos\data" -Description "VMaNGOS data directory"

Write-Host "[vmangos] Starting MySQL from $mysqlExe"
$mysql = Start-Process -FilePath $mysqlExe -ArgumentList $mysqlArgs -WorkingDirectory "C:\Mangos\mysql\bin" -PassThru
Wait-ForPort -Port 3306 -TimeoutSeconds 60
Write-Host "[vmangos] MySQL is listening on 3306"

Write-Host "[vmangos] Starting realmd from $realmdExe"
$realmd = Start-Process -FilePath $realmdExe -WorkingDirectory "C:\Mangos\server" -PassThru
Wait-ForPort -Port 3724 -TimeoutSeconds 30
Write-Host "[vmangos] realmd is listening on 3724"

try {
    Write-Host "[vmangos] Starting mangosd from $mangosdExe"
    & $mangosdExe
    exit $LASTEXITCODE
}
finally {
    foreach ($proc in @($realmd, $mysql)) {
        if ($null -ne $proc -and -not $proc.HasExited) {
            Write-Host "[vmangos] Stopping PID $($proc.Id)"
            Stop-Process -Id $proc.Id -Force
        }
    }
}
