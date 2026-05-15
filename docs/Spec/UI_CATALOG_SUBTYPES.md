# Spec — UI Activity Catalog (Sub-Types + Parameters)

> **Scope:** Mirrors the StateManager-side `ActivityCatalog` for what the
> Config Editor in `UI/WoWStateManagerUI/` actually presents to the user.
> The UI catalog is hand-mirrored (see
> `UI/WoWStateManagerUI/Services/ActivityCatalogService.cs`) so the UI can
> evolve independently of the StateManager assembly. This doc captures the
> contract — what Types exist, what Sub-Types live under each, and what
> parameters each Sub-Type exposes.

## Two-stage picker

Every Activity slot in a config is picked via two dropdowns at the top of the
Activity Detail panel: **Type** (Family) → **Sub-Type** (specific Instance).
A third top-level dropdown — **Faction** — locks the slot to Alliance or
Horde. Cross-faction grouping doesn't exist in 1.12.1, so a slot has one
faction; "Either" only lives in the catalog template until instantiation
(normalized to Alliance) and is **never** in the slot picker.

Two universal Activity flags live on every slot:

| Flag | Effect |
|---|---|
| `Repeat` | StateManager loops the Activity after each conclusion |
| `Reset State on Start` | In-flight Activity state is cleared at kickoff; otherwise the run resumes |

`MaxCharactersPerActivity = 40`. Activities with larger conceptual rosters
(AV 40v40) are configured as **two** Activities — one per faction.

## Types

| Type (Family) | Sub-Type count | Notes |
|---|---|---|
| Battleground | 12 | WSG × 6 brackets + AB × 5 brackets + AV (51-60) |
| Dungeon/Raid | 28 | 21 dungeons + 7 raids; the catalog row's level range is shown but not user-editable |
| Leveling | 3 | Mob Grind / Questing / Dungeon |
| Reputation | 19 | One per grindable 1.12.1 faction |
| Skill | 14 | One per trainable skill + Weapon Skill |
| Earn Gold | 12 | One per earn-gold method |
| Acquisition | 2 | Acquire Item, Acquire Skill / Spell |
| Questing | 1 | Complete a specific quest |

## Battleground sub-types

Each bracketed WSG/AB and the single AV row carries a single `Strategy`
parameter (display-spaced names; the runtime matches on exact string).

### Warsong Gulch strategies

- **Flag Rush** — straight to enemy flag room, grab, run home
- **Mid-Field Control** — contest midfield, deny enemy flag carriers
- **Stealth Cap** — rogue/druid sneak pulls
- **Defense Heavy** — focus on protecting friendly flag
- **Extend Friendly EFC** — pocket-heal your carrier outside the flag room

### Arathi Basin strategies

Stables is the Alliance home node; Farm is the Horde home node. Pick the
3-cap variant matching the activity's Faction (the other becomes a no-op
at runtime if mismatched).

- **Stables / LM / BS (Alliance 3-Cap)** — Alliance home + center + push
- **Farm / LM / BS (Horde 3-Cap)** — Horde home + center + push
- **4-Cap Push** — greedy push for 4 nodes
- **5-Cap Sweep** — all 5; rare/aggressive
- **Counter-Cap Defense** — hold 2, intercept enemy push

### Alterac Valley strategies

- **Zerg General** — straight to Vanndar / Drek'Thar
- **Full Clear** — capture every tower/graveyard first
- **Defensive Turtle** — defend in your own base + bunkers

## Leveling sub-types

| Sub-Type | Parameters |
|---|---|
| Mob Grind | `LevelStart`, `LevelEnd`, 6 × per-bracket Zone dropdowns |
| Questing | `LevelStart`, `LevelEnd`, 6 × per-bracket Zone dropdowns |
| Dungeon | `LevelStart`, `LevelEnd` — **no per-bracket Dungeon dropdowns**. The coordinator auto-picks the right dungeon for each bracket (RFC/Deadmines/WC/SFK at 10-20, BFD/Stockades/SM-Graveyard at 20-30, …). The mapping is hard game data, not a config knob |

**Battlegrounding is intentionally absent** from leveling methods — pre-2.0
BGs don't award XP. **Elite Party Grind** and **Mixed** were dropped (EPG is
just a MobGrind variant; Mixed = pick one sub-type per slot, run multiple
slots if you want multiple strategies).

Zone choice arrays per bracket are in `ActivityCatalogService.cs` under the
`ZoneChoicesBracketXX_YY` constants; each starts with `"Auto"` so the
coordinator can pick a zone in that bracket.

## Reputation sub-types

19 sub-types, one per grindable faction in 1.12.1. Each has its own
`Method` choice list because factions don't share grind paths.

| Faction | Methods | Faction-locked? |
|---|---|---|
| Argent Dawn | Item Turn-In · Dungeon Mob Kills · Raid Mob Kills | No |
| Cenarion Circle | Item Turn-In · Raid Mob Kills · Quest Chain | No |
| Brood of Nozdormu | Item Turn-In · Raid Mob Kills | No |
| Hydraxian Waterlords | Raid Mob Kills · Quest Chain · World Mob Kills | No |
| Thorium Brotherhood | Item Turn-In · Dungeon Mob Kills | No |
| Timbermaw Hold | World Mob Kills · Item Turn-In | No |
| Wintersaber Trainers | Quest Chain (daily) | Alliance |
| Shen'dralar | Item Turn-In · Dungeon Mob Kills | No |
| Zandalar Tribe | Raid Mob Kills · Item Turn-In | No |
| Gelkis Clan Centaur | World Mob Kills · Quest Chain | No |
| Magram Clan Centaur | World Mob Kills · Quest Chain | No |
| Bloodsail Buccaneers | World Mob Kills | No |
| Steamwheedle Cartel | World Mob Kills | No |
| Stormpike Guard | BG Victories | Alliance |
| Frostwolf Clan | BG Victories | Horde |
| Silverwing Sentinels | BG Victories | Alliance |
| Warsong Outriders | BG Victories | Horde |
| League of Arathor | BG Victories | Alliance |
| The Defilers | BG Victories | Horde |

Common params on every Reputation Sub-Type: `TargetStanding`
(Friendly/Honored/Revered/Exalted) and `Method` (filtered per faction).

## Skill sub-types

One Sub-Type per trainable skill in 1.12.1. Cap is 300. **No `Method`
parameter** — the method is implicit in the skill name:

| Skill | Implicit method |
|---|---|
| Mining | mine ore veins |
| Herbalism | gather herb nodes |
| Skinning | skin beast corpses |
| Fishing | fish pools / open water |
| Cooking | cook recipes (cannot be levelled by gathering nodes) |
| First Aid | make bandages from cloth; `Triage` quest unlocks 225+ |
| Engineering | craft recipes; Gnomish/Goblin spec at 200 |
| Enchanting | disenchant greens, craft enchants |
| Alchemy | brew potions/elixirs/flasks |
| Blacksmithing | smith recipes; Armorsmith/Weaponsmith spec at 200 |
| Leatherworking | craft leather recipes; Tribal/Elemental/Dragonscale spec at 225 |
| Tailoring | craft cloth recipes |
| Lockpicking (Rogue-only) | pick locked boxes/doors |
| **Weapon Skill** | attack mobs at `mob_level ≈ weapon_skill / 5 ± 2`; pick `WeaponType` |

Trainer visits happen **automatically** at 75/150/225/300 caps when
`Target Level` is above the character's current rank.

Riding is **not** a skill sub-type — it's purchased (75g apprentice,
1000g journeyman), not skill-leveled.

## Earn Gold sub-types

11 Sub-Types. Each captures a distinct gold-earning loop with its own
parameter set:

| Sub-Type | Params (besides `TargetGold`) |
|---|---|
| Use Profession Skills | Skill (dropdown) · OutputItem |
| Trash Grind | FarmZone (dropdown) |
| Fishing Grind | FishingSpot · TargetCatch |
| Gather Materials | GatherSkill · RouteZone (dropdown) · TargetNodeType |
| Craft Items | Skill · RecipeName (live-search) · MaxMaterialCost |
| **Auction Flip** | Strategy (dropdown — behavior pattern, see below) |
| Repeatable Quests | QuestId (live-search) · QuestGiverNpc |
| Dungeon Farm | DungeonName |
| Raid Loot Farm | RaidName · TargetItem |
| Elite Mob Farm | EliteName · FarmZone (dropdown) |
| Event Farm | EventName |

> **Vendor Items merged into Trash Grind** — both descriptions ("kill stuff
> and vendor what drops") describe the same coordinator loop. Trash Grind
> covers both.

### Auction Flip strategies

The Strategy dropdown picks the **behavior pattern** the coordinator
applies; exact buy/sell prices come from rolling market-history tracking
(too fragile to hardcode). Tactics drawn from classic goldmaker guides
(Crouching Tiger Hidden Goblin / TSM-classic):

- **Undercut Snipe** — list at -1c below current lowest; fast turnover, low margin
- **Buyout & Relist** — sweep listings priced below market, relist at market mean
- **Reset (Walling)** — buy every listing of an item, relist higher to set a new floor
- **Restock & Cycle** — keep N units always-listed on high-volume items; replenish as they sell
- **Material Arbitrage** — buy raw mats, list crafted output (potions, bandages, bags)
- **Cross-Faction (Neutral AH)** — Booty Bay / Gadgetzan / Everlook price-diff plays
- **Cooldown Resale** — Transmute / shard cooldown mats; daily/weekly cycle
- **Bulk Repackaging** — buy large stacks, split into smaller (small stacks command per-unit premium)
- **Peak-Time Pricing** — list during raid prep (Fri/Sat); de-list off-peak
- **Recipe Sniping** — buy underpriced recipes (high markup on rare drops)
- **Twink Gear Resale** — BoE blues at popular bracket slots (19/29/39/49/59)

Item-referencing Sub-Types (Craft Items, Repeatable Quests) expose
**live-search** parameters: typing in the cell queries
`mangos.item_template` / `quest_template` with a 250ms debounce; picking a
suggestion writes the **Name** into the parameter value (and stashes the id
in Description for runtime resolution).

## Acquisition sub-types

Two sub-types, both **minimal** by design — the runtime resolves source
from game data, not from user-picked dropdowns.

### Acquire Item

Single param: `TargetItem` (live-search by name against `item_template`).
The runtime classifies the item's source from game data and picks the right
path: quest reward → run the quest; dungeon boss drop → run the dungeon;
crafted → make it (or AH / character-request); vendor → buy it.

### Acquire Skill / Spell

Single param: `TargetSpell` (live-search). For trainer-bought spells the
runtime defaults to the closest trainer; for quest-gated spells (e.g.
Druid swim form) it runs the quest. **Class-locked spells**: the runtime
acquires the spell for eligible characters and marks the Activity
**COMPLETE** for non-eligible characters once they've done all they can.

## Character row

Below the Activity Detail panel, each character is one row in a grid. The
**Character** column is an inline ComboBox bound to characters from the
DB filtered by the activity's Faction; the dropdown shows only Alliance
chars when Faction=Alliance, only Horde chars when Faction=Horde. The row
also carries free-form `Starting state` and `End state` text — what the
runtime should set up for and detect-as-done.

## Default config

`Services/WoWStateManager/Settings/Configs/Default.json` is the auto-loaded
hierarchical config. It uses `acquire.levels.questing` (Sub-Type=Questing,
Alliance) with sensible zone defaults — Elwynn → Westfall → Wetlands → STV
→ Tanaris → Eastern Plaguelands.

## Search semantics

Search-typed parameters (SearchKind = `Item` | `Quest` | `Spell`) render a
TextBox + 🔍 button. Click 🔍 to open `SearchPickerDialog`. The dialog has
a search box, hits the DB on demand (Find button or Enter), and shows up
to 100 results in `ID — Name  Extra-info` form. Double-click a row or
click OK to pick.

The query matches **name LIKE %q% OR entry = parsed-id** in parallel —
the user can type either the human-readable name OR the entry id directly.

Picking a row writes the human-readable **Name** into the parameter Value
and stashes `→ id 12344 (extra)` in the Description tooltip (visible on
hover; consumed by the runtime to resolve unambiguously when names
collide).

| SearchKind | Backing source | Notes |
|---|---|---|
| `Item` | `mangos.item_template` (live SQL, ~13k rows) | Name + Quality + ItemLevel |
| `Quest` | `mangos.quest_template` (live SQL, ~3k rows) | Title + MinLevel |
| `Spell` | `mangos.spell_template` + `skill_line_ability` (live SQL, 35k+ rows) | Name + spellLevel; **typing a class name returns all spells of that class** via `class_mask` join |

All three SearchKinds query the live MariaDB instance via
`WorldDataService` against the `mangos` schema. The class-name → bitmask
table for `Spell` is in `WorldDataService.ClassMaskFor`:

| Class | Mask | Class | Mask |
|---|---|---|---|
| Warrior | 1 | Shaman | 64 |
| Paladin | 2 | Mage | 128 |
| Hunter | 4 | Warlock | 256 |
| Rogue | 8 | Druid | 1024 |
| Priest | 16 | | |

### StateManager-as-API-aggregate (future)

Direct SQL works for everything we need today, but a longer-term path is to
proxy these lookups through the **StateManager** so the UI doesn't carry
the DB connection strings. Same swap mechanism: `WorldDataService.Search*`
methods become protobuf calls into a StateManager API. Useful for:
- Per-zone creature lookup (Mob Grind enemy-type dropdown)
- Item-source classification (drives an auto-resolved Acquire Item path)
- Spell metadata not in `spell_template` (talent trees, faction-restricted spells)

## Auto-select first

A global `Behaviors.ComboBoxBehaviors.AutoSelectFirst` (attached property
applied via the WoWTheme `ComboBox` style) selects `Items[0]` whenever a
ComboBox's ItemsSource fills with `SelectedIndex < 0`. Bound values are
preserved; for editable ComboBoxes the auto-pick only fires when `Text` is
empty. Fixes the "blank dropdown after load" class of bugs.

## Open questions / future work

- Per-zone enemy lookup for Mob Grind (needs `creature_template` joined by
  zone)
- Class-locked filter for the Character column dropdown (Warrior can't pick
  spells from the Mage trainer list, etc.)
- Auction Flip coordinator design (see note above)
- Item-source classifier from `item_template.Flags` so Acquire Item can
  pre-validate "is this even obtainable on this server"
