# Plan/Activities — Catalog Status Board

Every catalog activity has a row in this table plus a slot in the
relevant family file. Slot status fields:

- **spec** — `ActivityDefinition` in `ActivityCatalog.cs`.
- **task-family** — `IBotTask` implementation(s) the activity drives.
- **coordinator** — coordinator that orchestrates the multi-bot
  activity (where applicable).
- **tests** — LiveValidation test(s) asserting the full loop.

Status values: `not-started` | `in-progress` | `done`.

## Starter questing (1-10) — see [`quests.md`](quests.md)

| Id | Activity | Location | Faction | Level | spec | task-family | coordinator | tests |
|---|---|---|---|---|---|---|---|---|
| `quest.starter.elwynn-forest` | Starter questing | Elwynn Forest | Alliance | 1-10 | not-started | partial | not-started | not-started |
| `quest.starter.dun-morogh` | Starter questing | Dun Morogh | Alliance | 1-10 | not-started | partial | not-started | not-started |
| `quest.starter.teldrassil` | Starter questing | Teldrassil | Alliance | 1-10 | not-started | partial | not-started | not-started |
| `quest.starter.durotar` | Starter questing | Durotar | Horde | 1-10 | not-started | partial | not-started | not-started |
| `quest.starter.tirisfal-glades` | Starter questing | Tirisfal Glades | Horde | 1-10 | not-started | partial | not-started | not-started |
| `quest.starter.mulgore` | Starter questing | Mulgore | Horde | 1-10 | not-started | partial | not-started | not-started |

## Zone questing (10-60) — see [`quests.md`](quests.md)

| Id | Activity | Location | Faction | Level |
|---|---|---|---|---|
| `quest.zone.westfall` | Zone questing | Westfall | Alliance | 9-18 |
| `quest.zone.loch-modan` | Zone questing | Loch Modan | Alliance | 10-19 |
| `quest.zone.darkshore` | Zone questing | Darkshore | Alliance | 10-20 |
| `quest.zone.silverpine-forest` | Zone questing | Silverpine Forest | Horde | 10-20 |
| `quest.zone.the-barrens` | Zone questing | The Barrens | Horde | 10-25 |
| `quest.zone.redridge-mountains` | Zone questing | Redridge Mountains | Alliance | 15-25 |
| `quest.zone.ashenvale` | Zone questing | Ashenvale | Either | 18-30 |
| `quest.zone.duskwood` | Zone questing | Duskwood | Alliance | 18-30 |
| `quest.zone.wetlands` | Zone questing | Wetlands | Alliance | 20-30 |
| `quest.zone.hillsbrad-foothills` | Zone questing | Hillsbrad Foothills | Either | 20-30 |
| `quest.zone.stonetalon-mountains` | Zone questing | Stonetalon Mountains | Either | 16-27 |
| `quest.zone.thousand-needles` | Zone questing | Thousand Needles | Either | 25-35 |
| `quest.zone.desolace` | Zone questing | Desolace | Either | 28-38 |
| `quest.zone.arathi-highlands` | Zone questing | Arathi Highlands | Either | 30-40 |
| `quest.zone.stranglethorn-vale` | Zone questing | Stranglethorn Vale | Either | 30-45 |
| `quest.zone.dustwallow-marsh` | Zone questing | Dustwallow Marsh | Either | 35-45 |
| `quest.zone.badlands` | Zone questing | Badlands | Either | 35-45 |
| `quest.zone.tanaris` | Zone questing | Tanaris | Either | 40-50 |
| `quest.zone.feralas` | Zone questing | Feralas | Either | 40-50 |
| `quest.zone.searing-gorge` | Zone questing | Searing Gorge | Either | 43-50 |
| `quest.zone.azshara` | Zone questing | Azshara | Either | 45-55 |
| `quest.zone.the-hinterlands` | Zone questing | The Hinterlands | Either | 30-45 |
| `quest.zone.felwood` | Zone questing | Felwood | Either | 48-55 |
| `quest.zone.ungoro-crater` | Zone questing | Un'Goro Crater | Either | 48-55 |
| `quest.zone.western-plaguelands` | Zone questing | Western Plaguelands | Either | 50-60 |
| `quest.zone.eastern-plaguelands` | Zone questing | Eastern Plaguelands | Either | 53-60 |
| `quest.zone.burning-steppes` | Zone questing | Burning Steppes | Either | 50-58 |
| `quest.zone.winterspring` | Zone questing | Winterspring | Either | 55-60 |
| `quest.zone.silithus` | Zone questing | Silithus | Either | 55-60 |

## Dungeons — see [`dungeons.md`](dungeons.md)

| Id | Activity | Location | Level | Roles |
|---|---|---|---|---|
| `dungeon.ragefire-chasm` | Dungeon | Ragefire Chasm | 13-18 | 1T 1H 3D |
| `dungeon.wailing-caverns` | Dungeon | Wailing Caverns | 17-24 | 1T 1H 3D |
| `dungeon.deadmines` | Dungeon | Deadmines | 17-26 | 1T 1H 3D |
| `dungeon.shadowfang-keep` | Dungeon | Shadowfang Keep | 22-30 | 1T 1H 3D |
| `dungeon.blackfathom-deeps` | Dungeon | Blackfathom Deeps | 20-30 | 1T 1H 3D |
| `dungeon.razorfen-kraul` | Dungeon | Razorfen Kraul | 24-34 | 1T 1H 3D |
| `dungeon.gnomeregan` | Dungeon | Gnomeregan | 29-38 | 1T 1H 3D |
| `dungeon.razorfen-downs` | Dungeon | Razorfen Downs | 35-45 | 1T 1H 3D |
| `dungeon.uldaman` | Dungeon | Uldaman | 41-51 | 1T 1H 3D |
| `dungeon.zul-farrak` | Dungeon | Zul'Farrak | 44-54 | 1T 1H 3D |
| `dungeon.maraudon` | Dungeon | Maraudon | 46-55 | 1T 1H 3D |
| `dungeon.sunken-temple` | Dungeon | Sunken Temple | 50-56 | 1T 1H 3D |
| `dungeon.blackrock-depths` | Dungeon | Blackrock Depths | 52-60 | 1T 1H 3D |
| `dungeon.lower-blackrock-spire` | Dungeon | Lower Blackrock Spire | 55-60 | 1T 1H 8D |
| `dungeon.upper-blackrock-spire` | Dungeon | Upper Blackrock Spire | 58-60 | 1T 1H 8D |
| `dungeon.dire-maul-east` | Dungeon | Dire Maul East | 55-60 | 1T 1H 3D |
| `dungeon.dire-maul-west` | Dungeon | Dire Maul West | 55-60 | 1T 1H 3D |
| `dungeon.dire-maul-north` | Dungeon | Dire Maul North | 55-60 | 1T 1H 3D |
| `dungeon.scholomance` | Dungeon | Scholomance | 58-60 | 1T 1H 3D |
| `dungeon.stratholme-undead` | Dungeon | Stratholme Undead | 58-60 | 1T 1H 3D |
| `dungeon.stratholme-live` | Dungeon | Stratholme Live | 58-60 | 1T 1H 3D |

## Raids — see [`raids.md`](raids.md)

| Id | Activity | Location | Level | Roles | Attunement |
|---|---|---|---|---|---|
| `raid.zg` | Raid | Zul'Gurub | 60 | 2T 5H 13D (20) | none |
| `raid.aq20` | Raid | Ruins of Ahn'Qiraj | 60 | 2T 5H 13D (20) | none |
| `raid.mc` | Raid | Molten Core | 60 | 5T 8H 27D (40) | yes |
| `raid.onyxia` | Raid | Onyxia's Lair | 60 | 3T 6H 31D (40) | yes (horde/alliance chain) |
| `raid.bwl` | Raid | Blackwing Lair | 60 | 5T 9H 26D (40) | yes |
| `raid.aq40` | Raid | Temple of Ahn'Qiraj | 60 | 5T 9H 26D (40) | scarabs (server-wide) |
| `raid.naxx` | Raid | Naxxramas | 60 | 5T 10H 25D (40) | yes |

## Battlegrounds — see [`battlegrounds.md`](battlegrounds.md)

| Id | Activity | Location | Level | Roles |
|---|---|---|---|---|
| `bg.wsg` | Battleground | Warsong Gulch | 10-60 | 10v10 |
| `bg.ab` | Battleground | Arathi Basin | 20-60 | 15v15 |
| `bg.av` | Battleground | Alterac Valley | 51-60 | 40v40 |

## Professions — see [`professions-gathering.md`](professions-gathering.md) / [`professions-crafting.md`](professions-crafting.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `prof.mining-route` | Profession farming | Mining route | 1-60 |
| `prof.herbalism-route` | Profession farming | Herbalism route | 1-60 |
| `prof.skinning-route` | Profession farming | Skinning route | 1-60 |
| `prof.city-trainer-loop` | Profession leveling | City trainer + recipe loop | 5-60 |

## Economy — see [`economy.md`](economy.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `econ.ah-restock` | Economy | Auction house restock | 1-60 |
| `econ.vendor-loop` | Economy | Vendor + repair + bank + mail loop | 1-60 |

## Reputations — see [`reputations.md`](reputations.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `rep.timbermaw-hold` | Reputation grind | Timbermaw Hold | 48-60 |
| `rep.argent-dawn` | Reputation grind | Argent Dawn | 50-60 |
| `rep.cenarion-circle` | Reputation grind | Cenarion Circle | 55-60 |
| `rep.thorium-brotherhood` | Reputation grind | Thorium Brotherhood | 50-60 |
| `rep.zandalar-tribe` | Reputation grind | Zandalar Tribe | 60 |

## Attunements — see [`attunements.md`](attunements.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `attune.mc` | Attunement | Molten Core attunement | 55-60 |
| `attune.ony-horde` | Attunement | Onyxia Horde chain | 55-60 |
| `attune.ony-alliance` | Attunement | Onyxia Alliance chain | 55-60 |
| `attune.bwl` | Attunement | Blackwing Lair attunement | 58-60 |
| `attune.naxx` | Attunement | Naxxramas attunement | 60 |

## World events — see [`world-events.md`](world-events.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `event.stv-fishing-extravaganza` | World event | STV Fishing Extravaganza | 30-60 |

## World bosses — see [`world-bosses.md`](world-bosses.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `boss.azuregos` | World boss | Azuregos (Azshara) | 60 |
| `boss.kazzak` | World boss | Lord Kazzak (Blasted Lands) | 60 |
| `boss.emerald-dragons` | World boss | Emerald Dragons (rotating) | 60 |

## Total: 86 rows

(Previously advertised as 88; the actual compiled catalog ships 86
distinct `ActivityDefinition` literals. See
[`01_CATALOG_ROWS.md`](01_CATALOG_ROWS.md) for the shard breakdown.
`Tests/BotRunner.Tests/Activities/CatalogMarkdownDriftTests.cs`
asserts the id sets here match the catalog.)
