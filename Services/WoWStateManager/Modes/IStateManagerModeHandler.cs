using System;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using WoWStateManager.Settings;

namespace WoWStateManager.Modes
{
    /// <summary>
    /// Per-mode dispatch contract. The handler is selected at startup from
    /// <see cref="StateManagerSettings.Mode"/> and decides what StateManager
    /// does at world-entry, on every snapshot tick, and in response to
    /// external (Shodan / WPF UI) activity requests.
    ///
    /// Action delivery is host-supplied: the call site (typically
    /// <c>CharacterStateSocketListener</c>) passes its own
    /// <c>EnqueueAction(accountName, action)</c> in via the
    /// <c>enqueueAction</c> delegate so handlers stay free of any reference
    /// to the listener and can be unit-tested in isolation.
    ///
    /// See docs/statemanager_modes_design.md for the design rationale.
    /// </summary>
    public interface IStateManagerModeHandler
    {
        /// <summary>
        /// The mode this handler implements. Must match the value
        /// chosen by <see cref="StateManagerSettings.Mode"/>.
        /// </summary>
        StateManagerMode Mode { get; }

        /// <summary>
        /// Called once per character the first time their snapshot becomes
        /// world-ready (<c>IsObjectManagerValid == true</c>). The Test mode
        /// implementation is a no-op; Automated dispatches APPLY_LOADOUT and
        /// relies on the bot-side <c>ActivityResolver</c> to start
        /// <see cref="CharacterSettings.AssignedActivity"/> from the
        /// <c>WWOW_ASSIGNED_ACTIVITY</c> env var.
        /// </summary>
        /// <param name="enqueueAction">
        /// Delegate the handler invokes to enqueue an action against an
        /// account. Returns true when the action was queued, false when it
        /// was rejected (e.g. dead/ghost guard, queue at capacity).
        /// </param>
        Task OnWorldEntryAsync(
            CharacterSettings character,
            Func<string, ActionMessage, bool> enqueueAction,
            CancellationToken cancellationToken);

        /// <summary>
        /// Called on every snapshot tick that arrives for a configured
        /// character. Test mode is a no-op (the existing per-snapshot
        /// coordinator dispatch is unchanged); Automated currently no-ops
        /// here because the activity loop is driven by the bot-side task
        /// stack populated at world entry.
        /// </summary>
        Task OnSnapshotAsync(
            CharacterSettings character,
            WoWActivitySnapshot snapshot,
            Func<string, ActionMessage, bool> enqueueAction,
            CancellationToken cancellationToken);

        /// <summary>
        /// Called when an external system (Shodan whisper, WPF UI POST)
        /// asks for an activity to be dispatched on demand. Test mode
        /// throws; Automated ignores; OnDemand resolves and dispatches.
        /// </summary>
        Task OnExternalActivityRequestAsync(
            string requestingPlayer,
            string activityDescriptor,
            Func<string, ActionMessage, bool> enqueueAction,
            CancellationToken cancellationToken);
    }
}
