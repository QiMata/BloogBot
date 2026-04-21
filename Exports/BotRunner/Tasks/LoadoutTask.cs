using BotRunner.Interfaces;
using Communication;

namespace BotRunner.Tasks;

/// <summary>
/// P3.3: Executes a <see cref="Communication.LoadoutSpec"/> received via
/// <c>ActionType.ApplyLoadout</c>. One task per bot, created exactly once
/// per loadout hand-off from StateManager; reports progress through the
/// shared <c>WoWActivitySnapshot.LoadoutStatus</c> field so the coordinator
/// can gate raid-formation / queue kick-off on every bot reaching
/// <see cref="LoadoutStatus.LoadoutReady"/>.
///
/// The task is deliberately plan-first and idempotent: it computes the full
/// ordered step list up-front from the spec, then walks it one step per
/// <see cref="Update"/> call. Each step may issue a GM chat command, a SOAP
/// command, or push a child task (e.g. <c>EquipItem</c>) onto the task stack.
/// "Already-known spell" / "already-equipped item" responses are treated as
/// success so a second ApplyLoadout is safe.
///
/// This scaffold lands the state machine and status plumbing. The concrete
/// step executors (chat dispatch, SOAP calls, equip-item sub-tasks) arrive
/// in follow-up commits alongside the fixture cleanup (P3.6); keeping them
/// separate prevents a single giant diff from landing half-done.
/// </summary>
public sealed class LoadoutTask : IBotTask
{
    private readonly IBotContext _context;
    private readonly LoadoutSpec _spec;
    private LoadoutStatus _status = LoadoutStatus.LoadoutNotStarted;
    private string _failureReason = string.Empty;
    private bool _planBuilt;

    public LoadoutTask(IBotContext context, LoadoutSpec spec)
    {
        _context = context ?? throw new System.ArgumentNullException(nameof(context));
        _spec = spec ?? throw new System.ArgumentNullException(nameof(spec));
    }

    /// <summary>Current progress through the loadout plan.</summary>
    public LoadoutStatus Status => _status;

    /// <summary>Populated when <see cref="Status"/> is <c>LoadoutFailed</c>.</summary>
    public string FailureReason => _failureReason;

    /// <summary>The spec this task is applying. Exposed for tests + diagnostics.</summary>
    public LoadoutSpec Spec => _spec;

    public void Update()
    {
        if (_status == LoadoutStatus.LoadoutReady || _status == LoadoutStatus.LoadoutFailed)
            return;

        if (!_planBuilt)
        {
            _planBuilt = true;
            _status = LoadoutStatus.LoadoutInProgress;
            // Plan construction happens here in follow-ups. The spec fields
            // are captured on construction so the plan is deterministic even
            // if the coordinator later re-sends the action.
        }

        // Placeholder: until the concrete executors land, a LoadoutTask with
        // an empty spec (TargetLevel=0, no items, no spells) immediately
        // transitions to Ready so the coordinator state machine can be
        // exercised end-to-end with the minimal loadout path. Non-empty
        // specs stay InProgress and rely on follow-up work to complete.
        if (IsEffectivelyEmpty(_spec))
        {
            _status = LoadoutStatus.LoadoutReady;
        }
    }

    private static bool IsEffectivelyEmpty(LoadoutSpec spec)
    {
        return spec.TargetLevel == 0
            && spec.HonorRank == 0
            && spec.RidingSkill == 0
            && spec.MountSpellId == 0
            && spec.ArmorSetId == 0
            && spec.SpellIdsToLearn.Count == 0
            && spec.Skills.Count == 0
            && spec.EquipItems.Count == 0
            && spec.SupplementalItemIds.Count == 0
            && spec.ElixirItemIds.Count == 0
            && spec.FactionReps.Count == 0
            && spec.CompletedQuestIds.Count == 0
            && string.IsNullOrEmpty(spec.TalentTemplate);
    }
}
