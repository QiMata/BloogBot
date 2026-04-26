using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Microsoft.Extensions.Logging;
using WoWStateManager.Settings;

namespace WoWStateManager.Modes
{
    /// <summary>
    /// Handler for <see cref="StateManagerMode.Automated"/>. At world-entry,
    /// dispatches a single <c>APPLY_LOADOUT</c> action per character whose
    /// <see cref="CharacterSettings.Loadout"/> is populated. The bot owns
    /// pacing of the loadout plan (see <c>BotRunner.Tasks.LoadoutTask</c>)
    /// and reports completion through
    /// <see cref="WoWActivitySnapshot.LoadoutStatus"/>.
    ///
    /// The activity itself (<see cref="CharacterSettings.AssignedActivity"/>)
    /// is started by the bot at world-entry from the
    /// <c>WWOW_ASSIGNED_ACTIVITY</c> env var via
    /// <c>BotRunner.Activities.ActivityResolver</c>; this handler does not
    /// re-dispatch it.
    /// </summary>
    public sealed class AutomatedModeHandler : IStateManagerModeHandler
    {
        private readonly ILogger<AutomatedModeHandler> _logger;

        private readonly ConcurrentDictionary<string, byte> _loadoutDispatched =
            new(StringComparer.OrdinalIgnoreCase);

        public AutomatedModeHandler(ILogger<AutomatedModeHandler> logger)
        {
            _logger = logger;
        }

        public StateManagerMode Mode => StateManagerMode.Automated;

        public Task OnWorldEntryAsync(
            CharacterSettings character,
            Func<string, ActionMessage, bool> enqueueAction,
            CancellationToken cancellationToken)
        {
            var accountName = character.AccountName;

            if (character.Loadout == null)
            {
                _logger.LogInformation(
                    "[MODE=Automated] World-entry for '{Account}' has no Loadout; activity '{Activity}' " +
                    "will run via bot-side ActivityResolver (env WWOW_ASSIGNED_ACTIVITY).",
                    accountName,
                    character.AssignedActivity ?? "(none)");
                return Task.CompletedTask;
            }

            if (!_loadoutDispatched.TryAdd(accountName, 0))
            {
                _logger.LogDebug(
                    "[MODE=Automated] World-entry for '{Account}': APPLY_LOADOUT already dispatched, skipping.",
                    accountName);
                return Task.CompletedTask;
            }

            var action = LoadoutSpecConverter.BuildApplyLoadoutAction(character.Loadout);
            var enqueued = enqueueAction(accountName, action);
            if (!enqueued)
            {
                // Listener rejected (dead/ghost guard or capacity). Reset the
                // gate so the next world-entry tick retries — this keeps the
                // contract "exactly once per ready world-entry", not "at most
                // once forever".
                _loadoutDispatched.TryRemove(accountName, out _);
                _logger.LogWarning(
                    "[MODE=Automated] APPLY_LOADOUT enqueue rejected for '{Account}'; will retry on next world-ready snapshot.",
                    accountName);
                return Task.CompletedTask;
            }

            _logger.LogInformation(
                "[MODE=Automated] World-entry dispatched APPLY_LOADOUT for '{Account}' " +
                "(spells={Spells}, equip={Equip}, skills={Skills}, supplemental={Supplemental}, " +
                "elixirs={Elixirs}, talents='{Talents}', activity='{Activity}').",
                accountName,
                action.LoadoutSpec.SpellIdsToLearn.Count,
                action.LoadoutSpec.EquipItems.Count,
                action.LoadoutSpec.Skills.Count,
                action.LoadoutSpec.SupplementalItemIds.Count,
                action.LoadoutSpec.ElixirItemIds.Count,
                string.IsNullOrWhiteSpace(action.LoadoutSpec.TalentTemplate) ? "(none)" : action.LoadoutSpec.TalentTemplate,
                character.AssignedActivity ?? "(none)");

            return Task.CompletedTask;
        }

        public Task OnSnapshotAsync(
            CharacterSettings character,
            WoWActivitySnapshot snapshot,
            Func<string, ActionMessage, bool> enqueueAction,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnExternalActivityRequestAsync(
            string requestingPlayer,
            string activityDescriptor,
            Func<string, ActionMessage, bool> enqueueAction,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "[MODE=Automated] Ignoring external activity request from '{Player}' for '{Descriptor}': " +
                "Automated mode does not service on-demand requests (handled by OnDemandActivities).",
                requestingPlayer,
                activityDescriptor);
            return Task.CompletedTask;
        }
    }
}
