# MMAP server-rescue handoff — 2026-06-13

Executes `docs/Plan/Pathfinding/MMAP_DESIRED_STATE_CLAUDE_PROMPT_2026-06-13.md`.
Companion runbook: `MMAP_SERVER_REGEN_PROMPT_2026-06-13.md`. Data-flow contract:
`docs/physics/MMAP_DATA_FLOW.md`.

## Outcome

The MaNGOS server mmap root `D:\MaNGOS\data\mmaps` was a broken **`001`-only**
set (map `000` / Eastern Kingdoms absent), which breaks NPC pathing. It was
**restored** to the known-good full server set and the server reloaded healthy.
Bot data roots were left untouched and remain isolated.

**Path used: EMERGENCY BACKUP RESTORE** (not regeneration).

Why restore, not regenerate: the local generation environment is
bot-contaminated and would risk reproducing the bad-copy failure —
`D:\MaNGOS\data\config.json` is a **bot-capsule** config (`agentRadius=1.0247`,
Tauren Male; references bot route tiles `3147`/`3050`), both 2026-05-31 mmap
artifacts on the server dir are broken/failed, and `dbc/` is absent. The clean
`mmaps_backup_20260315` predates all contamination, so a deterministic restore
was the correct, fast, fully-reversible rescue.

## Before / after (server root D:\MaNGOS\data\mmaps)

| | Files | Bytes | Prefixes | 000.mmap | 001.mmap |
|---|---:|---:|---|---|---|
| BEFORE (bad) | 845 (787 mmap/mmtile + 58 `.bak`) | 777,293,504 | **`001=845` ONLY** | ❌ absent | ✅ |
| AFTER (restored) | 2004 | 2,085,674,812 | `000=516, 001=787` + instances/BGs/raids through `533` | ✅ | ✅ |

Restored set is **byte-identical** to the source backup (file count + byte-sum
match, 0 zero-byte files).

## Evidence backup of the previous bad set

`D:\MaNGOS\data\mmaps.bad-20260613T153412Z` (845 files, preserved — DO NOT delete
without asking the user). Restore source `D:\MaNGOS\data\mmaps_backup_20260315`
is unchanged.

## Exact actions (in order)

1. Read-only inventory of all 6 roots; chose RESTORE.
2. Codex coding sub-agent → added read-only verifier
   `tools/scripts/verify-mmap-data-roots.ps1` (reviewed, confirmed
   non-destructive, detects the bad state with exit 1).
3. `docker stop wow-mangosd` (only this container; realmd/maria-db/bot
   services/FFXI/RO left running — no process kills).
4. Path-containment-guarded `Rename-Item mmaps -> mmaps.bad-20260613T153412Z`
   then `Copy-Item mmaps_backup_20260315 -> mmaps -Recurse`.
5. Verified restored set (read-only) + `verify-mmap-data-roots.ps1` → exit 0.
6. `docker start wow-mangosd` → healthy in ~24s; logs: `WORLD: mmap pathfinding
   enabled`, `World initialized`, SOAP bound :7878, no error/fail/missing/corrupt
   lines.
7. Codex adversarial review → 1 blocking (stale `WWOW_DATA_DIR=D:/MaNGOS/data`
   guidance in `MMAP_DATA_FLOW.md`) + 2 non-blocking. All fixed; re-review
   returned **no blocking findings, tests may proceed**.

## Verification

- `verify-mmap-data-roots.ps1`: exit 0, 0 warnings. Server not 001-only, both
  headers present, bot/server overlap only 3–4% (server-flavored, not a bot copy).
- Server health smoke: `wow-mangosd`/`wow-realmd`/`maria-db` healthy; SOAP :7878,
  auth :3724, world :8085 all listening.
- Isolated bot route probes (`-DataDir D:\wwow-bot\test-data`,
  `WWOW_DATA_DIR` unset), variant `post-server-mmap-rescue-20260613T155737Z`:
  - OG zeppelin manifest: ran clean against test-data, `error=0`
    (clean=5, step=6, 1 blocked) — matches the documented OG diagnostic surface.
  - BRM manifest: all 4 Flame Crest→BRM routes still blocked at segment 0,
    `error=0` — the **known pre-existing partial** (≈909–1214 yd short),
    unchanged by the rescue.

## Repo changes from this rescue

- NEW: `tools/scripts/verify-mmap-data-roots.ps1` (read-only verifier).
- `docs/physics/MMAP_DATA_FLOW.md`: rescinded the unsafe
  `WWOW_DATA_DIR=D:/MaNGOS/data` live-test fallback (premise false post-restore).
- `MMAP_SERVER_REGEN_PROMPT_2026-06-13.md` + `MMAP_DESIRED_STATE_CLAUDE_PROMPT_2026-06-13.md`:
  emergency-restore snippets gained the path-containment guard + targeted
  `docker stop/start wow-mangosd`.

## Remaining risks / not-done

- **BRM bot route still partial** (≈909–1214 yd short, first failure at segment 0
  for all 4 Flame Crest routes). This is a separate bot-route problem, NOT a
  server-mmap problem, and must NOT be treated as reduced/green until an
  endpoint-reaching probe exists.
- **Bot-capsule `config.json` still sits in `D:\MaNGOS\data`** — a future
  server-regeneration hazard. Do not run any generator against that dir without
  removing/replacing that config first.
- A separate **paused transport/pathing stream** on branch
  `fix/decklip-arrival-false-green` has many uncommitted working-tree changes,
  untouched by this rescue.
- Crossroads→Undercity live validation was paused; the server root is now healthy,
  but resume only after deciding the transport stream's next step.

## Do-not without asking the user

- Do not delete `mmaps.bad-20260613T153412Z`, `mmaps_backup_20260315`,
  `mmaps_old_pre_reextract`, or any backup.
- Do not shrink/minimize the server mmap set. Minimization is a separate decision.
