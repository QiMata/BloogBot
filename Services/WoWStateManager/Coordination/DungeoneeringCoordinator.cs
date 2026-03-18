using Communication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WoWStateManager.Settings;

namespace WoWStateManager.Coordination;

/// <summary>
/// Coordinates N bots for dungeon crawling. Handles group formation (leader
/// invites all, members accept), then dispatches START_DUNGEONEERING to all
/// bots once the group is formed.
///
/// The leader bot gets isLeader=1 and navigates dungeon waypoints.
/// All other bots get isLeader=0 and follow the party leader.
///
/// After dispatching dungeoneering, the coordinator enters a monitoring
/// state where it provides heal/DPS support based on party member snapshots.
/// </summary>
public class DungeoneeringCoordinator
{
    public enum CoordState
    {
        WaitingForBots,
        FormGroup_Inviting,
        FormGroup_Accepting,
        FormGroup_Verify,
        DispatchDungeoneering,
        DungeonInProgress,
    }

    private readonly ILogger _logger;
    private readonly string _leaderAccount;
    private readonly List<string> _memberAccounts;
    private readonly Dictionary<string, string> _accountToCharName = new();

    private CoordState _state = CoordState.WaitingForBots;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private int _tickCount;
    private int _inviteIndex;
    private int _acceptIndex;
    private bool _dungeoneeringDispatched;

    // Throttle follow/heal actions per account
    private readonly Dictionary<string, DateTime> _lastActionSent = new();
    private const double ACTION_COOLDOWN_SEC = 3.0;
    private const double FOLLOW_COOLDOWN_SEC = 1.5;
    private const float FOLLOW_DISTANCE = 15f;
    private const float HEAL_THRESHOLD = 0.50f;

    // Cached healer spell IDs
    private readonly Dictionary<string, uint> _healSpells = new();
    private readonly Dictionary<string, uint> _damageSpells = new();
    private readonly HashSet<string> _spellsResolved = new();

    public CoordState State => _state;

    public DungeoneeringCoordinator(string leaderAccount, IEnumerable<string> memberAccounts, ILogger logger)
    {
        _leaderAccount = leaderAccount;
        _memberAccounts = memberAccounts.Where(a => !a.Equals(leaderAccount, StringComparison.OrdinalIgnoreCase)).ToList();
        _logger = logger;

        _logger.LogInformation("DUNGEON_COORD: Initialized — Leader='{Leader}', Members=[{Members}]",
            leaderAccount, string.Join(", ", _memberAccounts));
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

        // Timeout protection
        var elapsed = (DateTime.UtcNow - _stateEnteredAt).TotalSeconds;
        if (_state is CoordState.FormGroup_Inviting or CoordState.FormGroup_Accepting or CoordState.FormGroup_Verify
            && elapsed > 60)
        {
            _logger.LogWarning("DUNGEON_COORD: State {State} timed out after {Elapsed:F0}s, advancing", _state, elapsed);
            TransitionTo(CoordState.DispatchDungeoneering);
        }

        return _state switch
        {
            CoordState.WaitingForBots => HandleWaitingForBots(requestingAccount, snapshots),
            CoordState.FormGroup_Inviting => HandleInviting(requestingAccount, snapshots),
            CoordState.FormGroup_Accepting => HandleAccepting(requestingAccount, snapshots),
            CoordState.FormGroup_Verify => HandleVerify(requestingAccount, snapshots),
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
        // Wait until at least leader + 1 member are InWorld
        if (!snapshots.TryGetValue(_leaderAccount, out var leaderSnap) || leaderSnap.ScreenState != "InWorld")
            return null;

        var readyMembers = _memberAccounts.Count(m =>
            snapshots.TryGetValue(m, out var s) && s.ScreenState == "InWorld");

        if (readyMembers < 1)
            return null;

        // Check if already grouped
        if (leaderSnap.PartyLeaderGuid != 0)
        {
            _logger.LogInformation("DUNGEON_COORD: Leader already in group, skipping formation.");
            TransitionTo(CoordState.DispatchDungeoneering);
            return null;
        }

        _logger.LogInformation("DUNGEON_COORD: {Count} members ready. Starting group formation.",
            readyMembers);
        _inviteIndex = 0;
        TransitionTo(CoordState.FormGroup_Inviting);
        return null;
    }

    private ActionMessage? HandleInviting(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Only act when leader polls
        if (!requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase))
            return null;

        _tickCount++;
        // Space out invites: 1 invite every 3 ticks (~1.5s at 500ms poll)
        if (_tickCount % 3 != 1)
            return null;

        // Find next member to invite
        while (_inviteIndex < _memberAccounts.Count)
        {
            var memberAccount = _memberAccounts[_inviteIndex];
            _inviteIndex++;

            if (_accountToCharName.TryGetValue(memberAccount, out var charName)
                && snapshots.TryGetValue(memberAccount, out var snap)
                && snap.ScreenState == "InWorld")
            {
                _logger.LogInformation("DUNGEON_COORD: Leader inviting '{CharName}' ({Account})",
                    charName, memberAccount);
                return MakeAction(ActionType.SendGroupInvite, charName);
            }
        }

        // All invites sent
        _logger.LogInformation("DUNGEON_COORD: All invites sent. Waiting for accepts.");
        _acceptIndex = 0;
        TransitionTo(CoordState.FormGroup_Accepting);
        return null;
    }

    private ActionMessage? HandleAccepting(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Find a member that hasn't accepted yet
        if (_acceptIndex >= _memberAccounts.Count)
        {
            TransitionTo(CoordState.FormGroup_Verify);
            return null;
        }

        var memberAccount = _memberAccounts[_acceptIndex];
        if (!requestingAccount.Equals(memberAccount, StringComparison.OrdinalIgnoreCase))
            return null;

        _tickCount++;
        if (_tickCount < 3)
            return null; // Small delay before accept

        _logger.LogInformation("DUNGEON_COORD: '{Account}' accepting invite.", memberAccount);
        _acceptIndex++;
        _tickCount = 0;
        return MakeAction(ActionType.AcceptGroupInvite);
    }

    private ActionMessage? HandleVerify(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        _tickCount++;
        if (_tickCount < 10)
            return null;

        // Check how many bots report a party leader
        var grouped = snapshots.Values.Count(s => s.PartyLeaderGuid != 0);
        _logger.LogInformation("DUNGEON_COORD: Group verify: {Grouped}/{Total} bots grouped.",
            grouped, snapshots.Count);

        TransitionTo(CoordState.DispatchDungeoneering);
        return null;
    }

    private ActionMessage? HandleDispatchDungeoneering(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Send START_DUNGEONEERING to the requesting bot
        bool isLeader = requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("DUNGEON_COORD: Dispatching START_DUNGEONEERING to '{Account}' (leader={IsLeader})",
            requestingAccount, isLeader);

        var action = new ActionMessage { ActionType = ActionType.StartDungeoneering };
        action.Parameters.Add(new RequestParameter { IntParam = isLeader ? 1 : 0 });

        // Track dispatches — once all bots have received the action, transition
        _tickCount++;
        if (_tickCount >= snapshots.Count * 2)
        {
            TransitionTo(CoordState.DungeonInProgress);
        }

        return action;
    }

    private ActionMessage? HandleDungeonInProgress(string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        // Leader runs on its own via DungeoneeringTask — no coordinator overlay needed.
        if (requestingAccount.Equals(_leaderAccount, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!snapshots.TryGetValue(_leaderAccount, out var leaderSnap))
            return null;

        if (!snapshots.TryGetValue(requestingAccount, out var mySnap))
            return null;

        // Resolve spells for this account if not done
        if (!_spellsResolved.Contains(requestingAccount))
            ResolveSpells(requestingAccount, mySnap);

        // Only overlay combat coordination when leader is in combat
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

        // Try to find healing spells (any class)
        var healSpells = new Dictionary<string, uint[]>
        {
            // Druid: Healing Touch, Rejuvenation
            ["HealingTouch"] = [5185, 5186, 5187, 5188, 5189],
            ["Rejuvenation"] = [774, 1058, 1430, 2090, 2091],
            // Priest: Lesser Heal, Heal
            ["LesserHeal"] = [2050, 2052, 2053],
            ["Heal"] = [2054, 2055, 6063, 6064],
            // Shaman: Healing Wave, Lesser Healing Wave
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

        // DPS spells
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

        _spellsResolved.Add(account);
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
            if (hp < lowestHp && hp > 0.01f) // Ignore dead members
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

    private static ActionMessage MakeCastSpellAction(int spellId, long targetGuid)
    {
        var action = new ActionMessage { ActionType = ActionType.CastSpell };
        action.Parameters.Add(new RequestParameter { IntParam = spellId });
        action.Parameters.Add(new RequestParameter { LongParam = targetGuid });
        return action;
    }
}
