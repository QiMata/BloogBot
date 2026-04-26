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
    /// See docs/statemanager_modes_design.md for the design rationale.
    /// F-1 step 2 introduces the interface plus the no-op
    /// <see cref="TestModeHandler"/>; live call-site wiring lands with
    /// step 3 alongside <c>AutomatedModeHandler</c>.
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
        /// then parses <see cref="CharacterSettings.AssignedActivity"/>.
        /// </summary>
        Task OnWorldEntryAsync(
            CharacterSettings character,
            CancellationToken cancellationToken);

        /// <summary>
        /// Called on every snapshot tick that arrives for a configured
        /// character. Test mode is a no-op (the existing per-snapshot
        /// coordinator dispatch is unchanged); Automated drives the
        /// activity loop here.
        /// </summary>
        Task OnSnapshotAsync(
            CharacterSettings character,
            WoWActivitySnapshot snapshot,
            CancellationToken cancellationToken);

        /// <summary>
        /// Called when an external system (Shodan whisper, WPF UI POST)
        /// asks for an activity to be dispatched on demand. Test mode
        /// throws; Automated ignores; OnDemand resolves and dispatches.
        /// </summary>
        Task OnExternalActivityRequestAsync(
            string requestingPlayer,
            string activityDescriptor,
            CancellationToken cancellationToken);
    }
}
