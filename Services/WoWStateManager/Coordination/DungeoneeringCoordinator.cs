using Communication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WoWStateManager.Clients;
using WoWStateManager.Settings;

namespace WoWStateManager.Coordination;

/// <summary>
/// Coordinates N bots for dungeon crawling with full raid preparation.
///
/// Pipeline:
///   WaitingForBots → PrepareCharacters → EquipGear → TeleportToOrgrimmar → FormGroup →
///   TeleportToRFC → DispatchDungeoneering → DungeonInProgress
///
/// PrepareCharacters: SOAP-based level set, .learn all_myclass, class-specific setup + .additem
/// EquipGear: Send EquipItem actions so bots auto-equip gear from backpack
/// TeleportToOrgrimmar: Bot chat .go xyz to Orgrimmar safe zone
/// FormGroup: Leader invites, members accept, verify
/// TeleportToRFC: Bot chat .go xyz into RFC interior (map 389)
/// DispatchDungeoneering: START_DUNGEONEERING to all bots
/// DungeonInProgress: Heal/DPS support overlay for members
/// </summary>
public class DungeoneeringCoordinator
{
    public enum CoordState
    {
        WaitingForBots,
        PrepareCharacters,
        EquipGear,
        TeleportToOrgrimmar,
        WaitForTeleportSettle,
        FormGroup_LeaveOldGroups,   // All bots leave any stale group from prior runs
        FormGroup_Inviting,         // Invite first 4 (party cap = 5 incl leader)
        FormGroup_Accepting,        // First 4 accept
        FormGroup_ConvertToRaid,    // Leader converts party → raid
        FormGroup_InvitingRest,     // Invite remaining members
        FormGroup_AcceptingRest,    // Remaining members accept
        FormGroup_Verify,
        OrganizeRaidSubgroups,  // Spread duplicate classes across subgroups for buff coverage
        TeleportToRFC,
        WaitForRFCSettle,
        DispatchDungeoneering,
        DungeonInProgress,
    }

    private readonly ILogger _logger;
    private readonly MangosSOAPClient? _soapClient;
    private string _leaderAccount; // Mutable for leader failover
    private readonly List<string> _memberAccounts;
    private readonly List<CharacterSettings> _allSettings;
    private readonly ConcurrentDictionary<string, string> _accountToCharName = new();

    private CoordState _state = CoordState.WaitingForBots;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private DateTime _phaseStartedAt = DateTime.UtcNow;
    private int _tickCount;
    private int _inviteIndex;
    private int _acceptIndex;

    // Dungeoneering dispatch tracking
    private readonly ConcurrentDictionary<string, byte> _dungeoneeringDispatched = new();

    // Prep tracking — concurrent because multiple bot threads call GetAction()
    private readonly ConcurrentDictionary<string, byte> _preparedAccounts = new();
    private readonly ConcurrentDictionary<string, byte> _teleportedToOrg = new();
    private readonly ConcurrentDictionary<string, byte> _teleportedToRFC = new();
    private readonly List<string> _rfcTeleportOrder = new(); // Built once: leader first, then members
    private int _rfcTeleportIndex;
    private DateTime _lastRfcTeleportAt = DateTime.MinValue;
    private const double RFC_TELEPORT_STAGGER_SEC = 3.0; // Seconds between each bot's teleport (3s to avoid DESTROY_OBJECT storms)
    private int _prepIndex;

    // Equip tracking — each bot needs EquipItem actions for each gear piece
    private readonly ConcurrentDictionary<string, int> _equipItemIndex = new(); // account → next item index to equip

    // Orgrimmar safe zone
    private const float OrgX = 1629.4f;
    private const float OrgY = -4373.4f;
    private const float OrgZ = 34.2f; // Z+3

    // RFC interior start (first waypoint area)
    private const int RfcMapId = 389;
    private const float RfcStartX = 3f;
    private const float RfcStartY = -11f;
    private const float RfcStartZ = -15f; // Z+3 from waypoint at -18

    // Vanilla WoW party limit: 5 members (including leader). Must convert to raid for 6+.
    private const int PARTY_SIZE_LIMIT = 4; // 4 invites = 5 total with leader
    private bool _raidConvertSent;
    private readonly ConcurrentDictionary<string, byte> _leftOldGroup = new();

    // Throttle follow/heal actions per account — concurrent for multi-thread safety
    private readonly ConcurrentDictionary<string, DateTime> _lastActionSent = new();
    private const double ACTION_COOLDOWN_SEC = 3.0;
    private const float HEAL_THRESHOLD = 0.50f;

    // Cached healer spell IDs — concurrent for multi-thread safety
    private readonly ConcurrentDictionary<string, uint> _healSpells = new();
    private readonly ConcurrentDictionary<string, uint> _damageSpells = new();
    private readonly ConcurrentDictionary<string, byte> _spellsResolved = new();

    // Raid subgroup organization
    private readonly ConcurrentDictionary<string, byte> _subgroupAssignments = new(); // charName → subgroup (0-7)
    private int _subgroupAssignIndex;
    private bool _subgroupsComputed;

    // Leader failover — promote a BG bot if FG leader drops
    private DateTime _leaderLastSeen = DateTime.UtcNow;
    private const double LEADER_FAILOVER_TIMEOUT_SEC = 15.0;
    private bool _leaderFailoverTriggered;

    public CoordState State => _state;

    public DungeoneeringCoordinator(
        string leaderAccount,
        IEnumerable<string> allAccounts,
        List<CharacterSettings> allSettings,
        MangosSOAPClient? soapClient,
        ILogger logger)
    {
        _leaderAccount = leaderAccount;
        _memberAccounts = allAccounts
            .Where(a => !a.Equals(leaderAccount, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _allSettings = allSettings;
        _soapClient = soapClient;
        _logger = logger;

        _logger.LogInformation("DUNGEON_COORD: Initialized — Leader='{Leader}', Members=[{Members}], SOAP={HasSoap}",
            leaderAccount, string.Join(", ", _memberAccounts), soapClient != null);
    }

    public ActionMessage? GetAction(
        string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Resolve character names from snapshots
        foreach (var kvp in snapshots)
        {
            if (!string.IsNullOrEmpty(kvp.Value.CharacterName))
                _accountToCharName[kvp.Key] = kvp.Value.CharacterName;
        }

        // Count InWorld bots
        var inWorldCount = snapshots.Count(s => s.Value.ScreenState == "InWorld");
        if (inWorldCount < 2)
            return null;

        // Timeout protection for group formation states
        var elapsed = (DateTime.UtcNow - _stateEnteredAt).TotalSeconds;
        if (_state is CoordState.FormGroup_LeaveOldGroups or CoordState.FormGroup_Inviting
            or CoordState.FormGroup_Accepting or CoordState.FormGroup_ConvertToRaid
            or CoordState.FormGroup_InvitingRest or CoordState.FormGroup_AcceptingRest
            or CoordState.FormGroup_Verify or CoordState.OrganizeRaidSubgroups
            && elapsed > 60)
        {
            _logger.LogWarning("DUNGEON_COORD: State {State} timed out after {Elapsed:F0}s, advancing", _state, elapsed);
            TransitionTo(CoordState.TeleportToRFC);
        }

        return _state switch
        {
            CoordState.WaitingForBots => HandleWaitingForBots(requestingAccount, snapshots),
            CoordState.PrepareCharacters => HandlePrepareCharacters(requestingAccount, snapshots),
            CoordState.EquipGear => HandleEquipGear(requestingAccount, snapshots),
            CoordState.TeleportToOrgrimmar => HandleTeleportToOrgrimmar(requestingAccount, snapshots),
            CoordState.WaitForTeleportSettle => HandleWaitForSettle(requestingAccount, snapshots, CoordState.FormGroup_LeaveOldGroups, 1),
            CoordState.FormGroup_LeaveOldGroups => HandleLeaveOldGroups(requestingAccount, snapshots),
            CoordState.FormGroup_Inviting => HandleInviting(requestingAccount, snapshots),
            CoordState.FormGroup_Accepting => HandleAccepting(requestingAccount, snapshots),
            CoordState.FormGroup_ConvertToRaid => HandleConvertToRaid(requestingAccount, snapshots),
            CoordState.FormGroup_InvitingRest => HandleInvitingRest(requestingAccount, snapshots),
            CoordState.FormGroup_AcceptingRest => HandleAcceptingRest(requestingAccount, snapshots),
            CoordState.FormGroup_Verify => HandleVerify(requestingAccount, snapshots),
            CoordState.OrganizeRaidSubgroups => HandleOrganizeRaidSubgroups(requestingAccount, snapshots),
            CoordState.TeleportToRFC => HandleTeleportToRFC(requestingAccount, snapshots),
            CoordState.WaitForRFCSettle => HandleWaitForSettle(requestingAccount, snapshots, CoordState.DispatchDungeoneering, RfcMapId),
            CoordState.DispatchDungeoneering => HandleDispatchDungeoneering(requestingAccount, snapshots),
            CoordState.DungeonInProgress => HandleDungeonInProgress(requestingAccount, snapshots),
            _ => null,
        };
    }

    private void TransitionTo(CoordState newState)
    {
        _logger.LogInformation("DUNGEON_COORD: {Old} → {New}", _state, newState);
        _state = newState;
        _stateEnteredAt = DateTime.UtcNow;
        _tickCount = 0;
    }

    // ===== State Handlers =====

    private ActionMessage? HandleWaitingForBots(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        if (!snapshots.TryGetValue(_leaderAccount, out var leaderSnap) || leaderSnap.ScreenState != "InWorld")
            return null;

        var readyMembers = _memberAccounts.Count(m =>
            snapshots.TryGetValue(m, out var s) && s.ScreenState == "InWorld");

        if (readyMembers < 1)
            return null;

        _logger.LogInformation("DUNGEON_COORD: {Count} members ready. Starting character preparation.",
            readyMembers);

        // If SOAP is available, do full prep; otherwise skip to group formation
        if (_soapClient != null)
        {
            _prepIndex = 0;
            TransitionTo(CoordState.PrepareCharacters);
        }
        else
        {
            _logger.LogWarning("DUNGEON_COORD: No SOAP client — skipping character preparation.");
            TransitionTo(CoordState.FormGroup_LeaveOldGroups);
        }
        return null;
    }

    private ActionMessage? HandlePrepareCharacters(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        if (_soapClient == null)
        {
            TransitionTo(CoordState.EquipGear);
            return null;
        }

        // Process one account per tick (SOAP is async, fire-and-forget from coordinator)
        _tickCount++;
        if (_tickCount % 3 != 1) // ~1.5s between preps
            return null;

        // Find next account to prepare
        var allAccounts = new List<string> { _leaderAccount };
        allAccounts.AddRange(_memberAccounts);

        while (_prepIndex < allAccounts.Count)
        {
            var account = allAccounts[_prepIndex];
            _prepIndex++;

            if (_preparedAccounts.ContainsKey(account))
                continue;

            if (!_accountToCharName.TryGetValue(account, out var charName))
                continue; // Not in world yet

            if (!snapshots.TryGetValue(account, out var snap) || snap.ScreenState != "InWorld")
                continue;

            // Fire SOAP commands for this character (async, don't await)
            _ = PrepareCharacterAsync(account, charName);
            _preparedAccounts.TryAdd(account, 0);
            return null; // One account per tick
        }

        // All accounts prepared — now equip the gear that was added to backpacks
        _logger.LogInformation("DUNGEON_COORD: All {Count} characters prepared. Equipping gear.",
            _preparedAccounts.Count);  // ConcurrentDictionary.Count is safe
        TransitionTo(CoordState.EquipGear);
        return null;
    }

    // ===== Level 8 Class Configuration =====
    // Spells available at level 8 in Vanilla WoW 1.12.1 (via .learn).
    // .learn all_myclass teaches ALL trainer spells — these are the key ones per class for verification.
    // Gear: green-quality dungeon-appropriate items for RFC (level 13-16 mobs).

    /// <summary>Key spell IDs per class at level 8, used for verification after .learn all_myclass.</summary>
    public static readonly Dictionary<string, uint[]> Level8KeySpells = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Warrior"] = [
            78,    // Heroic Strike (Rank 1) — level 1
            6673,  // Battle Shout (Rank 1) — level 1, PARTY-WIDE buff
            100,   // Charge (Rank 1) — level 4
            772,   // Rend (Rank 1) — level 4
            6343,  // Thunder Clap (Rank 1) — level 6
            34428, // Victory Rush — level 6
            1715,  // Hamstring — level 8
        ],
        ["Shaman"] = [
            403,   // Lightning Bolt (Rank 1) — level 1
            8071,  // Stoneskin Totem (Rank 1) — level 4, PARTY-WIDE buff
            332,   // Healing Wave (Rank 2) — level 6
            529,   // Lightning Bolt (Rank 2) — level 8
            8075,  // Strength of Earth Totem (Rank 1) — level 4, PARTY-WIDE buff
            324,   // Lightning Shield (Rank 1) — level 8
            8018,  // Rockbiter Weapon (Rank 1) — level 1
        ],
        ["Druid"] = [
            5176,  // Wrath (Rank 1) — level 1
            774,   // Rejuvenation (Rank 1) — level 4
            5177,  // Wrath (Rank 2) — level 6
            8921,  // Moonfire (Rank 1) — level 4
            5186,  // Healing Touch (Rank 2) — level 4
            1058,  // Rejuvenation (Rank 2) — level 8 (PARTY heal)
            467,   // Thorns (Rank 1) — level 6
        ],
        ["Priest"] = [
            585,   // Smite (Rank 1) — level 1
            2050,  // Lesser Heal (Rank 1) — level 1
            589,   // Shadow Word: Pain (Rank 1) — level 4
            591,   // Smite (Rank 2) — level 6
            17,    // Power Word: Shield (Rank 1) — level 6
            1244,  // Power Word: Fortitude (Rank 1) — level 1, PARTY-WIDE buff
            2052,  // Lesser Heal (Rank 2) — level 4
            586,   // Fade (Rank 1) — level 8
        ],
        ["Warlock"] = [
            686,   // Shadow Bolt (Rank 1) — level 1
            687,   // Demon Skin (Rank 1) — level 1
            172,   // Corruption (Rank 1) — level 4
            702,   // Curse of Weakness (Rank 1) — level 4
            695,   // Shadow Bolt (Rank 2) — level 6
            1454,  // Life Tap (Rank 1) — level 6
            980,   // Curse of Agony (Rank 1) — level 8
            688,   // Summon Imp — level 1
        ],
        ["Hunter"] = [
            75,    // Auto Shot — level 1
            2973,  // Raptor Strike (Rank 1) — level 1
            1978,  // Serpent Sting (Rank 1) — level 4
            3044,  // Arcane Shot (Rank 1) — level 6
            1130,  // Hunter's Mark (Rank 1) — level 6
            14260, // Raptor Strike (Rank 2) — level 8
        ],
        ["Rogue"] = [
            1752,  // Sinister Strike (Rank 1) — level 1
            2098,  // Eviscerate (Rank 1) — level 1
            1784,  // Stealth (Rank 1) — level 1
            53,    // Backstab (Rank 1) — level 4
            1776,  // Gouge (Rank 1) — level 6
            6760,  // Eviscerate (Rank 2) — level 8
        ],
        ["Mage"] = [
            133,   // Fireball (Rank 1) — level 1
            168,   // Frost Armor (Rank 1) — level 1
            116,   // Frostbolt (Rank 1) — level 4
            143,   // Fireball (Rank 2) — level 6
            587,   // Conjure Food (Rank 1) — level 6
            5504,  // Conjure Water (Rank 1) — level 4
            205,   // Frostbolt (Rank 2) — level 8
        ],
    };

    /// <summary>Item IDs for level-8 dungeon-appropriate gear per class.</summary>
    public static readonly Dictionary<string, (uint ItemId, string Name)[]> Level8Gear = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Warrior"] = [
            (2385,  "Tarnished Chain Vest"),        // Mail chest
            (2387,  "Tarnished Chain Leggings"),    // Mail legs
            (2386,  "Tarnished Chain Gloves"),      // Mail hands
            (2388,  "Tarnished Chain Boots"),       // Mail feet
            (25,    "Worn Shortsword"),             // 1H Sword (main hand)
            (2129,  "Large Round Shield"),          // Shield (off-hand)
        ],
        ["Shaman"] = [
            (2385,  "Tarnished Chain Vest"),        // Mail chest
            (2387,  "Tarnished Chain Leggings"),    // Mail legs
            (2386,  "Tarnished Chain Gloves"),      // Mail hands
            (36,    "Worn Mace"),                   // 1H Mace
            (2129,  "Large Round Shield"),          // Shield
            (5175,  "Earth Totem"),                 // Required for totem spells
        ],
        ["Druid"] = [
            (6059,  "Nomad Vest"),                  // Leather chest
            (1839,  "Rough Leather Pants"),         // Leather legs
            (2122,  "Cracked Leather Gloves"),      // Leather hands
            (2493,  "Wooden Mallet"),               // 2H Mace (staff alternative)
        ],
        ["Priest"] = [
            (6096,  "Apprentice's Robe"),           // Cloth chest
            (1395,  "Apprentice's Pants"),          // Cloth legs
            (55,    "Apprentice's Boots"),          // Cloth feet
            (955,   "Apprentice's Staff"),          // Staff (2H)
        ],
        ["Warlock"] = [
            (6096,  "Apprentice's Robe"),           // Cloth chest
            (1395,  "Apprentice's Pants"),          // Cloth legs
            (55,    "Apprentice's Boots"),          // Cloth feet
            (955,   "Apprentice's Staff"),          // Staff (2H)
        ],
        ["Hunter"] = [
            (2385,  "Tarnished Chain Vest"),        // Mail chest
            (2387,  "Tarnished Chain Leggings"),    // Mail legs
            (2386,  "Tarnished Chain Gloves"),      // Mail hands
            (2504,  "Worn Crossbow"),               // Ranged weapon
            (2512,  "Rough Arrow"),                 // Ammo (quantity added separately)
            (25,    "Worn Shortsword"),             // Melee backup
        ],
        ["Rogue"] = [
            (2473,  "Reinforced Leather Vest"),     // Leather chest
            (1839,  "Rough Leather Pants"),         // Leather legs
            (2122,  "Cracked Leather Gloves"),      // Leather hands
            (2092,  "Worn Dagger"),                 // Main hand dagger
            (2092,  "Worn Dagger"),                 // Off-hand dagger
        ],
        ["Mage"] = [
            (6096,  "Apprentice's Robe"),           // Cloth chest
            (1395,  "Apprentice's Pants"),          // Cloth legs
            (55,    "Apprentice's Boots"),          // Cloth feet
            (955,   "Apprentice's Staff"),          // Staff (2H)
        ],
    };

    private async Task PrepareCharacterAsync(string account, string charName)
    {
        if (_soapClient == null) return;

        try
        {
            var settings = _allSettings.FirstOrDefault(s =>
                s.AccountName.Equals(account, StringComparison.OrdinalIgnoreCase));
            var charClass = settings?.CharacterClass ?? "Warrior";

            _logger.LogInformation("DUNGEON_COORD: Preparing {Account} ({CharName}, {Class}): level 8 + spells + gear",
                account, charName, charClass);

            // Clear instance binds so RFC can be reset freely
            await _soapClient.ExecuteGMCommandAsync($".instance unbind all {charName}");

            // Set level to 8
            await _soapClient.ExecuteGMCommandAsync($".character level {charName} 8");

            // Learn all class spells up to level 8
            await _soapClient.ExecuteGMCommandAsync($".learn all_myclass {charName}");

            // Learn specific spells that .learn all_myclass may miss
            if (Level8KeySpells.TryGetValue(charClass, out var spells))
            {
                foreach (var spellId in spells)
                    await _soapClient.ExecuteGMCommandAsync($".learn {spellId} {charName}");
            }

            // Class-specific quest items and abilities
            await PrepareClassSpecific(charName, charClass);

            // Reset items to clean slate, then equip class-appropriate gear
            await _soapClient.ExecuteGMCommandAsync($".reset items {charName}");
            await AddStarterGear(charName, charClass);

            _logger.LogInformation("DUNGEON_COORD: {Account} ({CharName}) preparation complete.", account, charName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DUNGEON_COORD: Failed to prepare {Account}: {Error}", account, ex.Message);
        }
    }

    private async Task PrepareClassSpecific(string charName, string charClass)
    {
        if (_soapClient == null) return;

        switch (charClass.ToLowerInvariant())
        {
            case "shaman":
                // Earth Totem is a quest reward item required for totem spells
                // (handled in gear table, but also .learn the totem quests just in case)
                break;
            case "warlock":
                // Summon Imp should be learned by .learn all_myclass
                // Give Soul Shards for summoning
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 6265 10"); // Soul Shard x10
                break;
            case "hunter":
                // Tame Beast is level 10 — not available at 8
                // Hunters need ammo (handled in gear table)
                break;
        }
    }

    private async Task AddStarterGear(string charName, string charClass)
    {
        if (_soapClient == null) return;

        if (!Level8Gear.TryGetValue(charClass, out var gearList))
            return;

        foreach (var (itemId, name) in gearList)
        {
            // Hunter arrows get quantity 200
            if (itemId == 2512) // Rough Arrow
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} {itemId} 200");
            else
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} {itemId}");
        }
    }

    private ActionMessage? HandleEquipGear(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Each bot gets one EquipItem action per tick for each gear piece.
        // The bot's BuildEquipItemByIdSequence sends CMSG_AUTOEQUIP_ITEM for all backpack slots.
        var settings = _allSettings.FirstOrDefault(s =>
            s.AccountName.Equals(requestingAccount, StringComparison.OrdinalIgnoreCase));
        var charClass = settings?.CharacterClass ?? "Warrior";

        if (!Level8Gear.TryGetValue(charClass, out var gearList))
        {
            _equipItemIndex.TryAdd(requestingAccount, gearList?.Length ?? 0);
        }
        else
        {
            var idx = _equipItemIndex.GetOrAdd(requestingAccount, 0);
            if (idx < gearList.Length)
            {
                var (itemId, name) = gearList[idx];
                _equipItemIndex[requestingAccount] = idx + 1;

                // Skip ammo (arrows/bullets) — they don't need equipping
                if (itemId == 2512) // Rough Arrow
                    return null;

                _logger.LogInformation("DUNGEON_COORD: {Account} equipping {Item} ({ItemId}) [{Idx}/{Total}]",
                    requestingAccount, name, itemId, idx + 1, gearList.Length);

                var action = new ActionMessage { ActionType = ActionType.EquipItem };
                action.Parameters.Add(new RequestParameter { IntParam = (int)itemId });
                return action;
            }
        }

        // Check if all accounts have finished equipping
        var allAccounts = new List<string> { _leaderAccount };
        allAccounts.AddRange(_memberAccounts);

        var allEquipped = allAccounts.All(a =>
        {
            if (!_equipItemIndex.TryGetValue(a, out var idx)) return false;
            var aSettings = _allSettings.FirstOrDefault(s =>
                s.AccountName.Equals(a, StringComparison.OrdinalIgnoreCase));
            var aClass = aSettings?.CharacterClass ?? "Warrior";
            if (!Level8Gear.TryGetValue(aClass, out var aGear)) return true;
            return idx >= aGear.Length;
        });

        if (allEquipped)
        {
            _logger.LogInformation("DUNGEON_COORD: All bots equipped. Teleporting to Orgrimmar.");
            TransitionTo(CoordState.TeleportToOrgrimmar);
        }

        return null;
    }

    private ActionMessage? HandleTeleportToOrgrimmar(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Each bot types .go xyz to teleport to Orgrimmar
        if (_teleportedToOrg.TryAdd(requestingAccount, 0))
        {
            _logger.LogInformation("DUNGEON_COORD: Teleporting {Account} to Orgrimmar", requestingAccount);
            return MakeSendChatAction($".go xyz {OrgX:0.#} {OrgY:0.#} {OrgZ:0.#} 1");
        }

        // Check if all bots have been teleported
        var allAccounts = new List<string> { _leaderAccount };
        allAccounts.AddRange(_memberAccounts);
        var allTeleported = allAccounts.All(a => _teleportedToOrg.ContainsKey(a));

        if (allTeleported)
        {
            _logger.LogInformation("DUNGEON_COORD: All bots teleported to Orgrimmar. Waiting to settle.");
            TransitionTo(CoordState.WaitForTeleportSettle);
        }

        return null;
    }

    private ActionMessage? HandleWaitForSettle(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots,
        CoordState nextState, int expectedMapId)
    {
        _tickCount++;
        // Wait ~5 seconds (10 ticks) for teleport to settle
        if (_tickCount < 10)
            return null;

        // Verify at least some bots are on the expected map
        var onMap = snapshots.Values.Count(s =>
        {
            var mapId = s.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            return mapId == expectedMapId;
        });

        _logger.LogInformation("DUNGEON_COORD: Settle check: {OnMap}/{Total} bots on map {Map}",
            onMap, snapshots.Count, expectedMapId);

        // Proceed even if not all are on target map (teleport might still be propagating)
        if (_tickCount >= 20 || onMap >= 2)
        {
            if (nextState == CoordState.FormGroup_Inviting)
                _inviteIndex = 0;
            TransitionTo(nextState);
        }

        return null;
    }

    private ActionMessage? HandleLeaveOldGroups(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Each bot leaves any stale group and declines pending invites
        if (_leftOldGroup.TryAdd(requestingAccount, 0))
        {
            _logger.LogInformation("DUNGEON_COORD: {Account} leaving old group + declining pending invites", requestingAccount);
            // LeaveGroup is a no-op if not in a group; DeclineGroupInvite is a no-op if no pending invite
            return MakeAction(ActionType.LeaveGroup);
        }

        // Check if all bots have left
        var allAccounts = new List<string> { _leaderAccount };
        allAccounts.AddRange(_memberAccounts);
        if (!allAccounts.All(a => _leftOldGroup.ContainsKey(a)))
            return null;

        // Wait a few ticks for leave to propagate on the server
        _tickCount++;
        if (_tickCount < 6)
            return null;

        _logger.LogInformation("DUNGEON_COORD: All bots left old groups. Starting invite phase.");
        _inviteIndex = 0;
        TransitionTo(CoordState.FormGroup_Inviting);
        return null;
    }

    /// <summary>
    /// Invite+Accept one member at a time: leader invites → member accepts → verify grouped → next.
    /// MaNGOS only allows one outstanding invite per group, so we must serialize.
    /// Phase: _inviteIndex tracks which member we're working on. Within each member:
    ///   _tickCount 0: send invite (leader action)
    ///   _tickCount 1+: send accept (member action), then wait for PartyLeaderGuid
    /// </summary>
    private enum InvitePhase { SendInvite, SendAccept, WaitForGrouped }
    private volatile int _currentInvitePhaseInt; // 0=SendInvite, 1=SendAccept, 2=WaitForGrouped
    private InvitePhase CurrentInvitePhase
    {
        get => (InvitePhase)_currentInvitePhaseInt;
        set => _currentInvitePhaseInt = (int)value;
    }

    private ActionMessage? HandleInviting(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        var inviteLimit = Math.Min(PARTY_SIZE_LIMIT, _memberAccounts.Count);
        if (_inviteIndex >= inviteLimit)
        {
            // All batch 1 members processed — move to raid conversion or verify
            if (_memberAccounts.Count > PARTY_SIZE_LIMIT)
            {
                var grouped = _memberAccounts.Take(inviteLimit).Count(m =>
                    snapshots.TryGetValue(m, out var s) && s.PartyLeaderGuid != 0);
                _logger.LogInformation("DUNGEON_COORD: Batch 1 complete: {Grouped}/{Limit} grouped. Converting to raid.",
                    grouped, inviteLimit);
                TransitionTo(CoordState.FormGroup_ConvertToRaid);
            }
            else
            {
                TransitionTo(CoordState.FormGroup_Verify);
            }
            return null;
        }

        var memberAccount = _memberAccounts[_inviteIndex];

        switch (CurrentInvitePhase)
        {
            case InvitePhase.SendInvite:
                // Only leader sends invite
                if (!requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase))
                    return null;

                if (_accountToCharName.TryGetValue(memberAccount, out var charName)
                    && snapshots.TryGetValue(memberAccount, out var snap)
                    && snap.ScreenState == "InWorld")
                {
                    _logger.LogInformation("DUNGEON_COORD: Leader inviting '{CharName}' ({Account}) [{Idx}/{Limit}]",
                        charName, memberAccount, _inviteIndex + 1, inviteLimit);
                    CurrentInvitePhase = InvitePhase.SendAccept;
                    _phaseStartedAt = DateTime.UtcNow;
                    return MakeAction(ActionType.SendGroupInvite, charName);
                }
                // Skip if not in world
                _inviteIndex++;
                return null;

            case InvitePhase.SendAccept:
                // Wait 2s for invite to arrive at the member
                if ((DateTime.UtcNow - _phaseStartedAt).TotalSeconds < 2.0)
                    return null;

                // Only the target member can accept
                if (!requestingAccount.Equals(memberAccount, StringComparison.OrdinalIgnoreCase))
                    return null;

                _logger.LogInformation("DUNGEON_COORD: '{Account}' accepting invite.", memberAccount);
                CurrentInvitePhase = InvitePhase.WaitForGrouped;
                _phaseStartedAt = DateTime.UtcNow;
                return MakeAction(ActionType.AcceptGroupInvite);

            case InvitePhase.WaitForGrouped:
                // Atomic transition: only ONE thread advances the invite index
                if (Interlocked.CompareExchange(ref _currentInvitePhaseInt,
                        (int)InvitePhase.SendInvite, (int)InvitePhase.WaitForGrouped) != (int)InvitePhase.WaitForGrouped)
                    return null; // Another thread already claimed this transition

                var isGrouped = snapshots.TryGetValue(memberAccount, out var memberSnap) && memberSnap.PartyLeaderGuid != 0;
                var waitElapsed = (DateTime.UtcNow - _phaseStartedAt).TotalSeconds;
                if (isGrouped || waitElapsed > 5.0) // Wait up to 5s
                {
                    if (isGrouped)
                        _logger.LogInformation("DUNGEON_COORD: '{Account}' confirmed grouped.", memberAccount);
                    else
                        _logger.LogWarning("DUNGEON_COORD: '{Account}' not showing as grouped after timeout, continuing.", memberAccount);

                    _inviteIndex++;
                    _tickCount = 0;
                    // Phase already set to SendInvite by CompareExchange above
                }
                else
                {
                    // Not ready yet — restore WaitForGrouped so we check again next tick
                    CurrentInvitePhase = InvitePhase.WaitForGrouped;
                }
                return null;
        }

        return null;
    }

    /// <summary>
    /// HandleAccepting is no longer used — invite+accept is merged into HandleInviting.
    /// Kept as a no-op redirect to FormGroup_Verify for safety.
    /// </summary>
    private ActionMessage? HandleAccepting(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        TransitionTo(CoordState.FormGroup_Verify);
        return null;
    }

    private ActionMessage? HandleConvertToRaid(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Only leader can convert to raid
        if (!requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!_raidConvertSent)
        {
            _raidConvertSent = true;
            _logger.LogInformation("DUNGEON_COORD: Leader converting party to raid.");
            return MakeAction(ActionType.ConvertToRaid);
        }

        // Wait for the conversion to take effect — verify batch 1 members still grouped
        _tickCount++;
        var grouped = _memberAccounts.Take(PARTY_SIZE_LIMIT).Count(m =>
            snapshots.TryGetValue(m, out var s) && s.PartyLeaderGuid != 0);

        if (_tickCount < 15 && grouped < PARTY_SIZE_LIMIT)
        {
            if (_tickCount % 5 == 0)
                _logger.LogInformation("DUNGEON_COORD: Waiting for raid conversion: {Grouped}/{Expected} batch 1 members still grouped",
                    grouped, PARTY_SIZE_LIMIT);
            return null;
        }

        _logger.LogInformation("DUNGEON_COORD: Raid conversion done ({Grouped}/{Expected} batch 1 grouped). Inviting remaining {Count} members.",
            grouped, PARTY_SIZE_LIMIT, _memberAccounts.Count - PARTY_SIZE_LIMIT);
        _inviteIndex = PARTY_SIZE_LIMIT; // Start from where we left off
        TransitionTo(CoordState.FormGroup_InvitingRest);
        return null;
    }

    /// <summary>
    /// Invite+Accept remaining members (after raid conversion), one at a time.
    /// Same serialized pattern as HandleInviting.
    /// </summary>
    private ActionMessage? HandleInvitingRest(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        if (_inviteIndex >= _memberAccounts.Count)
        {
            TransitionTo(CoordState.FormGroup_Verify);
            return null;
        }

        var memberAccount = _memberAccounts[_inviteIndex];

        switch (CurrentInvitePhase)
        {
            case InvitePhase.SendInvite:
                if (!requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase))
                    return null;

                if (_accountToCharName.TryGetValue(memberAccount, out var charName)
                    && snapshots.TryGetValue(memberAccount, out var snap)
                    && snap.ScreenState == "InWorld")
                {
                    _logger.LogInformation("DUNGEON_COORD: Leader inviting '{CharName}' ({Account}) [batch 2, {Idx}/{Total}]",
                        charName, memberAccount, _inviteIndex - PARTY_SIZE_LIMIT + 1, _memberAccounts.Count - PARTY_SIZE_LIMIT);
                    CurrentInvitePhase = InvitePhase.SendAccept;
                    _phaseStartedAt = DateTime.UtcNow;
                    return MakeAction(ActionType.SendGroupInvite, charName);
                }
                _inviteIndex++;
                return null;

            case InvitePhase.SendAccept:
                if ((DateTime.UtcNow - _phaseStartedAt).TotalSeconds < 2.0)
                    return null;

                if (!requestingAccount.Equals(memberAccount, StringComparison.OrdinalIgnoreCase))
                    return null;

                _logger.LogInformation("DUNGEON_COORD: '{Account}' accepting invite [batch 2].", memberAccount);
                CurrentInvitePhase = InvitePhase.WaitForGrouped;
                _phaseStartedAt = DateTime.UtcNow;
                return MakeAction(ActionType.AcceptGroupInvite);

            case InvitePhase.WaitForGrouped:
                // Atomic transition: only ONE thread advances the invite index
                if (Interlocked.CompareExchange(ref _currentInvitePhaseInt,
                        (int)InvitePhase.SendInvite, (int)InvitePhase.WaitForGrouped) != (int)InvitePhase.WaitForGrouped)
                    return null; // Another thread already claimed this transition

                var isGrouped = snapshots.TryGetValue(memberAccount, out var memberSnap) && memberSnap.PartyLeaderGuid != 0;
                var waitElapsed = (DateTime.UtcNow - _phaseStartedAt).TotalSeconds;
                if (isGrouped || waitElapsed > 5.0)
                {
                    if (isGrouped)
                        _logger.LogInformation("DUNGEON_COORD: '{Account}' confirmed grouped [batch 2].", memberAccount);
                    else
                        _logger.LogWarning("DUNGEON_COORD: '{Account}' not showing as grouped after {Elapsed:F1}s [batch 2], continuing.", memberAccount, waitElapsed);

                    _inviteIndex++;
                    // Phase already set to SendInvite by CompareExchange above
                }
                else
                {
                    // Not ready yet — restore WaitForGrouped so we check again next tick
                    CurrentInvitePhase = InvitePhase.WaitForGrouped;
                }
                return null;
        }

        return null;
    }

    /// <summary>
    /// HandleAcceptingRest is no longer used — merged into HandleInvitingRest.
    /// </summary>
    private ActionMessage? HandleAcceptingRest(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        TransitionTo(CoordState.FormGroup_Verify);
        return null;
    }

    private ActionMessage? HandleVerify(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        _tickCount++;
        if (_tickCount < 10)
            return null;

        var grouped = snapshots.Values.Count(s => s.PartyLeaderGuid != 0);
        _logger.LogInformation("DUNGEON_COORD: Group verify: {Grouped}/{Total} bots grouped.",
            grouped, snapshots.Count);

        TransitionTo(CoordState.OrganizeRaidSubgroups);
        return null;
    }

    /// <summary>
    /// Organize raid subgroups so duplicate classes are in separate groups.
    /// This ensures party-wide buffs (Battle Shout, Power Word: Fortitude, etc.) cover more players.
    /// Algorithm: round-robin classes across subgroups — each instance of a class goes to the next subgroup.
    /// </summary>
    private ActionMessage? HandleOrganizeRaidSubgroups(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Only the leader sends subgroup change commands
        if (!requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase))
            return null;

        // Step 1: Compute subgroup assignments (once)
        if (!_subgroupsComputed)
        {
            ComputeSubgroupAssignments(snapshots);
            _subgroupsComputed = true;
            _subgroupAssignIndex = 0;
        }

        // Step 2: Send one ChangeRaidSubgroup per tick (throttled)
        _tickCount++;
        if (_tickCount % 3 != 1) // ~1.5s between each move
            return null;

        var assignments = _subgroupAssignments.ToArray();
        if (_subgroupAssignIndex >= assignments.Length)
        {
            _logger.LogInformation("DUNGEON_COORD: All {Count} subgroup assignments sent. Teleporting to RFC.",
                assignments.Length);
            TransitionTo(CoordState.TeleportToRFC);
            return null;
        }

        var (charName, subgroup) = assignments[_subgroupAssignIndex];
        _subgroupAssignIndex++;

        _logger.LogInformation("DUNGEON_COORD: Moving '{CharName}' to subgroup {SubGroup} [{Idx}/{Total}]",
            charName, subgroup, _subgroupAssignIndex, assignments.Length);

        var action = new ActionMessage { ActionType = ActionType.ChangeRaidSubgroup };
        action.Parameters.Add(new RequestParameter { StringParam = charName });
        action.Parameters.Add(new RequestParameter { IntParam = subgroup });
        return action;
    }

    /// <summary>
    /// Compute optimal subgroup assignments. Strategy:
    /// - Group classes that share party-wide buffs into different subgroups
    /// - Warriors spread across groups (Battle Shout is party-only)
    /// - Priests spread across groups (Power Word: Fortitude, Shadow Protection)
    /// - Shamans spread across groups (totems are party-only)
    /// - Healers spread across groups for healing coverage
    /// - Fill subgroups round-robin by class to maximize buff diversity per group
    /// </summary>
    private void ComputeSubgroupAssignments(ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Build (charName, class) pairs from settings + snapshots
        var members = new List<(string CharName, string Class, string Account)>();
        foreach (var setting in _allSettings)
        {
            if (_accountToCharName.TryGetValue(setting.AccountName, out var charName)
                && snapshots.TryGetValue(setting.AccountName, out var snap)
                && snap.PartyLeaderGuid != 0) // Only grouped members
            {
                members.Add((charName, setting.CharacterClass, setting.AccountName));
            }
        }

        if (members.Count <= 5)
        {
            _logger.LogInformation("DUNGEON_COORD: Only {Count} grouped members — single party, no subgroup changes needed.", members.Count);
            return; // All in subgroup 0 by default
        }

        // Group by class, then round-robin each class instance across subgroups
        var classBuckets = members.GroupBy(m => m.Class, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count()) // Spread most-duplicated classes first
            .ToList();

        // Track how many members are in each subgroup (max 5 per subgroup in a 40-man raid)
        var subgroupCounts = new int[8];
        const int MaxPerSubgroup = 5;
        var numSubgroups = Math.Max(2, (int)Math.Ceiling(members.Count / 5.0));

        foreach (var classGroup in classBuckets)
        {
            var classMembers = classGroup.ToList();
            for (int i = 0; i < classMembers.Count; i++)
            {
                // Find the subgroup with fewest members that this class hasn't been placed in yet
                var usedSubgroups = _subgroupAssignments
                    .Where(kvp => classMembers.Any(cm => cm.CharName == kvp.Key))
                    .Select(kvp => (int)kvp.Value)
                    .ToHashSet();

                byte bestSubgroup = 0;
                int bestCount = int.MaxValue;
                for (byte sg = 0; sg < numSubgroups; sg++)
                {
                    if (subgroupCounts[sg] < MaxPerSubgroup && subgroupCounts[sg] < bestCount
                        && !usedSubgroups.Contains(sg))
                    {
                        bestSubgroup = sg;
                        bestCount = subgroupCounts[sg];
                    }
                }
                // Fallback: if all subgroups have this class, pick least-full
                if (bestCount == int.MaxValue)
                {
                    for (byte sg = 0; sg < numSubgroups; sg++)
                    {
                        if (subgroupCounts[sg] < MaxPerSubgroup && subgroupCounts[sg] < bestCount)
                        {
                            bestSubgroup = sg;
                            bestCount = subgroupCounts[sg];
                        }
                    }
                }

                _subgroupAssignments[classMembers[i].CharName] = bestSubgroup;
                subgroupCounts[bestSubgroup]++;

                _logger.LogInformation("DUNGEON_COORD: Subgroup plan: {CharName} ({Class}) → group {SubGroup}",
                    classMembers[i].CharName, classMembers[i].Class, bestSubgroup);
            }
        }
    }

    /// <summary>
    /// Staggered RFC teleport: leader first, then one BG bot every 2 seconds.
    /// Prevents the FG WoW.exe crash caused by a flood of SMSG_DESTROY_OBJECT
    /// packets when 9 bots teleport away simultaneously.
    /// </summary>
    private ActionMessage? HandleTeleportToRFC(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Build teleport order: LEADER FIRST (creates the instance), then members.
        // FG bots go last since they can't reliably handle cross-map instance transfers.
        if (_rfcTeleportOrder.Count == 0)
        {
            _rfcTeleportOrder.Add(_leaderAccount); // Leader first — creates the instance
            // BG members before FG members
            var bgMembers = _memberAccounts.Where(a => !_allSettings.Any(s =>
                s.AccountName.Equals(a, StringComparison.OrdinalIgnoreCase)
                && s.RunnerType == Settings.BotRunnerType.Foreground));
            var fgMembers = _memberAccounts.Where(a => _allSettings.Any(s =>
                s.AccountName.Equals(a, StringComparison.OrdinalIgnoreCase)
                && s.RunnerType == Settings.BotRunnerType.Foreground));
            _rfcTeleportOrder.AddRange(bgMembers);
            _rfcTeleportOrder.AddRange(fgMembers); // FG bots last
            _rfcTeleportIndex = 0;
        }

        // All teleported?
        if (_rfcTeleportIndex >= _rfcTeleportOrder.Count)
        {
            _logger.LogInformation("DUNGEON_COORD: All bots teleported to RFC. Waiting to settle.");
            TransitionTo(CoordState.WaitForRFCSettle);
            return null;
        }

        // Stagger: wait RFC_TELEPORT_STAGGER_SEC between each teleport
        var sinceLast = (DateTime.UtcNow - _lastRfcTeleportAt).TotalSeconds;
        if (sinceLast < RFC_TELEPORT_STAGGER_SEC)
            return null;

        // Only the next bot in the order gets teleported
        var nextAccount = _rfcTeleportOrder[_rfcTeleportIndex];
        if (!requestingAccount.Equals(nextAccount, StringComparison.OrdinalIgnoreCase))
            return null; // Not this bot's turn

        _teleportedToRFC.TryAdd(requestingAccount, 0);
        _rfcTeleportIndex++;
        _lastRfcTeleportAt = DateTime.UtcNow;

        _logger.LogInformation("DUNGEON_COORD: Teleporting {Account} to RFC (map {Map}) [{Idx}/{Total}]",
            requestingAccount, RfcMapId, _rfcTeleportIndex, _rfcTeleportOrder.Count);

        // FG bots: use SOAP .tele to avoid FG client cross-map transfer issues.
        // BG bots: use bot chat .go xyz (faster, no SOAP roundtrip needed).
        var isFgBot = _allSettings.Any(s =>
            s.AccountName.Equals(requestingAccount, StringComparison.OrdinalIgnoreCase)
            && s.RunnerType == Settings.BotRunnerType.Foreground);
        if (isFgBot && _soapClient != null)
        {
            _accountToCharName.TryGetValue(requestingAccount, out var charName);
            if (charName != null)
            {
                _logger.LogInformation("DUNGEON_COORD: Using SOAP .tele for FG bot '{Char}'", charName);
                _ = _soapClient.ExecuteGMCommandAsync($".tele name {charName} rfc");
                return null; // SOAP handles the teleport server-side; no action needed from the bot
            }
        }

        return MakeSendChatAction($".go xyz {RfcStartX:0.#} {RfcStartY:0.#} {RfcStartZ:0.#} {RfcMapId}");
    }

    private ActionMessage? HandleDispatchDungeoneering(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        bool isLeader = requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase);

        _dungeoneeringDispatched.TryAdd(requestingAccount, 0);
        _logger.LogInformation("DUNGEON_COORD: Dispatching START_DUNGEONEERING to '{Account}' (leader={IsLeader}) [{Dispatched}/{Total}]",
            requestingAccount, isLeader, _dungeoneeringDispatched.Count, snapshots.Count);

        var action = new ActionMessage { ActionType = ActionType.StartDungeoneering };
        action.Parameters.Add(new RequestParameter { IntParam = isLeader ? 1 : 0 });

        _tickCount++;
        // Only transition when EVERY account (including leader) has been dispatched at least once.
        // Fallback: if leader hasn't polled after 30 ticks, transition anyway to avoid deadlock.
        var allDispatched = _dungeoneeringDispatched.Count >= snapshots.Count
                            && _dungeoneeringDispatched.ContainsKey(_leaderAccount);
        if (allDispatched || _tickCount > 30)
        {
            if (!_dungeoneeringDispatched.ContainsKey(_leaderAccount))
                _logger.LogWarning("DUNGEON_COORD: Leader '{Leader}' never polled during DispatchDungeoneering — forcing transition", _leaderAccount);
            TransitionTo(CoordState.DungeonInProgress);
        }

        return action;
    }

    private ActionMessage? HandleDungeonInProgress(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // --- Leader failover: if leader drops, disconnects, or can't enter the dungeon map ---
        bool leaderAlive = snapshots.TryGetValue(_leaderAccount, out var leaderSnap)
                           && leaderSnap.ScreenState == "InWorld";
        bool leaderOnDungeonMap = leaderAlive
                           && (leaderSnap!.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == RfcMapId;
        if (leaderOnDungeonMap)
        {
            _leaderLastSeen = DateTime.UtcNow;
        }
        else if (!_leaderFailoverTriggered
                 && (DateTime.UtcNow - _leaderLastSeen).TotalSeconds > LEADER_FAILOVER_TIMEOUT_SEC)
        {
            // Find first BG bot that's alive and on the RFC map
            var newLeader = _memberAccounts.FirstOrDefault(a =>
                snapshots.TryGetValue(a, out var s) && s.ScreenState == "InWorld"
                && (s.Player?.Unit?.GameObject?.Base?.MapId ?? 0) == RfcMapId);
            if (newLeader != null)
            {
                var reason = leaderAlive ? "not on dungeon map" : "no snapshot";
                _logger.LogWarning("DUNGEON_COORD: Leader '{OldLeader}' lost ({Reason} for {Timeout}s). Promoting '{NewLeader}' to leader.",
                    _leaderAccount, reason, LEADER_FAILOVER_TIMEOUT_SEC, newLeader);
                _leaderAccount = newLeader;
                _leaderFailoverTriggered = true;
                _leaderLastSeen = DateTime.UtcNow;
                // Clear dispatch tracking so the new leader gets re-dispatched as leader
                _dungeoneeringDispatched.TryRemove(newLeader, out _);
            }
        }

        // If the leader missed the DispatchDungeoneering window (FG bot polls slower),
        // or was just promoted via failover, send them StartDungeoneering now.
        if (requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase))
        {
            if (_dungeoneeringDispatched.TryAdd(requestingAccount, 0))
            {
                _logger.LogInformation("DUNGEON_COORD: Dispatching START_DUNGEONEERING to leader '{Leader}' (failover={Failover})",
                    _leaderAccount, _leaderFailoverTriggered);
                var action = new ActionMessage { ActionType = ActionType.StartDungeoneering };
                action.Parameters.Add(new RequestParameter { IntParam = 1 }); // isLeader = true
                return action;
            }
            return null; // Leader already dispatched — no heal/DPS overlay for leader
        }

        if (!snapshots.TryGetValue(_leaderAccount, out leaderSnap))
            return null;

        if (!snapshots.TryGetValue(requestingAccount, out var mySnap))
            return null;

        if (!_spellsResolved.ContainsKey(requestingAccount))
            ResolveSpells(requestingAccount, mySnap);

        if (!IsInCombat(leaderSnap))
            return null;

        if (!CanAct(requestingAccount, ACTION_COOLDOWN_SEC))
            return null;

        // Priority 1: Heal if any party member is below threshold
        if (_healSpells.TryGetValue(requestingAccount, out var healSpell) && healSpell != 0)
        {
            var lowestHpAccount = FindLowestHpMember(snapshots);
            if (lowestHpAccount != null && snapshots.TryGetValue(lowestHpAccount, out var lowestHpSnap))
            {
                var hpRatio = GetHealthRatio(lowestHpSnap);
                if (hpRatio < HEAL_THRESHOLD)
                {
                    var targetGuid = GetPlayerGuid(lowestHpSnap);
                    _logger.LogInformation("DUNGEON_COORD: {Account} healing {Target} (HP={Hp:P0})",
                        requestingAccount, lowestHpAccount, hpRatio);
                    _lastActionSent[requestingAccount] = DateTime.UtcNow;
                    return MakeCastSpellAction((int)healSpell, (long)targetGuid);
                }
            }
        }

        // Priority 2: DPS — assist the leader's target
        if (_damageSpells.TryGetValue(requestingAccount, out var dpsSpell) && dpsSpell != 0)
        {
            var leaderTargetGuid = GetTargetGuid(leaderSnap);
            if (leaderTargetGuid != 0)
            {
                _logger.LogInformation("DUNGEON_COORD: {Account} DPS on leader's target 0x{Target:X} (spell {Spell})",
                    requestingAccount, leaderTargetGuid, dpsSpell);
                _lastActionSent[requestingAccount] = DateTime.UtcNow;
                return MakeCastSpellAction((int)dpsSpell, (long)leaderTargetGuid);
            }
        }

        return null;
    }

    // ===== Helpers =====

    private void ResolveSpells(string account, WoWActivitySnapshot snap)
    {
        var spellList = snap.Player?.SpellList;
        if (spellList == null || spellList.Count == 0) return;

        var healSpells = new Dictionary<string, uint[]>
        {
            ["HealingTouch"] = [5185, 5186, 5187, 5188, 5189],
            ["Rejuvenation"] = [774, 1058, 1430, 2090, 2091],
            ["LesserHeal"] = [2050, 2052, 2053],
            ["Heal"] = [2054, 2055, 6063, 6064],
            ["HealingWave"] = [331, 332, 547, 913, 939],
            ["LesserHealingWave"] = [8004, 8008, 8010],
        };

        foreach (var category in healSpells)
        {
            var best = category.Value.Reverse().FirstOrDefault(id => spellList.Contains(id));
            if (best != 0)
            {
                _healSpells[account] = best;
                break;
            }
        }

        var dpsSpells = new Dictionary<string, uint[]>
        {
            ["LightningBolt"] = [403, 529, 548, 915, 943],
            ["Smite"] = [585, 591, 598, 984, 1004],
            ["Wrath"] = [5176, 5177, 5178, 5179, 5180],
            ["ShadowBolt"] = [686, 695, 705, 1088, 1106],
            ["Fireball"] = [133, 143, 145, 3140, 8400],
        };

        foreach (var category in dpsSpells)
        {
            var best = category.Value.Reverse().FirstOrDefault(id => spellList.Contains(id));
            if (best != 0)
            {
                _damageSpells[account] = best;
                break;
            }
        }

        _spellsResolved.TryAdd(account, 0);
        _logger.LogInformation("DUNGEON_COORD: Resolved spells for {Account}: heal={Heal}, dps={Dps}",
            account, _healSpells.GetValueOrDefault(account), _damageSpells.GetValueOrDefault(account));
    }

    private string? FindLowestHpMember(ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        string? lowest = null;
        float lowestHp = 1f;

        foreach (var kvp in snapshots)
        {
            var hp = GetHealthRatio(kvp.Value);
            if (hp < lowestHp && hp > 0.01f)
            {
                lowestHp = hp;
                lowest = kvp.Key;
            }
        }

        return lowest;
    }

    private bool CanAct(string account, double cooldownSec)
    {
        if (!_lastActionSent.TryGetValue(account, out var lastSent))
            return true;
        return (DateTime.UtcNow - lastSent).TotalSeconds >= cooldownSec;
    }

    private static bool IsInCombat(WoWActivitySnapshot snapshot)
    {
        var unitFlags = snapshot.Player?.Unit?.UnitFlags ?? 0;
        return (unitFlags & 0x80000) != 0; // UNIT_FLAG_IN_COMBAT
    }

    private static ulong GetPlayerGuid(WoWActivitySnapshot snapshot)
        => snapshot.Player?.Unit?.GameObject?.Base?.Guid ?? 0;

    private static ulong GetTargetGuid(WoWActivitySnapshot snapshot)
        => snapshot.Player?.Unit?.TargetGuid ?? 0;

    private static float GetHealthRatio(WoWActivitySnapshot snapshot)
    {
        var health = snapshot.Player?.Unit?.Health ?? 0;
        var maxHealth = snapshot.Player?.Unit?.MaxHealth ?? 1;
        return maxHealth > 0 ? (float)health / maxHealth : 1f;
    }

    private static ActionMessage MakeAction(ActionType actionType, string? stringParam = null)
    {
        var action = new ActionMessage { ActionType = actionType };
        if (stringParam != null)
            action.Parameters.Add(new RequestParameter { StringParam = stringParam });
        return action;
    }

    private static ActionMessage MakeSendChatAction(string chatMessage)
    {
        var action = new ActionMessage { ActionType = ActionType.SendChat };
        action.Parameters.Add(new RequestParameter { StringParam = chatMessage });
        return action;
    }

    private static ActionMessage MakeCastSpellAction(int spellId, long targetGuid)
    {
        var action = new ActionMessage { ActionType = ActionType.CastSpell };
        action.Parameters.Add(new RequestParameter { IntParam = spellId });
        action.Parameters.Add(new RequestParameter { LongParam = targetGuid });
        return action;
    }
}
