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
///   WaitingForBots → TeleportToOrgrimmar → WaitForOrgSettle → DisbandAndReset →
///   PrepareCharacters → LearnSpellsViaChat → AddItemsViaChat → EquipGear →
///   FormGroup → TeleportToRFC → DispatchDungeoneering → DungeonInProgress
///
/// TeleportToOrgrimmar: FIRST move — all bots .go xyz to Orgrimmar safe zone
/// DisbandAndReset: Leave old groups/raids, clean slate before prep
/// PrepareCharacters: SOAP-based level set + instance unbind (charName-based commands only)
/// LearnSpellsViaChat: Bot chat .targetself → .reset spells/talents/items → .learn <spellId> → .setskill
/// AddItemsViaChat: Bot chat .additem <itemId> for each gear piece
/// EquipGear: Send EquipItem actions so bots auto-equip gear from backpack
/// FormGroup: Leader invites, members accept, convert to raid, verify
/// TeleportToRFC: Bot chat .go xyz into RFC interior (map 389)
/// DispatchDungeoneering: START_DUNGEONEERING to all bots
/// DungeonInProgress: Heal/DPS support overlay for members
/// </summary>
public class DungeoneeringCoordinator
{
    public enum CoordState
    {
        WaitingForBots,
        TeleportToOrgrimmar,        // FIRST: get everyone to same zone
        WaitForOrgSettle,           // Wait for Org teleports to complete
        DisbandAndReset,            // Leave old groups/raids, clean slate
        PrepareCharacters,          // SOAP: level set + instance unbind
        LearnSpellsViaChat,         // .reset spells/talents/items + .learn
        AddItemsViaChat,            // .additem for gear
        EquipGear,                  // CMSG_AUTOEQUIP_ITEM
        FormGroup_Inviting,         // Invite first 4 (party cap = 5 incl leader)
        FormGroup_Accepting,        // First 4 accept (unused — merged into Inviting)
        FormGroup_ConvertToRaid,    // Leader converts party → raid
        FormGroup_InvitingRest,     // Invite remaining members
        FormGroup_AcceptingRest,    // Remaining members accept (unused — merged into InvitingRest)
        FormGroup_Verify,
        TeleportToRFC,
        WaitForRFCSettle,
        DispatchDungeoneering,
        DungeonInProgress,
    }

    private readonly ILogger _logger;
    private readonly MangosSOAPClient? _soapClient;
    private readonly string _leaderAccount;
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
    private readonly ConcurrentDictionary<string, Task> _prepTasks = new();
    private readonly ConcurrentDictionary<string, byte> _teleportedToOrg = new();
    private readonly ConcurrentDictionary<string, byte> _teleportedToRFC = new();
    private readonly List<string> _rfcTeleportOrder = new(); // Built once: leader first, then members
    private int _rfcTeleportIndex;
    private DateTime _lastRfcTeleportAt = DateTime.MinValue;
    private const double RFC_TELEPORT_STAGGER_SEC = 1.0; // Seconds between each bot's teleport (FG crash fixed, 1s is safe)

    // Chat-based spell/item tracking — per-bot command index into the chat command sequence
    private readonly ConcurrentDictionary<string, int> _chatSpellIndex = new(); // account → next spell command index
    private readonly ConcurrentDictionary<string, int> _chatItemIndex = new();  // account → next item command index

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
    private const double ACTION_COOLDOWN_SEC = 1.5;
    private const float HEAL_THRESHOLD = 0.50f;

    // Cached healer spell IDs — concurrent for multi-thread safety
    private readonly ConcurrentDictionary<string, uint> _healSpells = new();
    private readonly ConcurrentDictionary<string, uint> _damageSpells = new();
    private readonly ConcurrentDictionary<string, byte> _spellsResolved = new();

    // Raid subgroup organization
    private readonly ConcurrentDictionary<string, byte> _subgroupAssignments = new(); // charName → subgroup (0-7)
    private int _subgroupAssignIndex;
    private bool _subgroupsComputed;

    // Teleport throttle — prevent spamming .go xyz to bots mid-loading (causes crashes)
    private readonly ConcurrentDictionary<string, DateTime> _lastTeleportSent = new();
    private const double TELEPORT_COOLDOWN_SEC = 20.0; // Cross-map load can take 15s+

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
        var inWorldCount = snapshots.Count(s => s.Value.IsObjectManagerValid);
        if (inWorldCount < 2)
            return null;

        // Timeout protection for group formation states
        var elapsed = (DateTime.UtcNow - _stateEnteredAt).TotalSeconds;
        if (_state is CoordState.FormGroup_Inviting
            or CoordState.FormGroup_Accepting or CoordState.FormGroup_ConvertToRaid
            or CoordState.FormGroup_InvitingRest or CoordState.FormGroup_AcceptingRest
            or CoordState.FormGroup_Verify
            && elapsed > 60)
        {
            _logger.LogWarning("DUNGEON_COORD: State {State} timed out after {Elapsed:F0}s, advancing", _state, elapsed);
            TransitionTo(CoordState.TeleportToRFC);
        }

        return _state switch
        {
            CoordState.WaitingForBots => HandleWaitingForBots(requestingAccount, snapshots),
            CoordState.TeleportToOrgrimmar => HandleTeleportToOrgrimmar(requestingAccount, snapshots),
            CoordState.WaitForOrgSettle => HandleWaitForSettle(requestingAccount, snapshots, CoordState.DisbandAndReset, 1),
            CoordState.DisbandAndReset => HandleDisbandAndReset(requestingAccount, snapshots),
            CoordState.PrepareCharacters => HandlePrepareCharacters(requestingAccount, snapshots),
            CoordState.LearnSpellsViaChat => HandleLearnSpellsViaChat(requestingAccount, snapshots),
            CoordState.AddItemsViaChat => HandleAddItemsViaChat(requestingAccount, snapshots),
            CoordState.EquipGear => HandleEquipGear(requestingAccount, snapshots),
            CoordState.FormGroup_Inviting => HandleInviting(requestingAccount, snapshots),
            CoordState.FormGroup_Accepting => HandleAccepting(requestingAccount, snapshots),
            CoordState.FormGroup_ConvertToRaid => HandleConvertToRaid(requestingAccount, snapshots),
            CoordState.FormGroup_InvitingRest => HandleInvitingRest(requestingAccount, snapshots),
            CoordState.FormGroup_AcceptingRest => HandleAcceptingRest(requestingAccount, snapshots),
            CoordState.FormGroup_Verify => HandleVerify(requestingAccount, snapshots),
            CoordState.TeleportToRFC => HandleTeleportToRFC(requestingAccount, snapshots),
            CoordState.WaitForRFCSettle => HandleWaitForSettle(requestingAccount, snapshots, CoordState.DispatchDungeoneering, RfcMapId),
            CoordState.DispatchDungeoneering => HandleDispatchDungeoneering(requestingAccount, snapshots),
            CoordState.DungeonInProgress => HandleDungeonInProgress(requestingAccount, snapshots),
            _ => null,
        };
    }

    private void TransitionTo(CoordState newState)
    {
        // Use LogWarning so state transitions always appear in test output
        // (CharacterStateSocketListener category filters Info on some console providers)
        _logger.LogWarning("DUNGEON_COORD: {Old} → {New}", _state, newState);
        _state = newState;
        _stateEnteredAt = DateTime.UtcNow;
        _tickCount = 0;
    }

    // ===== State Handlers =====

    private ActionMessage? HandleWaitingForBots(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        if (!snapshots.TryGetValue(_leaderAccount, out var leaderSnap) || !leaderSnap.IsObjectManagerValid)
            return null;

        var readyMembers = _memberAccounts.Count(m =>
            snapshots.TryGetValue(m, out var s) && s.IsObjectManagerValid);

        if (readyMembers < 1)
            return null;

        _tickCount++;

        // Wait for ALL members (with 30s timeout for stragglers)
        if (readyMembers < _memberAccounts.Count)
        {
            if (_tickCount % 10 == 1)
                _logger.LogInformation("DUNGEON_COORD: Waiting for bots: {Ready}/{Total} members InWorld (tick {Tick})",
                    readyMembers, _memberAccounts.Count, _tickCount);
            if (_tickCount < 60) // ~30s at 500ms poll
                return null;
            _logger.LogWarning("DUNGEON_COORD: Timeout waiting for all members. Proceeding with {Ready}/{Total}.",
                readyMembers, _memberAccounts.Count);
        }

        _logger.LogInformation("DUNGEON_COORD: {Count}/{Total} members ready. Starting character preparation.",
            readyMembers, _memberAccounts.Count);

        // Step 1: Teleport everyone to Orgrimmar first (clean slate)
        TransitionTo(CoordState.TeleportToOrgrimmar);
        return null;
    }

    private ActionMessage? HandlePrepareCharacters(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        if (_soapClient == null)
        {
            TransitionTo(CoordState.LearnSpellsViaChat);
            return null;
        }

        // Fire all SOAP prep commands in parallel, track tasks for completion
        var allAccounts = new List<string> { _leaderAccount };
        allAccounts.AddRange(_memberAccounts);

        foreach (var account in allAccounts)
        {
            if (_preparedAccounts.ContainsKey(account))
                continue;

            if (!_accountToCharName.TryGetValue(account, out var charName))
                continue; // Not in world yet

            if (!snapshots.TryGetValue(account, out var snap) || !snap.IsObjectManagerValid)
                continue;

            // Mark as launched BEFORE starting the task to prevent duplicate launches
            // from concurrent GetAction calls (multiple bot threads enter HandlePrepareCharacters)
            if (!_preparedAccounts.TryAdd(account, 0))
                continue;
            var task = PrepareCharacterAsync(account, charName);
            _prepTasks.TryAdd(account, task);
        }

        // Wait for any unprepared accounts that weren't in world yet
        var unprepared = allAccounts.Count(a => !_preparedAccounts.ContainsKey(a));
        if (unprepared > 0)
        {
            _tickCount++;
            if (_tickCount < 20) // Wait up to ~10s for stragglers
                return null;
            _logger.LogWarning("DUNGEON_COORD: {Unprepared} accounts never came InWorld, continuing.", unprepared);
        }

        // Wait for ALL SOAP tasks to complete before proceeding.
        // .character level resets spells, so it MUST finish before .learn via chat.
        // Do NOT skip on timeout — out-of-order execution teaches spells that get reset.
        var pendingTasks = _prepTasks.Values.Where(t => !t.IsCompleted).ToList();
        if (pendingTasks.Count > 0)
        {
            _tickCount++;
            if (_tickCount % 20 == 1)
                _logger.LogInformation("DUNGEON_COORD: Waiting for {Count} SOAP prep tasks to complete (tick {Tick})...",
                    pendingTasks.Count, _tickCount);
            return null; // Never skip — SOAP must finish before spell learning
        }

        // All accounts prepared via SOAP — now learn spells via bot chat
        _logger.LogInformation("DUNGEON_COORD: All {Count} characters prepared via SOAP. Learning spells via chat.",
            _preparedAccounts.Count);
        TransitionTo(CoordState.LearnSpellsViaChat);
        return null;
    }

    // ===== Level 15 Class Configuration =====
    // Level 15 matches RFC mob range (13-16). .learn all_myclass teaches all trainer spells.
    // These are key spells per class for verification.
    // Gear: green-quality dungeon-appropriate items for RFC.

    /// <summary>Key spell IDs per class at level 15, used for verification after .learn all_myclass.</summary>
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

    /// <summary>Item IDs for dungeon-appropriate gear per class.</summary>
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

        // NEVER send SOAP mutations to FG (WoW.exe) characters while the client is live.
        // .character level modifies server-side state and pushes packets the real WoW client
        // can't handle gracefully (level reset mid-session → crash). FG bots use chat commands
        // in the LearnSpellsViaChat phase (.targetself → .character level 8) instead.
        var settings = _allSettings.FirstOrDefault(s =>
            s.AccountName.Equals(account, StringComparison.OrdinalIgnoreCase));
        if (settings?.RunnerType == Settings.BotRunnerType.Foreground)
        {
            _logger.LogInformation("DUNGEON_COORD: Skipping SOAP prep for FG bot {Account} ({CharName}) — will use chat commands",
                account, charName);
            return;
        }

        try
        {
            _logger.LogInformation("DUNGEON_COORD: Preparing {Account} ({CharName}): level 8 + instance unbind",
                account, charName);

            // Only SOAP commands that accept a player name directly (no target selection needed).
            // .reset spells/talents/items require a selected target → moved to bot chat phase.
            await _soapClient.ExecuteGMCommandAsync($".instance unbind all {charName}");
            await _soapClient.ExecuteGMCommandAsync($".character level {charName} 8");

            _logger.LogInformation("DUNGEON_COORD: {Account} ({CharName}) SOAP preparation complete.", account, charName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DUNGEON_COORD: Failed to prepare {Account}: {Error}", account, ex.Message);
        }
    }

    /// <summary>
    /// Builds the sequence of chat commands a bot must type to learn level-appropriate spells
    /// and max out weapon/defense skills for level 8 (max skill = 40).
    /// First command is always ".targetself" so .learn targets the bot itself.
    /// Does NOT use ".learn all_myclass" — that teaches every spell including level 60 talents.
    /// FG bots also get .character level 8 here (SOAP mutations crash WoW.exe).
    /// </summary>
    private List<string> BuildSpellChatCommands(string charClass, bool isForeground = false)
    {
        var commands = new List<string> { ".targetself" };

        // FG bots: SOAP mutations crash the real WoW client, so level/unbind go through chat.
        // .character level targets the selected player (.targetself above).
        if (isForeground)
        {
            commands.Add(".character level 8");
        }

        // Reset spells/talents/items BEFORE learning. These commands need a selected target
        // (.targetself above), so they go through bot chat instead of SOAP.
        // Order: reset first → learn second. Otherwise .reset spells wipes what was just learned.
        commands.Add(".reset spells");
        commands.Add(".reset talents");
        commands.Add(".reset items");

        // Learn only level-appropriate spells (level 1-8)
        if (Level8KeySpells.TryGetValue(charClass, out var spells))
        {
            foreach (var spellId in spells)
                commands.Add($".learn {spellId}");
        }

        // Max out weapon and defense skills for level 8 (cap = 40)
        // Skill IDs: Defense=95, 1H Swords=43, 1H Maces=54, 2H Maces=160,
        //            Daggers=173, Staves=136, Crossbows=226, Unarmed=162
        const int maxSkill = 40;
        commands.Add($".setskill 95 {maxSkill} {maxSkill}");  // Defense
        commands.Add($".setskill 162 {maxSkill} {maxSkill}"); // Unarmed

        if (Level8WeaponSkills.TryGetValue(charClass, out var skillIds))
        {
            foreach (var skillId in skillIds)
                commands.Add($".setskill {skillId} {maxSkill} {maxSkill}");
        }

        return commands;
    }

    /// <summary>Weapon skill IDs per class based on their equipped weapons.</summary>
    private static readonly Dictionary<string, int[]> Level8WeaponSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Warrior"] = [43, 433],   // 1H Swords, Shield
        ["Shaman"] = [54, 433],    // 1H Maces, Shield
        ["Druid"] = [160],         // 2H Maces
        ["Priest"] = [136],        // Staves
        ["Warlock"] = [136],       // Staves
        ["Hunter"] = [226, 43],    // Crossbows, 1H Swords
        ["Rogue"] = [173],         // Daggers
        ["Mage"] = [136],          // Staves
    };

    /// <summary>
    /// Builds the sequence of chat commands a bot must type to add all gear items.
    /// Assumes ".targetself" was already sent in spell phase.
    /// </summary>
    private List<string> BuildItemChatCommands(string charClass)
    {
        var commands = new List<string> { ".targetself" }; // Re-target self in case selection changed
        if (Level8Gear.TryGetValue(charClass, out var gearList))
        {
            foreach (var (itemId, _) in gearList)
            {
                if (itemId == 2512) // Rough Arrow — quantity 200
                    commands.Add($".additem {itemId} 200");
                else
                    commands.Add($".additem {itemId}");
            }
        }
        // Class-specific items
        if (charClass.Equals("Warlock", StringComparison.OrdinalIgnoreCase))
            commands.Add(".additem 6265 10"); // Soul Shard x10
        return commands;
    }

    private ActionMessage? HandleLearnSpellsViaChat(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Each bot gets one SEND_CHAT action per tick: .targetself, .learn all_myclass, .learn <id>...
        var settings = _allSettings.FirstOrDefault(s =>
            s.AccountName.Equals(requestingAccount, StringComparison.OrdinalIgnoreCase));
        var charClass = settings?.CharacterClass ?? "Warrior";
        var isFg = settings?.RunnerType == Settings.BotRunnerType.Foreground;
        var commands = BuildSpellChatCommands(charClass, isFg);

        var idx = _chatSpellIndex.GetOrAdd(requestingAccount, 0);
        if (idx < commands.Count)
        {
            _chatSpellIndex[requestingAccount] = idx + 1;
            var cmd = commands[idx];
            _logger.LogInformation("DUNGEON_COORD: {Account} spell chat [{Idx}/{Total}]: {Cmd}",
                requestingAccount, idx + 1, commands.Count, cmd);
            return MakeSendChatAction(cmd);
        }

        // Check if all accounts have finished learning
        var allAccounts = new List<string> { _leaderAccount };
        allAccounts.AddRange(_memberAccounts);

        var allDone = allAccounts.All(a =>
        {
            if (!_chatSpellIndex.TryGetValue(a, out var i)) return false;
            var aSettings = _allSettings.FirstOrDefault(s =>
                s.AccountName.Equals(a, StringComparison.OrdinalIgnoreCase));
            var aClass = aSettings?.CharacterClass ?? "Warrior";
            var aIsFg = aSettings?.RunnerType == Settings.BotRunnerType.Foreground;
            return i >= BuildSpellChatCommands(aClass, aIsFg).Count;
        });

        if (allDone)
        {
            _logger.LogInformation("DUNGEON_COORD: All bots learned spells. Adding items via chat.");
            TransitionTo(CoordState.AddItemsViaChat);
        }
        return null;
    }

    private ActionMessage? HandleAddItemsViaChat(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Each bot gets one SEND_CHAT action per tick: .additem <id> for each gear piece
        var settings = _allSettings.FirstOrDefault(s =>
            s.AccountName.Equals(requestingAccount, StringComparison.OrdinalIgnoreCase));
        var charClass = settings?.CharacterClass ?? "Warrior";
        var commands = BuildItemChatCommands(charClass);

        var idx = _chatItemIndex.GetOrAdd(requestingAccount, 0);
        if (idx < commands.Count)
        {
            _chatItemIndex[requestingAccount] = idx + 1;
            var cmd = commands[idx];
            _logger.LogInformation("DUNGEON_COORD: {Account} item chat [{Idx}/{Total}]: {Cmd}",
                requestingAccount, idx + 1, commands.Count, cmd);
            return MakeSendChatAction(cmd);
        }

        // Check if all accounts have finished adding items
        var allAccounts = new List<string> { _leaderAccount };
        allAccounts.AddRange(_memberAccounts);

        var allDone = allAccounts.All(a =>
        {
            if (!_chatItemIndex.TryGetValue(a, out var i)) return false;
            var aSettings = _allSettings.FirstOrDefault(s =>
                s.AccountName.Equals(a, StringComparison.OrdinalIgnoreCase));
            var aClass = aSettings?.CharacterClass ?? "Warrior";
            return i >= BuildItemChatCommands(aClass).Count;
        });

        if (allDone)
        {
            _logger.LogInformation("DUNGEON_COORD: All bots have items. Equipping gear.");
            TransitionTo(CoordState.EquipGear);
        }
        return null;
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
            _logger.LogInformation("DUNGEON_COORD: All bots equipped. Forming group.");
            _inviteIndex = 0;
            TransitionTo(CoordState.FormGroup_Inviting);
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
            TransitionTo(CoordState.WaitForOrgSettle);
        }

        return null;
    }

    private ActionMessage? HandleWaitForSettle(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots,
        CoordState nextState, int expectedMapId)
    {
        _tickCount++;
        // Wait ~2 seconds (4 ticks) for teleport to settle
        if (_tickCount < 4)
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
        if (_tickCount >= 8 || onMap >= 2)
        {
            TransitionTo(nextState);
        }

        return null;
    }

    private ActionMessage? HandleDisbandAndReset(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Each bot leaves any stale group/raid and declines pending invites
        if (_leftOldGroup.TryAdd(requestingAccount, 0))
        {
            _logger.LogInformation("DUNGEON_COORD: {Account} leaving old group/raid + declining pending invites", requestingAccount);
            // LeaveGroup is a no-op if not in a group; DeclineGroupInvite is a no-op if no pending invite
            return MakeAction(ActionType.LeaveGroup);
        }

        // Check if all bots have left
        var allAccounts = new List<string> { _leaderAccount };
        allAccounts.AddRange(_memberAccounts);
        if (!allAccounts.All(a => _leftOldGroup.ContainsKey(a)))
            return null;

        // Wait a couple ticks for leave to propagate on the server
        _tickCount++;
        if (_tickCount < 4)
            return null;

        _logger.LogInformation("DUNGEON_COORD: All bots disbanded. Starting character preparation.");

        // If SOAP is available, do full prep (level set + instance unbind)
        if (_soapClient != null)
        {
            TransitionTo(CoordState.PrepareCharacters);
        }
        else
        {
            _logger.LogWarning("DUNGEON_COORD: No SOAP client — skipping SOAP prep, going to spell learning.");
            TransitionTo(CoordState.LearnSpellsViaChat);
        }
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
                    && snap.IsObjectManagerValid)
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
                // Wait 0.8s for invite to arrive at the member
                if ((DateTime.UtcNow - _phaseStartedAt).TotalSeconds < 0.8)
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
                if (isGrouped || waitElapsed > 3.0) // Wait up to 3s
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

        // Wait longer for the conversion to propagate — MaNGOS needs time to update
        // all clients' group state. Without this, batch 2 invites are sent before
        // the server fully transitions the group to raid mode, causing silent failures.
        if (_tickCount < 10 && grouped < PARTY_SIZE_LIMIT)
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
                    && snap.IsObjectManagerValid)
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
                if ((DateTime.UtcNow - _phaseStartedAt).TotalSeconds < 0.8)
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
                if (isGrouped || waitElapsed > 3.0)
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
        if (_tickCount < 3)
            return null;

        var grouped = snapshots.Values.Count(s => s.PartyLeaderGuid != 0);
        _logger.LogInformation("DUNGEON_COORD: Group verify: {Grouped}/{Total} bots grouped.",
            grouped, snapshots.Count);

        // Skip subgroup organization — it can desync raid membership in Vanilla.
        // All bots stay in default subgroup 0. Buff coverage is less optimal but raid stays intact.
        TransitionTo(CoordState.TeleportToRFC);
        return null;
    }

    /// <summary>
    /// Organize raid subgroups so duplicate classes are in separate groups.
    /// This ensures party-wide buffs (Battle Shout, Power Word: Fortitude, etc.) cover more players.
    /// Algorithm: round-robin classes across subgroups — each instance of a class goes to the next subgroup.
    /// NOTE: Currently bypassed — subgroup changes can desync raid membership in Vanilla 1.12.1.
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

        // Step 2: Send one ChangeRaidSubgroup per tick (no throttle — fast)
        _tickCount++;

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
        _lastTeleportSent[requestingAccount] = DateTime.UtcNow; // Seed throttle for late-join path
        _rfcTeleportIndex++;
        _lastRfcTeleportAt = DateTime.UtcNow;

        _logger.LogInformation("DUNGEON_COORD: Teleporting {Account} to RFC (map {Map}) [{Idx}/{Total}]",
            requestingAccount, RfcMapId, _rfcTeleportIndex, _rfcTeleportOrder.Count);

        // All bots (FG and BG) use bot chat .go xyz for cross-map teleport.
        // .go xyz is a self-teleport GM command that triggers server-side transfer.
        // Previously FG used SOAP `.tele name` which required a game_tele entry ("rfc")
        // that may not exist, causing silent teleport failures.
        return MakeSendChatAction($".go xyz {RfcStartX:0.#} {RfcStartY:0.#} {RfcStartZ:0.#} {RfcMapId}");
    }

    private ActionMessage? HandleDispatchDungeoneering(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        _tickCount++;

        // Wait for the designated leader (FG bot / TESTBOT1) to poll. Do NOT promote a BG bot
        // as substitute — the FG bot is the main tank and must always be leader.
        // BG members wait in FollowLeader state (autonomous waypoint nav) until the FG leader arrives.
        bool leaderHasPolled = _dungeoneeringDispatched.ContainsKey(_leaderAccount);
        bool allMembersDispatched = _memberAccounts.All(m => _dungeoneeringDispatched.ContainsKey(m));
        if (!leaderHasPolled && allMembersDispatched && _tickCount % 10 == 0)
        {
            _logger.LogInformation("DUNGEON_COORD: Waiting for leader '{Leader}' to poll (tick {Tick}). " +
                "Members dispatched: {Dispatched}/{Total}",
                _leaderAccount, _tickCount, _dungeoneeringDispatched.Count, snapshots.Count);
        }

        bool isLeader = requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase);

        // Late-join teleport: if this bot is not on RFC map AND was never teleported
        // in the TeleportToRFC phase, send them a teleport command.
        // If they WERE already teleported, their snapshot may show stale mapId while
        // WoW.exe loads the dungeon (10-20s loading screen). Do NOT re-teleport during
        // loading — spamming .go xyz mid-load crashes WoW.exe.
        if (snapshots.TryGetValue(requestingAccount, out var snap)
            && snap.Player?.Unit?.GameObject?.Base?.MapId != RfcMapId
            && !_teleportedToRFC.ContainsKey(requestingAccount))
        {
            var teleportAction = TrySendThrottledTeleport(requestingAccount,
                $".go xyz {RfcStartX:0.#} {RfcStartY:0.#} {RfcStartZ:0.#} {RfcMapId}");
            return teleportAction; // null if throttled — bot will re-poll
        }

        // Only dispatch ONCE per bot. TryAdd returns false if already dispatched.
        // Without this guard, every poll re-sends StartDungeoneering which rebuilds the
        // behavior tree, steals DungeoneeringTask ticks, and floods the pathfinding service.
        if (!_dungeoneeringDispatched.TryAdd(requestingAccount, 0))
            return null;

        _logger.LogWarning("DUNGEON_COORD: Dispatching START_DUNGEONEERING to '{Account}' (leader={IsLeader}) [{Dispatched}/{Total}]",
            requestingAccount, isLeader, _dungeoneeringDispatched.Count, snapshots.Count);

        var action = new ActionMessage { ActionType = ActionType.StartDungeoneering };
        action.Parameters.Add(new RequestParameter { IntParam = isLeader ? 1 : 0 });
        action.Parameters.Add(new RequestParameter { IntParam = RfcMapId }); // target dungeon map ID

        // Transition when all bots (including leader) have been dispatched.
        // Re-evaluate after TryAdd since the pre-check was before this bot was added.
        var leaderDispatched = _dungeoneeringDispatched.ContainsKey(_leaderAccount);
        var nowAllDispatched = _memberAccounts.All(m => _dungeoneeringDispatched.ContainsKey(m));
        if (leaderDispatched && nowAllDispatched)
        {
            TransitionTo(CoordState.DungeonInProgress);
        }
        else if (!leaderDispatched && _tickCount % 10 == 0)
        {
            _logger.LogInformation("DUNGEON_COORD: Still waiting for leader '{Leader}' dispatch (tick {Tick})",
                _leaderAccount, _tickCount);
        }

        return action;
    }

    private ActionMessage? HandleDungeonInProgress(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        WoWActivitySnapshot? leaderSnap = null;

        // If the leader missed the DispatchDungeoneering window (FG bot polls slower),
        // handle late join: teleport to RFC if needed, then dispatch.
        if (requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase))
        {
            snapshots.TryGetValue(_leaderAccount, out leaderSnap);
            // Late-join teleport: if leader is not on RFC map AND wasn't already teleported,
            // send them there. If they were already teleported (in TeleportToRFC phase),
            // their snapshot may lag behind during WoW.exe loading — just wait.
            var leaderMapId = leaderSnap?.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
            if (leaderMapId != RfcMapId && !_teleportedToRFC.ContainsKey(_leaderAccount))
            {
                return TrySendThrottledTeleport(_leaderAccount,
                    $".go xyz {RfcStartX:0.#} {RfcStartY:0.#} {RfcStartZ:0.#} {RfcMapId}");
            }

            // Re-dispatch StartDungeoneering to leader every few seconds until acknowledged.
            // The BotRunner drops actions when playerWorldReady=false (map still loading),
            // but the coordinator already marked it as dispatched. Without re-dispatch,
            // the leader sits idle forever. The BotRunner's ActionDispatch handles
            // duplicate StartDungeoneering gracefully (checks for existing task, skips).
            // Rate-limit to once per 3 seconds to avoid behavior tree rebuild spam.
            _dungeoneeringDispatched.TryAdd(requestingAccount, 0);
            if (CanAct(requestingAccount, 3.0))
            {
                _lastActionSent[requestingAccount] = DateTime.UtcNow;
                var leaderAction = new ActionMessage { ActionType = ActionType.StartDungeoneering };
                leaderAction.Parameters.Add(new RequestParameter { IntParam = 1 }); // isLeader = true
                return leaderAction;
            }
            return null;
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

    /// <summary>
    /// Send a teleport command if not already sent recently. Returns null if throttled.
    /// CRITICAL: Spamming .go xyz to an FG bot mid-loading causes WoW.exe to crash.
    /// One teleport command is enough — the bot needs 10-20s to complete cross-map loading.
    /// </summary>
    private ActionMessage? TrySendThrottledTeleport(string account, string teleportCommand)
    {
        if (_lastTeleportSent.TryGetValue(account, out var lastSent)
            && (DateTime.UtcNow - lastSent).TotalSeconds < TELEPORT_COOLDOWN_SEC)
        {
            return null; // Already sent recently — wait for load to complete
        }
        _lastTeleportSent[account] = DateTime.UtcNow;
        _logger.LogWarning("DUNGEON_COORD: Sending teleport to '{Account}': {Cmd}", account, teleportCommand);
        return MakeSendChatAction(teleportCommand);
    }

    /// <summary>

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
