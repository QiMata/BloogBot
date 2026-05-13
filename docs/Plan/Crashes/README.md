# Crashes — Research and Hardening

Per decision R12: every reproducible WoW.exe crash is a bug, not a
constraint. Each crash cluster gets a research file in this directory
with:

- **Symptoms** — what the operator/agent sees.
- **Reproduction** — minimal recipe to trigger.
- **WER capture** — how to get dumps off the host (open research).
- **Triage** — WinDbg/IDA findings.
- **Hardening** — code patch or guard.

## Open research: WER capture infrastructure

How does StateManager auto-capture WER dumps from FG bots when they
crash? Currently undocumented. This is a research item for the next
agent to land it. Likely path:

1. Set per-process `WerFaultBucket` registry keys on FG launch (under
   `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\WoW.exe`):
   - `DumpFolder` = `%REPO_ROOT%\TestResults\LiveLogs\WerDumps\`
   - `DumpCount` = 10 (rolling)
   - `DumpType` = 2 (full minidump)
2. Alternative: launch `procdump -ma -e -w` as a sidecar on each FG
   bot (waits, captures on unhandled exception).
3. StateManager monitors the dump folder; new dumps trigger an
   automatic `crash_cluster_<hash>` event in the UI errors panel.

Acceptable timeline: open until first reproducible crash needs the
infra. BRD/LBRS cluster (in Plan/10) is the leading candidate.

## Open clusters

### `brd-lbrs-cluster.md` (placeholder)

User's guess (2026-05-12): possible memory leak or request-spam after
the bot gets stuck. Triage: reproduce, capture, attach WinDbg.

### `fg-crash-001.md` (placeholder)

Historical crash at `0x00619CDF` in ghost-form. Last not reproduced
2026-04-15. Keep as reference; do not assume fixed.

## Adding a new cluster

```markdown
# Cluster <name>

## Symptoms
## Reproduction
## WER capture
## Triage
## Hardening
## Test
```
