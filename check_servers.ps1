$procs = Get-Process | Where-Object { $_.ProcessName -match 'mangos|world|realm|logon|mysql' }
foreach ($p in $procs) {
    Write-Output "$($p.ProcessName) (PID $($p.Id))"
}
if ($procs.Count -eq 0) { Write-Output "No matching processes found" }

# Check port 8085 (default world server port)
$tcp = Get-NetTCPConnection -LocalPort 8085 -ErrorAction SilentlyContinue
if ($tcp) { Write-Output "Port 8085 OPEN (world server)" } else { Write-Output "Port 8085 NOT OPEN" }

# Check port 3724 (auth server)
$tcp2 = Get-NetTCPConnection -LocalPort 3724 -ErrorAction SilentlyContinue
if ($tcp2) { Write-Output "Port 3724 OPEN (auth server)" } else { Write-Output "Port 3724 NOT OPEN" }
