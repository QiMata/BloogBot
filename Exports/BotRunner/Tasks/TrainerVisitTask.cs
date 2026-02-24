using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;
using System.Threading;

namespace BotRunner.Tasks;

/// <summary>
/// Task that walks to a class trainer NPC, learns all available spells, then pops itself.
/// Pushed by StateManager on level-up when a trainer NPC is nearby.
/// </summary>
public class TrainerVisitTask : BotTask, IBotTask
{
    private enum TrainerState { FindTrainer, MoveToTrainer, LearnSpells, Done }
    private TrainerState _state = TrainerState.FindTrainer;

    private IWoWUnit? _trainerUnit;
    private ulong _trainerGuid;
    private DateTime _stateEnteredAt = DateTime.Now;
    private int _actionAttempts;
    // Uses Config.NpcInteractRange and Config.StuckTimeoutMs

    public TrainerVisitTask(IBotContext botContext) : base(botContext) { }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            Pop();
            return;
        }

        // Abort if in combat
        if (ObjectManager.Aggressors.Any())
        {
            Log.Information("[TRAINER] Combat detected, aborting trainer visit");
            ObjectManager.StopAllMovement();
            Pop();
            return;
        }

        // Timeout
        if ((DateTime.Now - _stateEnteredAt).TotalMilliseconds > Config.StuckTimeoutMs)
        {
            Log.Warning("[TRAINER] Timed out in {State}, aborting", _state);
            ObjectManager.StopAllMovement();
            Pop();
            return;
        }

        switch (_state)
        {
            case TrainerState.FindTrainer:
                FindTrainer(player);
                break;
            case TrainerState.MoveToTrainer:
                MoveToTrainer(player);
                break;
            case TrainerState.LearnSpells:
                LearnSpells();
                break;
            case TrainerState.Done:
                Pop();
                break;
        }
    }

    private void FindTrainer(IWoWUnit player)
    {
        var playerClass = (player as IWoWPlayer)?.Class ?? Class.Warrior;

        // Prefer trainers matching our class; skip trainers for other classes or professions
        var trainers = ObjectManager.Units
            .Where(u => u.Health > 0
                && u.Position != null
                && (u.NpcFlags & NPCFlags.UNIT_NPC_FLAG_TRAINER) != 0)
            .OrderBy(u => player.Position.DistanceTo(u.Position))
            .ToList();

        // First: look for a trainer whose name contains our class name
        _trainerUnit = trainers.FirstOrDefault(u => IsClassTrainerMatch(u.Name, playerClass));

        // Second: accept any trainer that isn't obviously wrong (no other class/profession in name)
        _trainerUnit ??= trainers.FirstOrDefault(u => !IsWrongTrainer(u.Name, playerClass));

        if (_trainerUnit == null)
        {
            Log.Warning("[TRAINER] No suitable class trainer found nearby for {Class}, aborting", playerClass);
            Pop();
            return;
        }

        _trainerGuid = _trainerUnit.Guid;
        var dist = player.Position.DistanceTo(_trainerUnit.Position);
        Log.Information("[TRAINER] Found trainer: {Name} ({Dist:F0}y away, class: {Class})", _trainerUnit.Name, dist, playerClass);
        SetState(TrainerState.MoveToTrainer);
    }

    /// <summary>
    /// Returns true if the NPC name contains the player's class name (likely a matching class trainer).
    /// </summary>
    public static bool IsClassTrainerMatch(string npcName, Class playerClass)
    {
        if (string.IsNullOrEmpty(npcName)) return false;
        var className = GetClassKeyword(playerClass);
        return className != null && npcName.Contains(className, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true if the NPC is definitely NOT a suitable trainer
    /// (contains a different class keyword or a profession keyword).
    /// </summary>
    public static bool IsWrongTrainer(string npcName, Class playerClass)
    {
        if (string.IsNullOrEmpty(npcName)) return false;

        // Check for profession trainer keywords
        foreach (var prof in ProfessionKeywords)
        {
            if (npcName.Contains(prof, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check for other class trainer keywords
        var myKeyword = GetClassKeyword(playerClass);
        foreach (var (cls, keyword) in ClassKeywords)
        {
            if (cls != playerClass && npcName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                // Exception: don't reject if our own class keyword is also in the name
                if (myKeyword != null && npcName.Contains(myKeyword, StringComparison.OrdinalIgnoreCase))
                    continue;
                return true;
            }
        }

        return false;
    }

    private static string? GetClassKeyword(Class playerClass)
    {
        foreach (var (cls, keyword) in ClassKeywords)
        {
            if (cls == playerClass) return keyword;
        }
        return null;
    }

    private static readonly (Class, string)[] ClassKeywords =
    [
        (Class.Warrior, "Warrior"),
        (Class.Paladin, "Paladin"),
        (Class.Hunter, "Hunter"),
        (Class.Rogue, "Rogue"),
        (Class.Priest, "Priest"),
        (Class.Shaman, "Shaman"),
        (Class.Mage, "Mage"),
        (Class.Warlock, "Warlock"),
        (Class.Druid, "Druid"),
    ];

    private static readonly string[] ProfessionKeywords =
    [
        "Mining", "Herbalism", "Skinning", "Fishing", "Cooking", "First Aid",
        "Blacksmithing", "Leatherworking", "Tailoring", "Engineering",
        "Enchanting", "Alchemy", "Riding", "Weapon Master",
    ];

    private void MoveToTrainer(IWoWUnit player)
    {
        if (_trainerUnit == null || _trainerUnit.Position == null)
        {
            SetState(TrainerState.FindTrainer);
            return;
        }

        var dist = player.Position.DistanceTo(_trainerUnit.Position);
        if (dist <= Config.NpcInteractRange)
        {
            ObjectManager.StopAllMovement();
            SetState(TrainerState.LearnSpells);
            return;
        }

        // Pathfind toward trainer (NavigationPath caches + throttles internally)
        NavigateToward(_trainerUnit.Position);
    }

    private void LearnSpells()
    {
        if (!Wait.For("trainer_learn", 1500, true))
            return;

        _actionAttempts++;
        if (_actionAttempts > 3)
        {
            Log.Warning("[TRAINER] Too many learn attempts, aborting");
            SetState(TrainerState.Done);
            return;
        }

        try
        {
            ObjectManager.SetTarget(_trainerGuid);

            var learned = ObjectManager.LearnAllAvailableSpellsAsync(_trainerGuid, CancellationToken.None)
                .GetAwaiter().GetResult();

            Log.Information("[TRAINER] Learned {Count} spells from trainer", learned);

            // Refresh spell list
            ObjectManager.RefreshSpells();

            SetState(TrainerState.Done);
        }
        catch (Exception ex)
        {
            Log.Warning("[TRAINER] Learn failed: {Error}", ex.Message);
        }
    }

    private void SetState(TrainerState newState)
    {
        if (_state != newState)
        {
            _state = newState;
            _stateEnteredAt = DateTime.Now;
            _actionAttempts = 0;
        }
    }

    private void Pop()
    {
        Wait.Remove("trainer_move");
        Wait.Remove("trainer_learn");
        BotTasks.Pop();
    }
}
