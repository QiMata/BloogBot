using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace BotRunner.Activities;

/// <summary>
/// "Fishing[Ratchet]" activity.
///
/// With <c>useGmCommands=true</c>, the bot self-stages via GM chat:
///   1. Ensure fishing pole (6256) + Nightcrawler bait (6530) are in bags.
///   2. Learn Fishing rank 1 (7620) and Fishing Pole proficiency (7738).
///   3. Set fishing skill to 75/300.
///   4. Refresh the Barrens master fishing pool (2628) for Ratchet tests.
///   5. Self-teleport to the Ratchet packet-capture dock.
///   6. Push <see cref="FishingTask"/> without hardcoded geometry; elevated
///      dock/pier cast-position resolution belongs to
///      <see cref="FishingCastPositionFinder"/> and is invoked from
///      <see cref="FishingTask"/>.
///
/// With <c>useGmCommands=false</c>, we don't have the natural travel/train
/// path wired yet — the task emits a diagnostic and pops so the default
/// idle sequence takes over. Filling that in is the DecisionEngine's job.
/// </summary>
internal sealed class FishingAtRatchetActivity : IActivity
{
    public string Name => "Fishing";
    public string? Location => "Ratchet";

    public IBotTask CreateTask(IBotContext context, bool useGmCommands)
        => new FishingAtRatchetTask(context, useGmCommands);

    // Ratchet packet-capture dock (staging position). Matches the staging
    // dock used by the previous FishingProfessionTests so we're fishing
    // the same real off-shore child pools.
    private const int RatchetMapId = 1;
    private const int RatchetMasterPoolId = 2628;
    private const float RatchetStageX = -949.932f;
    private const float RatchetStageY = -3766.883f;
    private const float RatchetStageZ = 5.949f; // slight lift so .go xyz doesn't underflow

    private sealed class FishingAtRatchetTask : BotTask, IBotTask
    {
        private enum Phase
        {
            Outfit,
            Teleport,
            Fish,
            Done,
        }

        private readonly bool _useGmCommands;
        private Phase _phase;
        private int _outfitStep;
        private int _teleportSettleTicks;
        private bool _announced;

        public FishingAtRatchetTask(IBotContext context, bool useGmCommands) : base(context)
        {
            _useGmCommands = useGmCommands;
            _phase = Phase.Outfit;
        }

        public void Update()
        {
            if (!_announced)
            {
                _announced = true;
                BotContext.AddDiagnosticMessage(
                    $"[ACTIVITY] FishingAtRatchet start useGm={_useGmCommands} phase={_phase}");
            }

            if (!_useGmCommands)
            {
                BotContext.AddDiagnosticMessage(
                    "[ACTIVITY] FishingAtRatchet non-GM path not yet implemented; falling back to idle.");
                PopTask("non_gm_path_not_implemented");
                return;
            }

            switch (_phase)
            {
                case Phase.Outfit:
                    TickOutfit();
                    break;
                case Phase.Teleport:
                    TickTeleport();
                    break;
                case Phase.Fish:
                    TickPushFishingTask();
                    break;
                case Phase.Done:
                    // FishingTask was pushed on top; when it pops after loot_success,
                    // control returns here. Leave the activity task parked so it
                    // doesn't interfere with normal idle/action dispatch.
                    break;
            }
        }

        private void TickOutfit()
        {
            // One chat command per tick to avoid flooding. BotRunnerService
            // ticks at ~100ms so this sequence completes in ~1 second.
            var om = ObjectManager;
            switch (_outfitStep)
            {
                case 0:
                    om.SendChatMessage(".additem 6256 1"); // Fishing Pole
                    break;
                case 1:
                    om.SendChatMessage(".additem 6530 1"); // Nightcrawler Bait
                    break;
                case 2:
                    om.SendChatMessage($".learn {FishingData.FishingRank1}");
                    break;
                case 3:
                    om.SendChatMessage($".learn {FishingData.FishingPoleProficiency}");
                    break;
                case 4:
                    om.SendChatMessage($".setskill {FishingData.FishingSkillId} 75 300");
                    break;
                case 5:
                    om.SendChatMessage($".pool update {RatchetMasterPoolId}");
                    BotContext.AddDiagnosticMessage(
                        $"[ACTIVITY] FishingAtRatchet pool_refresh_dispatched master={RatchetMasterPoolId}");
                    break;
                case 6:
                    BotContext.AddDiagnosticMessage("[ACTIVITY] FishingAtRatchet outfit_complete");
                    _phase = Phase.Teleport;
                    _teleportSettleTicks = 0;
                    break;
            }

            _outfitStep++;
        }

        private void TickTeleport()
        {
            if (_teleportSettleTicks == 0)
            {
                ObjectManager.SendChatMessage(
                    $".go xyz {RatchetStageX} {RatchetStageY} {RatchetStageZ} {RatchetMapId}");
                BotContext.AddDiagnosticMessage(
                    $"[ACTIVITY] FishingAtRatchet teleport_dispatched pos=({RatchetStageX:F1},{RatchetStageY:F1},{RatchetStageZ:F1}) map={RatchetMapId}");
            }

            _teleportSettleTicks++;

            // Give the server ~3s to apply the teleport before kicking off
            // FishingTask; FishingTask itself will wait for valid world state
            // via WorldEntryHydration.
            if (_teleportSettleTicks >= 30)
            {
                _phase = Phase.Fish;
            }
        }

        private void TickPushFishingTask()
        {
            BotContext.AddDiagnosticMessage("[ACTIVITY] FishingAtRatchet push_fishing_task");
            BotTasks.Push(new FishingTask(BotContext, null));
            _phase = Phase.Done;
        }
    }
}
