---
name: crash-cluster-triage
description: Capture a WER dump, root-cause a reproducible WoW.exe (FG) crash, and ship a hardening patch + regression guard, documented as a crash cluster. Use when the foreground client crashes repeatably.
trigger: crash triage, WoW.exe crash, WER dump, FG crash, root cause a crash, hardening patch, crash cluster, CRASH-001
---

# Crash Cluster Triage

## Goal

Turn a reproducible foreground-client crash into a documented cluster: capture the
dump, root-cause it, ship a hardening patch with a regression guard, and record the
cluster so it doesn't recur silently.

## Inputs

- A reproducible crash (console stderr, WoW.exe unhandled exception, or WER report)
  and its trigger sequence.
- Key references:
  - Crash research home: `docs/Plan/Crashes/README.md` (structure + WER capture
    plan); historical reference: CRASH-001 (a known client bug — an acceptable
    test skip per CLAUDE.md).
  - Hardening sites: `Exports/BotRunner/` (FG guards), `Services/WoWStateManager/`
    (state guards), native `Navigation.dll` (collision edge cases).
- Area rules: `.github/instructions/services.instructions.md`,
  `.github/instructions/native.instructions.md`.

> **Status:** The crash-research structure is defined in
> `docs/Plan/Crashes/README.md`, but **WER-capture tooling is not yet
> implemented** on this branch — set up local dumps manually (below) per cluster.

## Preconditions

- A minimal, repeatable reproduction (set, GM commands, task sequence).
- Process safety: only kill the specific WoW.exe PID you launched — never
  blanket-kill (D2Bot and other repos share this machine).

## Procedure

1. Create a cluster doc `docs/Plan/Crashes/crash-<name>.md` with **Symptoms** and
   **Reproduction**.
2. Enable local dumps: registry
   `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\WoW.exe`
   → `DumpFolder`, `DumpType=2` (full), `DumpCount`.
3. Reproduce, collect the dump, and root-cause it (WinDbg) — record the faulting
   stack in the cluster doc.
4. Ship a hardening patch at the right layer (null/bounds guard, state-machine
   fix, native edge-case fix) + a regression test or guard.
5. Add a `FailureReason` for the path if appropriate (see [[failure-reason-mapping]]),
   and mark the cluster **Hardened**.

## Verification

- The reproduction no longer crashes after the patch.
- The regression test/guard is green; broader suite unaffected
  (`.\scripts\test-fast.ps1`).
- The cluster doc records symptoms → root cause → fix → guard.

## Outputs

- `docs/Plan/Crashes/crash-<name>.md`, a hardening patch, and a regression guard.

## Failure modes and recovery

- **Patching a symptom, not the cause** — confirm the faulting stack from the dump.
- **Blanket process kills** during repro — kill only your PID.
- **No regression guard** — the cluster will recur; always add one (or document why
  it's an accepted client bug like CRASH-001).

## Related skills

- [[failure-reason-mapping]] — classify the crash path.
- [[fg-bg-physics-parity]] — physics edge cases that crash the client.
- [[debugging]] — general investigation workflow.
- Reference: `docs/Plan/Crashes/README.md`.
