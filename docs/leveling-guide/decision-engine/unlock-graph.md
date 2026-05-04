# Unlock Graph

> **Pass 1 skeleton.** Directed acyclic graph of prerequisites: *A unlocks B*. Each node is a discrete progression milestone. Each edge is a precondition the DecisionEngine must verify before a downstream action is eligible.
>
> Pass 1 sketches the top-level shape. Passes 2-9 fill in concrete node IDs as class / dungeon / raid / attunement files are written.

## Node taxonomy

Nodes are namespaced. A leading namespace lets the engine fast-path filter before evaluating preconditions.

| Namespace | Example node | Source folder |
|---|---|---|
| `level.<n>` | `level.40` | implicit (level itself) |
| `class.<class>.<topic>` | `class.warrior.charge` | [classes/](../classes/) |
| `quest.<questId>` | `quest.7848` (Onyxia attunement turn-in) | [zones/](../zones/), [attunements/](../attunements/) |
| `dungeon.<dungeon>.cleared` | `dungeon.deadmines.cleared` | [dungeons/](../dungeons/) |
| `dungeon.<dungeon>.boss.<boss>` | `dungeon.brd.boss.emperor` | [dungeons/](../dungeons/) |
| `key.<keyId>` | `key.shadowforge` | [attunements/](../attunements/) |
| `attune.<raid>` | `attune.molten-core`, `attune.onyxia`, `attune.bwl`, `attune.naxx` | [attunements/](../attunements/) |
| `raid.<raid>.boss.<boss>` | `raid.mc.boss.ragnaros` | [raids/](../raids/) |
| `prof.<prof>.<rank>` | `prof.engineering.225` (gates Goblin/Gnomish split) | [professions/](../professions/) |
| `rep.<faction>.<tier>` | `rep.argent-dawn.revered` (gates Naxx attune) | [reputations/](../reputations/) |
| `rank.pvp.<n>` | `rank.pvp.10` | [pvp/](../pvp/) |
| `mount.<tier>` | `mount.apprentice`, `mount.journeyman`, `mount.epic`, `mount.epic-class` | [systems/](../systems/) |
| `spell.<id>` | `spell.polymorph-pig` (Mage book), `spell.shadowform`, `spell.travel-form`, `spell.mind-vision` | [classes/](../classes/) |
| `world.<event>` | `world.scepter-of-shifting-sands` (AQ gate event) | [raids/](../raids/) |

## Top-level edges (sketch)

The full graph is built incrementally. The most load-bearing edges:

```
level.10 ──► quest.druid.bear-form
level.10 ──► quest.warlock.summon-imp                         (lvl 1, but useful as anchor)
level.14 ──► quest.druid.aquatic-form
level.16 ──► dungeon.deadmines (Alliance)
level.18 ──► dungeon.shadowfang (Horde at SFK; Alliance can come too)
level.20 ──► quest.warrior.charge-rank-up                     (placeholder - charge is lvl 4)
level.20 ──► mount.apprentice
level.20 ──► quest.druid.cat-form
level.20 ──► bg.warsong-gulch
level.30 ──► quest.paladin.charger-prerequisite              (full chain at 60)
level.40 ──► mount.journeyman
level.40 ──► quest.druid.epic-aquatic-form                   (n/a — aquatic is free; left as marker)
level.40 ──► quest.hunter.epic-bow                            (Rhok'delar chain at 60)
level.50 ──► quest.priest.benediction                         (the chain is lvl 60)
level.50 ──► dungeon.sunken-temple
level.50 ──► bg.alterac-valley
level.52 ──► dungeon.brd
level.55 ──► dungeon.lbrs
level.58 ──► dungeon.ubrs (with Seal of Ascension)
level.58 ──► dungeon.scholomance
level.58 ──► dungeon.stratholme
level.60 ──► attune.molten-core
level.60 ──► quest.onyxia.windsor-chain                       (Alliance) / quest.onyxia.eitrigg-chain (Horde)
level.60 ──► attune.onyxia
level.60 ──► attune.bwl
level.60 ──► raid.zg                                          (no attunement)
level.60 ──► raid.aq20                                        (no attunement post-event)
level.60 ──► raid.aq40                                        (no attunement post-event; Scepter for server-first)
level.60 ──► attune.naxx (rep.argent-dawn.revered + 60g + Eye of Shadow)

dungeon.brd.boss.emperor ──► attune.molten-core (Drakkisath head alt path: ubrs.boss.drakkisath ──► attune.mc)
ubrs.boss.drakkisath ──► quest.lothos-riftwaker.attune-mc
ubrs ──► attune.bwl (Vaelastrasz orb in BRS upper)

key.shadowforge ──► dungeon.brd (full clears beyond first wing)
key.crescent ──► dungeon.sunken-temple
key.master ──► access.brm-orb (between LBRS / UBRS / MC)
key.workshop ──► dungeon.gnomeregan (workshop wing)
key.skeleton ──► dungeon.scholomance

rep.argent-dawn.honored ──► quest.naxx.scourgestone-turnins
rep.argent-dawn.revered ──► attune.naxx
rep.cenarion-circle.friendly ──► quest.silithus.dust-turnins
rep.brood.exalted ──► aq40.crafted-rings
rep.thorium.honored ──► recipes.thorium-blacksmithing
rep.timbermaw.honored ──► quest.felwood-cleansing
rep.hydraxian.honored ──► aqual-quintessence
rep.hydraxian.revered ──► eternal-quintessence (mc-trash douses)

prof.engineering.225 ──► spec.gnomish OR spec.goblin                   (irreversible split at 225 in 1.12)
prof.blacksmithing.200 ──► spec.armorsmith OR weaponsmith              (master at 250+)
prof.weaponsmith.260 ──► spec.swordsmith OR macesmith OR axesmith      (sub-spec)
prof.leatherworking.225 ──► spec.dragonscale OR elemental OR tribal

mount.epic-class.paladin ──► quest.charger-chain (lvl 60)
mount.epic-class.warlock ──► quest.dreadsteed-chain (lvl 60, Scholo + Dire Maul N)

spell.druid.travel-form @ lvl 30                                       (overrides apprentice mount until journeyman desired)
spell.shaman.ghost-wolf @ lvl 20
```

## Cycle / soft-precondition warnings

A few real edges are **not strictly required** but skip-them-and-you-pay later:

- **UBRS Seal of Ascension chain** is *not* required to enter UBRS as a guest in a 10-man, but is required to *summon* Drakkisath efficiently and is the prereq for the BWL orb. Engine should treat it as `priority.high` once UBRS is unlocked.
- **Onyxia attunement** requires UBRS for both factions (Alliance: head of Drakkisath as proof; Horde: Warchief's Mandate from Rexxar / Nazgrel). Always plan UBRS *before* the 60g Onyxia chain step.
- **MC attunement** has two paths: (a) Lothos Riftwaker after looting Drakkisath's head; (b) Hand of the Marshal NPC chain — but in practice path (a) is universal because UBRS is on the critical path anyway.

## Decision-Engine Rules

- The engine evaluates `Eligible(node) := all parents satisfied`. If a node's parents include `level.N`, the engine reads `snapshot.Level >= N`.
- Cycles are forbidden. If a future edit would create one, treat it as a graph bug — the engine logs and refuses to compile.
- Where two paths satisfy the same downstream node (UBRS-Drakkisath-head OR Marshal-NPC for MC attune), record both as alternative parents with `OR` semantics; the engine picks the cheaper path by `priority.cost`.

## Snapshot Fields Needed

- `snapshot.Level` (existing)
- `snapshot.QuestsCompleted` (existing) — every `quest.<id>` node maps to an id
- `snapshot.KeysInBags` (existing) — every `key.<id>` node
- `snapshot.Attunements` (planned) — every `attune.<raid>` node
- `snapshot.Reputation` (existing) — every `rep.<faction>.<tier>` node
- `snapshot.PrimaryProfessions` / `SecondaryProfessions` (existing) — every `prof.*` node
- `snapshot.HonorRank` (existing) — every `rank.pvp.<n>` node
- `snapshot.RidingSkill` + class-epic spellbook scan (planned) — every `mount.*` node
- `snapshot.Spells` (planned) — every `spell.<id>` node
