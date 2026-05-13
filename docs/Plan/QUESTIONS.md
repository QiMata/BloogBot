# Plan — Open Questions

> Rolling backlog of spec ambiguities and decisions that require human
> input. Workers add entries when they cannot proceed without an answer.
> The human resolves; the lead agent moves resolved entries to the
> "Resolved" section with a citation.

## Open

*(Q-S0.8.1-1, Q-S0.10-1, Q-S0.11-1, Q-S1.0-1 resolved as R19, R20, R21,
R26 in Resolved below. Five Phase-2-deferred verification flags below
remain non-blocking with `⚠ Unverified` markers in shard files.)*

### Q-S0.9.5-1 — Cenarion Circle faction id (added 2026-05-11, by monorepo-worker, in slot S0.9.5)

**Context:** S0.9.5 catalog row `rep.cenarion-circle` requires a
`FactionId` for the `RewardKind.FactionRep` reward. The S0.9.5 slot
brief proposes id **609**. A grep of the repo
(`Config/`, `Tests/`, `Exports/`, `docs/`) finds no existing assertion
that 609 is the Cenarion Circle faction id — unlike Argent Dawn (529,
verified at `Config/CharacterTemplates/HolyPriestMCReady.json:13` and
`Tests/BotRunner.Tests/Progression/ReputationTrackingTests.cs:70`) and
Timbermaw Hold (576, verified at
`Config/activities/rep.timbermaw-hold.json:15`).

**Blocker:** none — the row ships with the brief's value (609) and a
`⚠ Unverified` flag in `docs/Plan/Activities/_catalog_rows/05_misc.md`.
Phase 2 legality validator (slot S2.x) needs to confirm against
`mangos.faction_template`.

**Options:**
(a) Accept 609 per brief, verify in Phase 2 against
    `mangos.faction_template` (current path).
(b) Run a one-shot MySQL probe now via the SOAP-cleared bootstrap
    fixture and replace 609 if mangos returns a different id.
(c) Replace `FactionId: 609` with `FactionId: null` until Phase 2
    verifier lands.

**Worker assumption (if proceeding):** ⚠ ASSUMPTION: vanilla 1.12.1
`mangos.faction_template` row for Cenarion Circle uses id 609 per
S0.9.5 slot brief. Documented in the row's `Rewards[0].FactionId`.

### Q-S0.9.5-2 — Thorium Brotherhood faction id (added 2026-05-11, by monorepo-worker, in slot S0.9.5)

**Context:** S0.9.5 catalog row `rep.thorium-brotherhood` requires a
`FactionId`. The S0.9.5 slot brief itself flags this as uncertain:
"FactionId `59` (Thorium Brotherhood) — verify; alternate `577` (Brood
of Nozdormu) is a different faction." A grep of the repo finds no
existing code reference confirming 59. The `Brood of Nozdormu` faction
appears in five `docs/leveling-guide/raids/*.md` files but only by
name, not by numeric id.

**Blocker:** none — the row ships with 59 and a `⚠ Unverified` flag.

**Options:**
(a) Accept 59 per brief, verify in Phase 2.
(b) Replace with `null` until Phase 2.
(c) Probe `mangos.faction_template` now via SOAP fixture.

**Worker assumption (if proceeding):** ⚠ ASSUMPTION: Thorium
Brotherhood faction id is 59. The brief explicitly disambiguates
against 577 (Brood of Nozdormu), so the worker has not used 577.

### Q-S0.9.5-3 — Zandalar Tribe faction id (added 2026-05-11, by monorepo-worker, in slot S0.9.5)

**Context:** S0.9.5 catalog row `rep.zandalar-tribe` requires a
`FactionId`. Slot brief proposes 270. No existing repo reference
confirms.

**Blocker:** none — the row ships with 270 and a `⚠ Unverified` flag.

**Options:** same as Q-S0.9.5-1/-2.

**Worker assumption (if proceeding):** ⚠ ASSUMPTION: Zandalar Tribe
faction id is 270 per S0.9.5 brief.

### Q-S0.9.3-1 — UBRS Seal of Ascension item-id triple (added 2026-05-11, by monorepo-worker, in slot S0.9.3)

**Context:** `dungeon.upper-blackrock-spire`'s
`EntryRequirements.RequiredItems` needs the Seal-of-Ascension chain
item IDs. `docs/leveling-guide/attunements/seal-of-ascension.md`
references the chain qualitatively (Unadorned Seal → 3 Gemstones →
Forged Seal → final Seal of Ascension ring) and cites quest 4742 but
does not list `mangos.item_template` IDs. The S0.9.3 slot brief
proposes `[12344, 12342, 12343]` — best-effort mapping (final
ring=12344, unadorned=12342, forged=12343) consistent with public
Wowhead data, but no in-repo evidence ties the ring item to a specific
id.

**Blocker:** none — the row ships with the brief's IDs and a
`⚠ Unverified` flag in `docs/Plan/Activities/_catalog_rows/03_dungeons.md`.
Phase 2 legality validator cross-checks against `mangos.item_template`.

**Options:**
(a) Ship `[12344, 12342, 12343]` per brief; verify in Phase 2.
(b) Leave `RequiredItems = []` for UBRS and rely on the
    `RequiredAttunements` field once `attune.ubrs` ships.
(c) Block S0.9.3 until the IDs are confirmed against
    `mangos.item_template`.

**Worker assumption (if proceeding):** ⚠ ASSUMPTION: vanilla 1.12.1
`mangos.item_template` ids for the Seal chain are 12344 (final ring),
12342 (unadorned), 12343 (forged). The final ring is the actual UBRS
gate; the intermediates are listed so the Phase 2 validator can
recognise an in-progress chain. Fix-up slot will land if Phase 2
returns a different id triple.

### Q-S0.9.3-2 — Scholomance Skeleton Key gate (added 2026-05-11, by monorepo-worker, in slot S0.9.3)

**Context:** Live 1.12.1 required the **Skeleton Key** (forged via the
quest chain at the Bulwark in Western Plaguelands) to open the front
door of Scholomance. Private-server enforcement varies: some MaNGOS
branches require the key, some allow walk-in. `docs/leveling-guide/dungeons/scholomance.md`
documents the chain but does not pin the enforcement against the live
WWoW MaNGOS instance.

**Blocker:** none —
`dungeon.scholomance.EntryRequirements.RequiredItems` ships as `[]`
(family default). Phase 2's legality validator cross-checks against
the live server and a fix-up slot upgrades the row if the key is
enforced.

**Options:**
(a) `RequiredItems = []` now; Phase 2 verifier promotes to `[13704]`
    if the live server enforces it.
(b) `RequiredItems = [13704]` now; accept the risk of mid-run
    rejections if the live MaNGOS instance does not enforce the key.

**Worker assumption (if proceeding):** ⚠ ASSUMPTION: option (a).
Scholo row ships with `RequiredItems = []` to match the family
default. The key requirement is a server-enforcement property, not a
catalog description, and Phase 2 is the right place to surface the
mismatch.

---

*(Phase 0 spec-hardening: the 2026-05-11 self-sufficiency dry run
surfaced 5 ambiguities; all resolved in Spec/04_ACTIVITIES.md and
recorded as R14–R18 in Resolved. The 2026-05-12 spec-precision pass
surfaced 3 more; R19–R21 below.)*

## Resolved

### R1 — Doc consolidation taxonomy (resolved 2026-05-11)
Decision: single `SPEC.md` entry point referencing
`Spec/` (contracts), `Plan/` (phases), `Plan/Activities/`, plus existing
`physics/`, `server-protocol/`, `leveling-guide/`. Audits moved to
`Audits/`; deprecated handoffs to `Archive/`.

### R2 — ActivityCatalog source-of-truth (resolved 2026-05-11)
Decision: hard-coded in `Services/WoWStateManager/Activities/ActivityCatalog.cs`.
`leveling-guide/` is reference. Tests assert drift.

### R3 — Dashboard data path (resolved 2026-05-11)
Decision: Dashboard reads StateManager summary APIs only. No direct
Prometheus queries from UI.

### R4 — Prometheus/Grafana scope (resolved 2026-05-11)
Decision: first-class in `docker-compose`, surfaced through WPF
Dashboard via summary APIs. Power users may use Grafana directly.

### R5 — Single vs sharded StateManager (resolved 2026-05-11)
Decision: start with one StateManager; partition only when measured
load justifies it. AV (40v40 = 80) is the smallest scale gate.

### R6 — Cross-faction human requests (resolved 2026-05-11)
Decision: every catalog activity is requestable by any human caller;
scheduler forms the group around the human.

### R7 — UI surface (resolved 2026-05-11)
Decision: keep existing WPF (`UI/WoWStateManagerUI/`). No HTTP API
layer.

### R8 — Hot reload (resolved 2026-05-11)
Decision: yes, required for all reloadable sections. See
[`Spec/14_CONFIG.md`](../Spec/14_CONFIG.md).

### R9 — Config UI granularity (resolved 2026-05-11)
Decision: activity catalog edits + per-character settings + service
config. Operator-only commands (force-disconnect, pause, lease lock)
are extra and out of initial scope.

### R10 — Cleanup authorization (resolved 2026-05-11)
Decision: E1–E9 cleanup authorized as discussed (archive old TASKS.md
handoffs, delete completed handoff files, purge tmp, move root
screenshots, untrack LiveLogs, keep physics disasm, merge overlapping
anti-pattern docs, prune obsolete prompts).

### R11 — Cross-game replication (resolved 2026-05-11)
Decision: WWoW is gold standard. Skills are the unit of cross-game
knowledge transfer. See [`Spec/15_SKILLS.md`](../Spec/15_SKILLS.md) +
[`10_PARALLEL_SKILL_REFINEMENT.md`](10_PARALLEL_SKILL_REFINEMENT.md).

### R12 — WoW.exe crash policy (resolved 2026-05-11)
Decision: each reproducible crash gets a hardening slot. Crashes are
bugs, not constraints. Triage path: WER dump → WinDbg → root-cause →
patch or guard.

### R13 — Test signal authority (resolved 2026-05-11)
Decision: assertions through StateManager APIs are the pass/fail
signal. Screenshots and JSON reports are developer aids only.

### R14 — Location resolution (resolved 2026-05-11)
Decision: `ActivityDefinition.Location` is a plain string. The
`NamedLocationResolver` (slot ST.6 in
[`Plan/Activities/travel.md`](Activities/travel.md)) reads
`Bot/named-locations.json` at runtime to produce `(MapId, Position)`.
No `WoWZone` enum is introduced. The Phase 0 catalog test asserts
every catalog `Location` has a non-empty entry in
`Bot/named-locations.json`.

### R15 — Item-template validation deferred (resolved 2026-05-11)
Decision: `EntryRequirements.RequiredItems` references item IDs in
`mangos.item_template`, but DB-fixture validation of those references
moves to Phase 2 (the legality validator). Phase 0's catalog test only
asserts the lists are non-null with non-negative ids.

### R16 — TaskFamily existence rule (resolved 2026-05-11)
Decision: `TaskFamily` is one of the fixed family-head strings in
[`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../Spec/03_BOTRUNNER.md#catalog-of-task-families).
Phase 0 catalog test asserts membership; Phase 2 will additionally
verify each family has at least one implemented `IBotTask`.

### R17 — LockoutPolicy as hint + DB-authoritative verifier (resolved 2026-05-11)
Decision: keep `LockoutPolicy(LockoutType, string? LockoutKey)` as a
catalog HINT. The MaNGOS `character_instance` table is the authority
on actual lockout state. Phase 2 ships a `LockoutVerifier` (slot S2.12a)
that observes DB state at request time and cross-validates the hint.
Tests for catalog rows with non-None lockouts drive a fresh character
through the activity and assert observed DB lockout matches the hint.

### R18 — RewardDefinition shape + "always picks" invariant (resolved 2026-05-11)
Decision: keep `RewardDefinition(RewardKind, int Min, int Max, int?
ItemId, int? FactionId)` as the activity SUMMARY. New invariant: a bot
ALWAYS picks a reward when offered (never null). `IRewardSelector`
evolves: Phase 2 trivial (first valid index, slot S2.12b) → Phase 4
progression-aware (BiS-targeting, slot S4.2a) → future ML-augmented
(parallel skill track). Per-encounter drop tables for BoE chase items
(Eye of Sulfuras, etc.) deferred to a later spec PR via a separate
`LootTable` model. Defined in
[`Spec/03_BOTRUNNER.md#reward-selection`](../Spec/03_BOTRUNNER.md#reward-selection)
and [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md).

### R19 — `IBotTask` contract: spec is target, code is current (resolved 2026-05-12, originally Q-S0.8.1-1)

**Drift:** `Spec/03_BOTRUNNER.md#ibottask-interface` documents
`IBotTask` as a four-method async contract (`TickAsync`,
`OnPushedAsync`, `OnPoppedAsync`, `OnChildFailedAsync` with
`BotTaskContext` + `Name` + `BotTaskStatus`). Shipped today at
`Exports/BotRunner/Interfaces/IBotTask.cs` is a single synchronous
`void Update()`; lifecycle is mediated by per-task base classes
(`BotTask : IBotTask` with `PopTask(string reason)`).

**Decision:** the spec is the **Phase 1 target contract**, not a
description of current state. The current shipped interface is
incomplete substrate that Phase 1 closes.

**How this resolves S0.8 worker doc protocol:**

- The **Public surface** bullet for each task documents:
  - **Current shipped surface:** the actual signatures present today
    (usually `void Update()` plus constructor + private state).
  - **Target surface (Phase 1):** the `IBotTask` contract per Spec/03
    — `TickAsync`, `OnPushedAsync`, `OnPoppedAsync`,
    `OnChildFailedAsync`, plus `Name` and `Status`.
- A new Phase 1 slot **S1.0 — IBotTask contract migration** lands
  the target interface before any S1.4..S1.13 family slot is claimed.
  Phase 1 workers code against the new interface; the migration slot
  is the first slot a Phase 1 worker claims.
- Spec/03 gets a one-paragraph "Current shipped state" callout noting
  this so a future spec reader is not surprised.

**Out of scope for S0.8:** rewriting the `IBotTask` interface. That
is a code change owned by S1.0.

### R20 — Talent-grant GM verb (resolved 2026-05-12, originally Q-S0.10-1)

**Drift:** `Spec/17_LOADOUT.md` documents `.add talent <spellId>` as
the talent-grant verb. No existing repo path uses `.add talent`.
Candidate alternatives: `.learn <talentSpellId>`, direct
`mangos.character_talent` writes, or a vmangos-specific custom verb.

**Decision:** worker assumption stands provisionally — `Spec/17_LOADOUT.md`
documents `.add talent <spellId>` with an explicit `⚠ UNVERIFIED`
callout. Phase 2 slot **S2.4.1 — Talent-grant verb verification**
empirically confirms the verb against the live MaNGOS instance before
the OnDemand `Outfitting` stage's talent step is wired. If `.add
talent` is not a real verb, the verifier slot replaces every spec
reference and updates `Spec/17_LOADOUT.md` accordingly.

**Out of scope for S0.10:** running the GM-command experiment. That
belongs in Phase 2 against a running server.

### R21 — Test framework for spec/schema tests (resolved 2026-05-12, originally Q-S0.11-1)

**Drift:** the S0.11 brief specified MSTest. Repo convention (per
`Tests/CLAUDE.md`) is xUnit + Moq across all 11 test projects.

**Decision:** **xUnit** is the repo standard; the S0.11 worker
correctly chose option (a). All Phase 0/1 test stubs use xUnit
`[Fact]` / `[Theory]` with the `Skip=` parameter for not-yet-wired
assertions. The S0.8/S0.9/S0.10 slot definitions in
`Plan/01_PHASE0_SPEC_HARDENING.md` and any future test slots inherit
this. The Phase 0 plan's reference to MSTest was a brief-authoring
error; treat repo convention as authoritative.

### R22 — `BotTaskContext` metrics sink shape (resolved 2026-05-12, originally Q-S1.0-1 from the S0.7 dry-run)

**Drift:** `Spec/03_BOTRUNNER.md` declares `BotTaskContext` exposes a
"metrics sink"; no `IMetricsSink` interface exists. Phase 5
(Observability) introduces the rich metrics surface.

**Decision for S1.0:** Phase 1 ships **a narrow `IMetricsSink`** with
exactly two methods:

```csharp
public interface IMetricsSink
{
    void IncrementCounter(string name, IReadOnlyDictionary<string,string>? labels = null);
    void RecordDuration(string name, TimeSpan duration, IReadOnlyDictionary<string,string>? labels = null);
}
```

Anchor: `Exports/BotRunner/Tasks/IMetricsSink.cs`. The
`BotRunnerService` constructor takes an `IMetricsSink` (DI-resolved
in StateManager; null-object fallback in tests). Phase 5 either
extends this interface or adds adjacent sinks (gauges, histograms);
this two-method surface is forward-compatible.

### R23 — `BotTaskContext` chat sink shape (resolved 2026-05-12, originally Q-S1.0-2 from the S0.7 dry-run)

**Drift:** Spec/03 declares a "chat sink" on `BotTaskContext`; no
abstraction exists today (chat goes through `IBotContext` helpers).

**Decision:** S1.0 uses a delegate, not a new interface:

```csharp
public delegate void ChatSink(string channel, string text);
```

The two channels used today are `"chat"` (bot chat) and `"whisper"`
(direct whisper). Anchors:
- Definition: `Exports/BotRunner/Tasks/BotTaskContext.cs`
- Production wire-up: `BotRunnerService` resolves the delegate from
  the existing chat helpers via `IBotContext`.
- Test wire-up: lambda capturing into an in-memory list.

### R24 — `OnChildFailedAsync` return semantics (resolved 2026-05-12, originally Q-S1.0-3 from the S0.7 dry-run)

**Drift:** Spec/03 declares `Task<bool> OnChildFailedAsync(...)`.
Direction undefined.

**Decision:** **`true` means "parent absorbs the failure and keeps
running"** (handler returns `true` = "I handled it"). **`false` means
"parent also fails / escalate upward"** (handler returns `false` = "I
cannot recover; propagate"). Default base-class implementation
returns `false` (conservative — failures propagate).

S1.0's `IBotTaskContractTests` asserts both directions:
- `OnChildFailedAsync_TrueReturn_KeepsParentRunning`
- `OnChildFailedAsync_FalseReturn_PopsParentToo`

### R26 — DIM defaults on `IBotTask` (resolved 2026-05-12, originally Q-S1.0-1 raised during S1.0 implementation)

**Drift:** S1.0 brief specified the target `IBotTask` with abstract
member declarations only. A read-only test helper at
`Tests/BotRunner.Tests/BotRunnerServiceGoToDispatchTests.cs:93`
(`NoOpTask : IBotTask` with only `public void Update()`) is outside
S1.0's owned-paths but would fail to compile against the new abstract
contract.

**Decision:** **Option (a) — ship DIM defaults on every new
`IBotTask` member.** DIM defaults (`=> Task.CompletedTask` for the
four async methods, `=> GetType().Name` for `Name`,
`=> BotTaskStatus.Running` for `Status`, `=> Task.FromResult(false)`
for `OnChildFailedAsync` matching R24's conservative-escalate
default) are **body, not contract surface.** The interface member
names, signatures, and return types match the target contract in
`Spec/03_BOTRUNNER.md` exactly.

The `BotTask` base class overrides each DIM with the shim semantics
(legacy `Update()` invocation via reflection). Phase 1 family slots
(S1.4..S1.13) override `TickAsync` etc. natively per their own brief
without going through DIM. No production code path depends on the
DIM body.

This resolution applies to future interface additions in the repo:
when an abstract member would break read-only files outside owned
paths, prefer a DIM default that does the conservative thing rather
than expanding scope.

### R25 — S1.0 migration scope: shim-only + BotProfiles glob (resolved 2026-05-12, originally Q-S1.0-4 from the S0.7 dry-run)

**Drift:** S1.0's owned-paths listed `Exports/BotRunner/Tasks/**` but
the procedure said to migrate `BotProfiles/*/Tasks/**` too. The slot
wording was ambiguous about whether to convert every task's body to
async or to ship a shim.

**Decision:**
1. **Scope:** S1.0's owned-paths is **extended to include
   `BotProfiles/*/Tasks/**`** so the worker can migrate every task in
   one slot. Update `Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md` S1.0
   owned-paths.
2. **Migration depth: shim-only.** `BotTask` base class ships an
   `async Task TickAsync` whose default body calls a protected
   synchronous `OnTick(BotTaskContext)` helper. The default `OnTick`
   calls the existing `void Update()`. Every existing task inherits
   the shim unchanged — `void Update()` keeps working.
3. **Per-family async refactor is out of scope for S1.0.** Each
   family slot (S1.4–S1.13) may opt to refactor its representative
   task to override `TickAsync` directly when implementing the
   target surface per its family-file detail block.

## How to add an entry

1. Workers append to the **Open** section with:

```
### Q<n> — <short title> (added <date>, by <agent>, in slot S<phase>.<num>)
**Context:** what the worker observed
**Blocker:** which slot(s) cannot proceed
**Options:** (a) ... (b) ... (c) ...
**Worker assumption (if proceeding):** ⚠ ASSUMPTION: ...
```

2. Lead agent reviews; if resolvable from spec, answers in-line and
   moves to Resolved. Otherwise escalates to human.
3. Human posts decision; lead agent updates the entry, moves to
   Resolved, and creates a fix-up slot if the worker's assumption was
   wrong.
