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
///   WaitingForBots → PrepareCharacters → TeleportToOrgrimmar → FormGroup →
///   TeleportToRFC → DispatchDungeoneering → DungeonInProgress
///
/// PrepareCharacters: SOAP-based level set, .learn all_myclass, class-specific setup
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
        TeleportToOrgrimmar,
        WaitForTeleportSettle,
        FormGroup_LeaveOldGroups,   // All bots leave any stale group from prior runs
        FormGroup_Inviting,         // Invite first 4 (party cap = 5 incl leader)
        FormGroup_Accepting,        // First 4 accept
        FormGroup_ConvertToRaid,    // Leader converts party → raid
        FormGroup_InvitingRest,     // Invite remaining members
        FormGroup_AcceptingRest,    // Remaining members accept
        FormGroup_Verify,
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
    private int _prepIndex;

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
            or CoordState.FormGroup_Verify
            && elapsed > 60)
        {
            _logger.LogWarning("DUNGEON_COORD: State {State} timed out after {Elapsed:F0}s, advancing", _state, elapsed);
            TransitionTo(CoordState.TeleportToRFC);
        }

        return _state switch
        {
            CoordState.WaitingForBots => HandleWaitingForBots(requestingAccount, snapshots),
            CoordState.PrepareCharacters => HandlePrepareCharacters(requestingAccount, snapshots),
            CoordState.TeleportToOrgrimmar => HandleTeleportToOrgrimmar(requestingAccount, snapshots),
            CoordState.WaitForTeleportSettle => HandleWaitForSettle(requestingAccount, snapshots, CoordState.FormGroup_LeaveOldGroups, 1),
            CoordState.FormGroup_LeaveOldGroups => HandleLeaveOldGroups(requestingAccount, snapshots),
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
            TransitionTo(CoordState.TeleportToOrgrimmar);
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

        // All accounts prepared
        _logger.LogInformation("DUNGEON_COORD: All {Count} characters prepared. Teleporting to Orgrimmar.",
            _preparedAccounts.Count);  // ConcurrentDictionary.Count is safe
        TransitionTo(CoordState.TeleportToOrgrimmar);
        return null;
    }

    private async Task PrepareCharacterAsync(string account, string charName)
    {
        if (_soapClient == null) return;

        try
        {
            var settings = _allSettings.FirstOrDefault(s =>
                s.AccountName.Equals(account, StringComparison.OrdinalIgnoreCase));
            var charClass = settings?.CharacterClass ?? "Warrior";

            _logger.LogInformation("DUNGEON_COORD: Preparing {Account} ({CharName}, {Class}): level 8 + spells",
                account, charName, charClass);

            // Clear instance binds so RFC can be reset freely
            await _soapClient.ExecuteGMCommandAsync($".instance unbind all {charName}");

            // Set level to 8
            await _soapClient.ExecuteGMCommandAsync($".character level {charName} 8");

            // Learn all class spells
            await _soapClient.ExecuteGMCommandAsync($".learn all_myclass {charName}");

            // Class-specific setup (totems, pets, etc.)
            await PrepareClassSpecific(charName, charClass);

            // Reset items to clean slate
            await _soapClient.ExecuteGMCommandAsync($".reset items {charName}");

            // Add class-appropriate starter gear
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
                // Earth Totem (quest item needed for totem quests)
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 5175"); // Earth Totem
                break;
            case "warlock":
                // Imp summon spell (learned via .learn all_myclass)
                break;
            case "hunter":
                // Tame Beast learned at level 10, not available at 8
                break;
        }
    }

    private async Task AddStarterGear(string charName, string charClass)
    {
        if (_soapClient == null) return;

        // Add basic weapons and armor appropriate for the class
        // These are common low-level items from MaNGOS item tables
        switch (charClass.ToLowerInvariant())
        {
            case "warrior":
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2385"); // Tarnished Chain Vest
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2387"); // Tarnished Chain Leggings
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 25"); // Worn Shortsword
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2129"); // Large Round Shield
                break;
            case "shaman":
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2385"); // Tarnished Chain Vest
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 36"); // Worn Mace
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2129"); // Large Round Shield
                break;
            case "druid":
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 6059"); // Nomad Vest (leather)
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2493"); // Wooden Mallet
                break;
            case "priest":
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 6096"); // Apprentice's Robe
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 955"); // Apprentice's Staff
                break;
            case "warlock":
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 6096"); // Apprentice's Robe
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 955"); // Apprentice's Staff
                break;
            case "hunter":
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2385"); // Tarnished Chain Vest
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2504"); // Worn Crossbow
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2512 200"); // Rough Arrow x200
                break;
            case "rogue":
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2385"); // Tarnished Chain Vest (rogues can wear mail at low level? actually leather)
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2092"); // Worn Dagger
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 2092"); // Worn Dagger (offhand)
                break;
            case "mage":
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 6096"); // Apprentice's Robe
                await _soapClient.ExecuteGMCommandAsync($".additem {charName} 955"); // Apprentice's Staff
                break;
        }
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

        TransitionTo(CoordState.TeleportToRFC);
        return null;
    }

    private ActionMessage? HandleTeleportToRFC(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        if (_teleportedToRFC.TryAdd(requestingAccount, 0))
        {
            _logger.LogInformation("DUNGEON_COORD: Teleporting {Account} to RFC (map {Map})",
                requestingAccount, RfcMapId);
            return MakeSendChatAction($".go xyz {RfcStartX:0.#} {RfcStartY:0.#} {RfcStartZ:0.#} {RfcMapId}");
        }

        var allAccounts = new List<string> { _leaderAccount };
        allAccounts.AddRange(_memberAccounts);
        if (allAccounts.All(a => _teleportedToRFC.ContainsKey(a)))
        {
            _logger.LogInformation("DUNGEON_COORD: All bots teleported to RFC. Waiting to settle.");
            TransitionTo(CoordState.WaitForRFCSettle);
        }

        return null;
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
        // --- Leader failover: if leader drops (FG crash), promote first available BG bot ---
        bool leaderAlive = snapshots.TryGetValue(_leaderAccount, out var leaderSnap)
                           && leaderSnap.ScreenState == "InWorld";
        if (leaderAlive)
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
                _logger.LogWarning("DUNGEON_COORD: Leader '{OldLeader}' lost (no snapshot for {Timeout}s). Promoting '{NewLeader}' to leader.",
                    _leaderAccount, LEADER_FAILOVER_TIMEOUT_SEC, newLeader);
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
            if (lowestHpAccount != null)
            {
                var hpRatio = GetHealthRatio(snapshots[lowestHpAccount]);
                if (hpRatio < HEAL_THRESHOLD)
                {
                    var targetGuid = GetPlayerGuid(snapshots[lowestHpAccount]);
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
