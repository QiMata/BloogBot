$j = Get-Content 'C:\Users\lrhod\Documents\BloogBot\MovementRecordings\Dralrahgra_Undercity_2026-02-12_15-08-24.json' | ConvertFrom-Json
$frames = $j.frames
Write-Host "Total frames: $($frames.Count)"

$transportFrames = @($frames | Where-Object { $_.transportGuid -ne 0 })
Write-Host "Transport frames: $($transportFrames.Count)"

if ($transportFrames.Count -gt 0) {
    $first = $transportFrames[0]
    $idx = [array]::IndexOf($frames, $first)
    Write-Host "`nFirst transport frame index: $idx"
    Write-Host "  TransportGuid: $($first.transportGuid)"
    Write-Host "  Position: ($($first.position.x), $($first.position.y), $($first.position.z))"
    Write-Host "  NearbyGameObjects count: $($first.nearbyGameObjects.Count)"

    if ($first.nearbyGameObjects.Count -gt 0) {
        foreach ($go in $first.nearbyGameObjects) {
            Write-Host "    GO guid=$($go.guid) displayId=$($go.displayId) entry=$($go.entry) pos=($($go.position.x),$($go.position.y),$($go.position.z)) facing=$($go.facing)"
        }
    }

    # Check a few transport frames to see if any have the transport GO
    $hasTransportGO = 0
    $missingTransportGO = 0
    foreach ($tf in $transportFrames) {
        $tguid = $tf.transportGuid
        $found = $false
        if ($tf.nearbyGameObjects) {
            foreach ($go in $tf.nearbyGameObjects) {
                if ($go.guid -eq $tguid) { $found = $true; break }
            }
        }
        if ($found) { $hasTransportGO++ } else { $missingTransportGO++ }
    }
    Write-Host "`nTransport frames WITH matching GO in NearbyGameObjects: $hasTransportGO"
    Write-Host "Transport frames WITHOUT matching GO: $missingTransportGO"

    # Show unique TransportGuid values
    $uniqueGuids = $transportFrames | ForEach-Object { $_.transportGuid } | Sort-Object -Unique
    Write-Host "`nUnique TransportGuid values: $($uniqueGuids -join ', ')"

    # Show unique GO guids across all transport frames
    $allGOGuids = @()
    foreach ($tf in $transportFrames) {
        if ($tf.nearbyGameObjects) {
            foreach ($go in $tf.nearbyGameObjects) {
                $allGOGuids += $go.guid
            }
        }
    }
    $uniqueGOGuids = $allGOGuids | Sort-Object -Unique
    Write-Host "Unique GO GUIDs in NearbyGameObjects during transport: $($uniqueGOGuids -join ', ')"
}

# Also check non-transport frames near the transition
$boardIdx = -1
for ($i = 0; $i -lt $frames.Count - 1; $i++) {
    if ($frames[$i].transportGuid -eq 0 -and $frames[$i+1].transportGuid -ne 0) {
        $boardIdx = $i
        break
    }
}
if ($boardIdx -ge 0) {
    Write-Host "`n--- BOARD transition at frame $boardIdx ---"
    $pre = $frames[$boardIdx]
    $post = $frames[$boardIdx + 1]
    Write-Host "Pre-board frame $boardIdx : pos=($($pre.position.x),$($pre.position.y),$($pre.position.z)) transportGuid=$($pre.transportGuid)"
    Write-Host "  NearbyGOs: $($pre.nearbyGameObjects.Count)"
    if ($pre.nearbyGameObjects) {
        foreach ($go in $pre.nearbyGameObjects) {
            Write-Host "    GO guid=$($go.guid) displayId=$($go.displayId) pos=($($go.position.x),$($go.position.y),$($go.position.z))"
        }
    }
    Write-Host "Post-board frame $($boardIdx+1): pos=($($post.position.x),$($post.position.y),$($post.position.z)) transportGuid=$($post.transportGuid)"
    Write-Host "  NearbyGOs: $($post.nearbyGameObjects.Count)"
    if ($post.nearbyGameObjects) {
        foreach ($go in $post.nearbyGameObjects) {
            Write-Host "    GO guid=$($go.guid) displayId=$($go.displayId) pos=($($go.position.x),$($go.position.y),$($go.position.z))"
        }
    }
}
