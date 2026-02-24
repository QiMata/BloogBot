using Communication;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace WoWStateManager.Coordination;

/// <summary>
/// Coordinates combat between a foreground warrior and a
/// background shaman (receives actions from StateManager). Reads both bots'
/// snapshots and injects appropriate actions for the shaman: heal when warrior
/// is low, DPS the warrior's target, follow when out of combat.
/// </summary>
public class CombatCoordinator
{
    public enum CoordinatorState
    {
        WaitingForBots,
        FormGroup_SendInvite,
        FormGroup_WaitForAccept,
        FormGroup_VerifyGroup,
        GroupFormed,
        CombatSupport,
        ShamanResting,
    }

    private readonly ILogger _logger;
    private readonly string _foregroundAccount;
    private readonly string _backgroundAccount;

    private CoordinatorState _state = CoordinatorState.WaitingForBots;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private int _tickCount;

    // Resolved at runtime from snapshots
    private string? _foregroundCharName;
    private string? _backgroundCharName;

    // Cached spell IDs (resolved once from shaman's spellList)
    private uint _healSpellId;
    private uint _damageSpellId;
    private uint _earthShockId;
    private bool _spellsResolved;

    // Throttle: don't spam the same action every tick
    private DateTime _lastActionSentAt = DateTime.MinValue;

    // Follow throttle: don't send GOTO every tick
    private DateTime _lastFollowSentAt = DateTime.MinValue;

    private const float HEAL_THRESHOLD = 0.50f;      // Heal warrior when below 50% HP
    private const float MANA_LOW_THRESHOLD = 0.15f;   // Shaman rests when mana below 15%
    private const float MANA_REGEN_THRESHOLD = 0.80f;  // Resume when mana above 80%
    private const float FOLLOW_DISTANCE = 15f;         // Start following when > 15y away
    private const float FOLLOW_CLOSE = 8f;             // Target follow distance
    private const double ACTION_COOLDOWN_SEC = 3.0;    // Min time between spell actions (Lightning Bolt = 2.5s cast)
    private const double FOLLOW_COOLDOWN_SEC = 1.0;    // Min time between GOTO actions

    public CoordinatorState State => _state;

    public CombatCoordinator(string foregroundAccount, string backgroundAccount, ILogger logger)
    {
        _foregroundAccount = foregroundAccount;
        _backgroundAccount = backgroundAccount;
        _logger = logger;
    }

    /// <summary>
    /// Called each tick from CharacterStateSocketListener.HandleRequest.
    /// Returns an ActionMessage to inject into the response for the given account, or null.
    /// </summary>
    public ActionMessage? GetAction(
        string requestingAccount,
        ConcurrentDictionary<string, WoWActivitySnapshot> snapshots)
    {
        if (!snapshots.TryGetValue(_foregroundAccount, out var fgSnapshot) ||
            !snapshots.TryGetValue(_backgroundAccount, out var bgSnapshot))
            return null;

        // Both bots must be InWorld
        if (fgSnapshot.ScreenState != "InWorld" || bgSnapshot.ScreenState != "InWorld")
            return null;

        // Resolve character names
        if (string.IsNullOrEmpty(_foregroundCharName) && !string.IsNullOrEmpty(fgSnapshot.CharacterName))
            _foregroundCharName = fgSnapshot.CharacterName;
        if (string.IsNullOrEmpty(_backgroundCharName) && !string.IsNullOrEmpty(bgSnapshot.CharacterName))
            _backgroundCharName = bgSnapshot.CharacterName;

        if (string.IsNullOrEmpty(_foregroundCharName) || string.IsNullOrEmpty(_backgroundCharName))
            return null;

        // Timeout protection: if stuck in any state > 60s (except stable states), log warning
        var elapsed = (DateTime.UtcNow - _stateEnteredAt).TotalSeconds;
        if (_state is CoordinatorState.FormGroup_SendInvite or CoordinatorState.FormGroup_WaitForAccept
            or CoordinatorState.FormGroup_VerifyGroup && elapsed > 30)
        {
            _logger.LogWarning($"COMBAT_COORD: State {_state} timed out after {elapsed:F0}s, advancing to GroupFormed");
            TransitionTo(CoordinatorState.GroupFormed);
        }

        return _state switch
        {
            CoordinatorState.WaitingForBots => HandleWaitingForBots(requestingAccount, fgSnapshot, bgSnapshot),
            CoordinatorState.FormGroup_SendInvite => HandleFormGroupSendInvite(requestingAccount, fgSnapshot, bgSnapshot),
            CoordinatorState.FormGroup_WaitForAccept => HandleFormGroupWaitForAccept(requestingAccount),
            CoordinatorState.FormGroup_VerifyGroup => HandleFormGroupVerify(requestingAccount, fgSnapshot, bgSnapshot),
            CoordinatorState.GroupFormed => HandleGroupFormed(requestingAccount, fgSnapshot, bgSnapshot),
            CoordinatorState.CombatSupport => HandleCombatSupport(requestingAccount, fgSnapshot, bgSnapshot),
            CoordinatorState.ShamanResting => HandleShamanResting(requestingAccount, fgSnapshot, bgSnapshot),
            _ => null
        };
    }

    private void TransitionTo(CoordinatorState newState)
    {
        _logger.LogInformation($"COMBAT_COORD: {_state} → {newState}");
        _state = newState;
        _stateEnteredAt = DateTime.UtcNow;
        _tickCount = 0;
    }

    // ===== State Handlers =====

    private ActionMessage? HandleWaitingForBots(string requestingAccount,
        WoWActivitySnapshot fgSnapshot, WoWActivitySnapshot bgSnapshot)
    {
        // Check if bots are already in a group — skip formation if so
        if (fgSnapshot.PartyLeaderGuid != 0 || bgSnapshot.PartyLeaderGuid != 0)
        {
            _logger.LogInformation($"COMBAT_COORD: Bots already grouped (FG PartyLeader={fgSnapshot.PartyLeaderGuid:X}, BG PartyLeader={bgSnapshot.PartyLeaderGuid:X}). Skipping group formation.");
            TransitionTo(CoordinatorState.GroupFormed);
            return null;
        }

        _logger.LogInformation($"COMBAT_COORD: Both bots InWorld. Starting group formation.");
        TransitionTo(CoordinatorState.FormGroup_SendInvite);
        return null;
    }

    private ActionMessage? HandleFormGroupSendInvite(string requestingAccount,
        WoWActivitySnapshot fgSnapshot, WoWActivitySnapshot bgSnapshot)
    {
        // Double-check: if already grouped (race condition), skip to GroupFormed
        if (fgSnapshot.PartyLeaderGuid != 0 || bgSnapshot.PartyLeaderGuid != 0)
        {
            _logger.LogInformation($"COMBAT_COORD: Already grouped during SendInvite. Skipping.");
            TransitionTo(CoordinatorState.GroupFormed);
            return null;
        }

        // Foreground invites background
        if (requestingAccount == _foregroundAccount)
        {
            _logger.LogInformation($"COMBAT_COORD: Sending group invite from '{_foregroundAccount}' to '{_backgroundCharName}'");
            TransitionTo(CoordinatorState.FormGroup_WaitForAccept);
            return MakeAction(ActionType.SendGroupInvite, _backgroundCharName);
        }
        return null;
    }

    private ActionMessage? HandleFormGroupWaitForAccept(string requestingAccount)
    {
        _tickCount++;
        if (_tickCount >= 5 && requestingAccount == _backgroundAccount)
        {
            _logger.LogInformation($"COMBAT_COORD: Background bot accepting group invite");
            TransitionTo(CoordinatorState.FormGroup_VerifyGroup);
            return MakeAction(ActionType.AcceptGroupInvite);
        }
        return null;
    }

    private ActionMessage? HandleFormGroupVerify(string requestingAccount,
        WoWActivitySnapshot fgSnapshot, WoWActivitySnapshot bgSnapshot)
    {
        _tickCount++;
        if (_tickCount >= 5)
        {
            if (fgSnapshot.PartyLeaderGuid != 0 || bgSnapshot.PartyLeaderGuid != 0)
            {
                _logger.LogInformation($"COMBAT_COORD: Group formed! PartyLeader FG={fgSnapshot.PartyLeaderGuid:X}, BG={bgSnapshot.PartyLeaderGuid:X}");
                TransitionTo(CoordinatorState.GroupFormed);
            }
            else if (_tickCount >= 20)
            {
                _logger.LogWarning("COMBAT_COORD: Group verify timed out, advancing anyway");
                TransitionTo(CoordinatorState.GroupFormed);
            }
        }
        return null;
    }

    private ActionMessage? HandleGroupFormed(string requestingAccount,
        WoWActivitySnapshot fgSnapshot, WoWActivitySnapshot bgSnapshot)
    {
        // Only send actions to background bot
        if (requestingAccount != _backgroundAccount)
            return null;

        // Periodic status logging (every ~30 ticks = ~3s)
        _tickCount++;
        if (_tickCount % 30 == 0)
        {
            var wFlags = fgSnapshot.Player?.Unit?.UnitFlags ?? 0;
            var wTarget = GetTargetGuid(fgSnapshot);
            var wHp = GetHealthRatio(fgSnapshot);
            var wPos = GetPosition(fgSnapshot);
            var sPos = GetPosition(bgSnapshot);
            var shamanSpells = bgSnapshot.Player?.SpellList?.Count ?? 0;
            var posStr = wPos.HasValue ? $"({wPos.Value.x:F0},{wPos.Value.y:F0},{wPos.Value.z:F0})" : "?";
            var sPosStr = sPos.HasValue ? $"({sPos.Value.x:F0},{sPos.Value.y:F0},{sPos.Value.z:F0})" : "?";
            _logger.LogInformation($"COMBAT_COORD: [GroupFormed] W@{posStr} flags=0x{wFlags:X} target={wTarget:X} hp={wHp:P0} | S@{sPosStr} spells={shamanSpells}");
        }

        // Check if warrior is in combat
        if (IsInCombat(fgSnapshot))
        {
            ResolveSpells(bgSnapshot);
            _logger.LogInformation($"COMBAT_COORD: Warrior in combat, target={GetTargetGuid(fgSnapshot):X}. Entering CombatSupport.");
            TransitionTo(CoordinatorState.CombatSupport);
            return null; // Will handle next tick
        }

        // Out of combat: follow the warrior
        return TryFollowWarrior(fgSnapshot, bgSnapshot);
    }

    private ActionMessage? HandleCombatSupport(string requestingAccount,
        WoWActivitySnapshot fgSnapshot, WoWActivitySnapshot bgSnapshot)
    {
        // Only send actions to background bot
        if (requestingAccount != _backgroundAccount)
            return null;

        var warriorTargetGuid = GetTargetGuid(fgSnapshot);
        var warriorHpRatio = GetHealthRatio(fgSnapshot);

        // If warrior is dead or nearly dead (< 1% HP), return to GroupFormed
        if (warriorHpRatio < 0.01f)
        {
            _logger.LogWarning($"COMBAT_COORD: Warrior is DEAD (HP={warriorHpRatio:P1}). Returning to GroupFormed.");
            TransitionTo(CoordinatorState.GroupFormed);
            return null;
        }

        // If warrior is no longer in combat, go back to GroupFormed
        if (!IsInCombat(fgSnapshot))
        {
            _logger.LogInformation("COMBAT_COORD: Warrior no longer in combat. Returning to GroupFormed.");
            TransitionTo(CoordinatorState.GroupFormed);
            return null;
        }

        // Check shaman mana
        var shamanManaRatio = GetManaRatio(bgSnapshot);
        if (shamanManaRatio < MANA_LOW_THRESHOLD)
        {
            _logger.LogInformation($"COMBAT_COORD: Shaman mana low ({shamanManaRatio:P0}). Entering ShamanResting.");
            TransitionTo(CoordinatorState.ShamanResting);
            return null;
        }

        // If shaman is too far from warrior, move closer first
        var followAction = TryFollowWarrior(fgSnapshot, bgSnapshot);
        if (followAction != null)
            return followAction;

        // Throttle actions
        if ((DateTime.UtcNow - _lastActionSentAt).TotalSeconds < ACTION_COOLDOWN_SEC)
            return null;

        // Decision: heal or DPS?
        var warriorGuid = GetPlayerGuid(fgSnapshot);

        if (warriorHpRatio < HEAL_THRESHOLD && _healSpellId != 0)
        {
            // Heal the warrior
            _logger.LogInformation($"COMBAT_COORD: Warrior HP={warriorHpRatio:P0}, casting heal (spell {_healSpellId}) on warrior {warriorGuid:X}");
            _lastActionSentAt = DateTime.UtcNow;
            return MakeCastSpellAction((int)_healSpellId, (long)warriorGuid);
        }

        if (warriorTargetGuid != 0 && _damageSpellId != 0)
        {
            // DPS the warrior's target
            _logger.LogInformation($"COMBAT_COORD: Warrior HP={warriorHpRatio:P0}, casting damage (spell {_damageSpellId}) on target {warriorTargetGuid:X}");
            _lastActionSentAt = DateTime.UtcNow;
            return MakeCastSpellAction((int)_damageSpellId, (long)warriorTargetGuid);
        }

        // Warrior in combat but no target yet — wait for warrior to acquire a target
        return null;
    }

    private ActionMessage? HandleShamanResting(string requestingAccount,
        WoWActivitySnapshot fgSnapshot, WoWActivitySnapshot bgSnapshot)
    {
        if (requestingAccount != _backgroundAccount)
            return null;

        // If warrior is in combat and low health, override rest to heal
        if (IsInCombat(fgSnapshot) && GetHealthRatio(fgSnapshot) < 0.30f && _healSpellId != 0)
        {
            var warriorGuid = GetPlayerGuid(fgSnapshot);
            _logger.LogInformation($"COMBAT_COORD: Emergency heal! Warrior at {GetHealthRatio(fgSnapshot):P0}");
            _lastActionSentAt = DateTime.UtcNow;
            return MakeCastSpellAction((int)_healSpellId, (long)warriorGuid);
        }

        var shamanManaRatio = GetManaRatio(bgSnapshot);
        if (shamanManaRatio >= MANA_REGEN_THRESHOLD)
        {
            _logger.LogInformation($"COMBAT_COORD: Shaman mana recovered ({shamanManaRatio:P0}). Returning to GroupFormed.");
            TransitionTo(CoordinatorState.GroupFormed);
        }

        return null;
    }

    // ===== Helpers =====

    private void ResolveSpells(WoWActivitySnapshot bgSnapshot)
    {
        if (_spellsResolved) return;

        var spellList = bgSnapshot.Player?.SpellList;
        if (spellList == null || spellList.Count == 0) return;

        _healSpellId = ShamanSpells.FindBestSpell(ShamanSpells.HealingWave, spellList);
        if (_healSpellId == 0)
            _healSpellId = ShamanSpells.FindBestSpell(ShamanSpells.LesserHealingWave, spellList);

        _damageSpellId = ShamanSpells.FindBestSpell(ShamanSpells.LightningBolt, spellList);
        _earthShockId = ShamanSpells.FindBestSpell(ShamanSpells.EarthShock, spellList);

        _logger.LogInformation($"COMBAT_COORD: Resolved shaman spells — Heal={_healSpellId}, Damage={_damageSpellId}, Interrupt={_earthShockId} (from {spellList.Count} known spells)");
        _spellsResolved = true;
    }

    private ActionMessage? TryFollowWarrior(WoWActivitySnapshot fgSnapshot, WoWActivitySnapshot bgSnapshot)
    {
        if ((DateTime.UtcNow - _lastFollowSentAt).TotalSeconds < FOLLOW_COOLDOWN_SEC)
            return null;

        var warriorPos = GetPosition(fgSnapshot);
        var shamanPos = GetPosition(bgSnapshot);

        if (warriorPos == null || shamanPos == null) return null;

        var dx = warriorPos.Value.x - shamanPos.Value.x;
        var dy = warriorPos.Value.y - shamanPos.Value.y;
        var dz = warriorPos.Value.z - shamanPos.Value.z;
        var dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        if (dist > FOLLOW_DISTANCE)
        {
            _lastFollowSentAt = DateTime.UtcNow;
            _logger.LogInformation($"COMBAT_COORD: Shaman following warrior ({dist:F1}y away)");
            return MakeGoToAction(warriorPos.Value.x, warriorPos.Value.y, warriorPos.Value.z);
        }

        return null;
    }

    private static bool IsInCombat(WoWActivitySnapshot snapshot)
    {
        var unitFlags = snapshot.Player?.Unit?.UnitFlags ?? 0;
        return (unitFlags & 0x80000) != 0; // UNIT_FLAG_IN_COMBAT = 0x80000
    }

    private static ulong GetTargetGuid(WoWActivitySnapshot snapshot)
    {
        return snapshot.Player?.Unit?.TargetGuid ?? 0;
    }

    private static ulong GetPlayerGuid(WoWActivitySnapshot snapshot)
    {
        return snapshot.Player?.Unit?.GameObject?.Base?.Guid ?? 0;
    }

    private static float GetHealthRatio(WoWActivitySnapshot snapshot)
    {
        var health = snapshot.Player?.Unit?.Health ?? 0;
        var maxHealth = snapshot.Player?.Unit?.MaxHealth ?? 1;
        return maxHealth > 0 ? (float)health / maxHealth : 1f;
    }

    private static float GetManaRatio(WoWActivitySnapshot snapshot)
    {
        var power = snapshot.Player?.Unit?.Power;
        var maxPower = snapshot.Player?.Unit?.MaxPower;
        if (power == null || maxPower == null) return 1f;

        // Power type 0 = Mana
        power.TryGetValue(0, out var mana);
        maxPower.TryGetValue(0, out var maxMana);
        return maxMana > 0 ? (float)mana / maxMana : 1f;
    }

    private static (float x, float y, float z)? GetPosition(WoWActivitySnapshot snapshot)
    {
        var pos = snapshot.Player?.Unit?.GameObject?.Base?.Position;
        if (pos == null) return null;
        return (pos.X, pos.Y, pos.Z);
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

    private static ActionMessage MakeGoToAction(float x, float y, float z)
    {
        var action = new ActionMessage { ActionType = ActionType.Goto };
        action.Parameters.Add(new RequestParameter { FloatParam = x });
        action.Parameters.Add(new RequestParameter { FloatParam = y });
        action.Parameters.Add(new RequestParameter { FloatParam = z });
        action.Parameters.Add(new RequestParameter { FloatParam = 5f }); // tolerance (stop within 5y)
        return action;
    }
}
