# PathPhysicsProbe

Drives `Navigation.dll` / `Physics.dll` to classify the runtime physics
affordance of every segment on a path. Use it to localize the FIRST segment
where the bake-mesh-vs-runtime-physics disagreement happens.

This tool implements the **`mmo-physics-pathing-probe`** Skill contract for
the Westworld of Warcraft repo. Sibling repos in `E:\repos\` (Final Fantasy
XI, Warhammer Online, D2Bot, etc.) each provide their own
`tools/PathPhysicsProbe/` against their own physics DLL, with the same CLI
signature, output schema, and exit codes — so cross-game scripts work.

## When to use it

- Bot stalls at a coordinate with `currentSpeed=0` and `movement_flags=0` mid-route.
- Detour's `FindPath` returns success, BotRunner stalls anyway.
- Same route works for one capsule but not another.
- A live test fails with a wall/ceiling-collision-style stuck-guard fire.

## Build

```powershell
dotnet build tools/PathPhysicsProbe/PathPhysicsProbe.csproj -c Release
```

The exe lands at `Bot/Release/net8.0/PathPhysicsProbe.exe` along with the
required `Navigation.dll` / `Physics.dll`.

## Use

```powershell
$env:WWOW_DATA_DIR = 'D:\MaNGOS\data'

# Single segment probe
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
  --map 1 `
  --start 1338.1,-4646.0,51.6 `
  --end   1335.2,-4644.4,53.5 `
  --verbose

# Multi-segment probe from a corners file
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
  --map 1 --path my-corners.txt --verbose

# Run FindPath first, then probe the resolved corners
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
  --map 1 `
  --start 1677,-4315,62 `
  --end   1331.1,-4649.5,53.6 `
  --detour-resolve --smooth --verbose --json
```

## CLI

| Flag | Default | Meaning |
|---|---|---|
| `--map <id>` | required | Map ID (uint32). |
| `--start X,Y,Z` | required (or `--path`) | Segment start. |
| `--end X,Y,Z` | required (or `--path`) | Segment end. |
| `--path corners.txt` | — | One `X,Y,Z` per line; `#` comments OK; overrides `--start`/`--end`. |
| `--radius R` | `1.0247` | Capsule radius (Tauren M default). |
| `--height H` | `2.625` | Capsule height (Tauren M default). |
| `--detour-resolve` | off | Run `FindPath(start, end)` first; probe the resolved corners. |
| `--smooth` | off | Paired with `--detour-resolve`, request smoothPath. |
| `--verbose` | off | Add endpoint surface enumeration + GroundZ breakdown for the first non-Walk segment. |
| `--json` | off | Emit machine-parseable JSON instead of TSV. |

## Output

**TSV** (default): one row per segment with `idx, sx, sy, sz, ex, ey, ez,
hDist, vDelta, affordance, validation, climb, drop, slope, resolvedZ`.

The first non-Walk segment is summarized to stderr with the surface
enumeration at its endpoint (under `--verbose`).

**JSON** (`--json`): top-level object with `map`, `radius`, `height`,
`segmentCount`, `firstFailure`, and a `segments` array.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Every segment classified `Walk` — clean path. |
| `1` | At least one `StepUp` / `JumpGap` / `SafeDrop` / etc. (passable but non-trivial). |
| `2` | At least one `Blocked` / `UnsafeDrop` / `Cliff`. |
| `3` | Argument or initialization failure. |

## What the columns mean

- **affordance** — runtime physics classification. `Walk` = clean
  walkable. `StepUp` = vertical step up within tolerance. `SteepClimb` =
  steep but passable. `JumpGap` = horizontal gap, requires jump. `SafeDrop`
  = walkable drop. `UnsafeDrop` = drop too far. `Blocked` = LOS or
  geometry rejects the segment.
- **validation** — failure reason if not `Clear`. `BlockedGeometry` = the
  capsule center can't see end from start (often: a roof/floor between).
  `MissingSupport` = no walkable surface at one or more sample points.
  `StepUpTooHigh` = step exceeds `STEP_HEIGHT`. `StepDownTooFar` = drop
  exceeds the safe-drop threshold.
- **climb / drop** — peak vertical step up / down across the 8 sample
  points along the segment.
- **slope** — segment slope in degrees from horizontal.
- **resolvedZ** — the Z value the runtime ground-snap resolves at the
  endpoint (may differ from `endZ` in the input).

## Diagnostic recipe — Tauren stalls at the OG zeppelin tower deck-edge

Confirmed-failing segment: `(1338.1, -4646.0, 51.6) → (1335.2, -4644.4, 53.5)`.

```powershell
$env:WWOW_DATA_DIR = 'D:\MaNGOS\data'
.\Bot\Release\net8.0\PathPhysicsProbe.exe `
  --map 1 `
  --start 1338.1,-4646.0,51.6 `
  --end   1335.2,-4644.4,53.5 `
  --verbose
```

Expected output (current bake):
```
affordance=Blocked validation=BlockedGeometry
hDist=3.31 vDelta=1.90 climb=1.94 slope=30.40 resolvedEndZ=53.54
surfaces at end (13):
  z=53.54  ← target deck
  z=51.49  ← lower platform (bot's height)
  z=50.82, 51.34, 50.59 ← lower platform variants
  z=35.53  ← mid-tower
  z=24.62  ← city ground
```

Read: BOTH the target deck (z=53.54) and the bot's current platform
(z=51.49) exist as walkable surfaces. The runtime LOS check from
`start+height/2 = 52.91` to `end+height/2 = 54.81` is blocked because the
deck itself (at z=53.54) is in the way. Detour's polymesh has the lower
and upper platforms as **2D-adjacent polygons at different Z**; pure 2D
adjacency lets `FindPath` produce a corner across the gap, but the
runtime correctly refuses to walk it. Bake-side fix is to prevent the
polygon graph from creating this 2D adjacency at the deck edge.
