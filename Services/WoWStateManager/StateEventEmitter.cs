using System;
using Communication;

namespace WoWStateManager
{
    /// <summary>
    /// Centralized event surface for StateManager state changes. Consumers (UI push
    /// client, future telemetry, future automation hooks) subscribe to typed events
    /// instead of polling shared state. This lets the StateManager push to the UI
    /// only when something actually changes, rather than on a fixed timer.
    /// </summary>
    public sealed class StateEventEmitter
    {
        /// <summary>
        /// Raised after a bot pushes a full (non-heartbeat) snapshot and the listener
        /// has stored it. The payload is the snapshot as recorded.
        /// </summary>
        public event Action<WoWActivitySnapshot>? BotSnapshotUpdated;

        /// <summary>
        /// Raised when a bot's tracked process exits or the StateManager removes its
        /// snapshot slot. Subscribers can purge their local cache for this account.
        /// </summary>
        public event Action<string>? BotDisconnected;

        internal void RaiseBotSnapshotUpdated(WoWActivitySnapshot snapshot)
        {
            try { BotSnapshotUpdated?.Invoke(snapshot); }
            catch { /* subscriber failures must not bubble into the listener */ }
        }

        internal void RaiseBotDisconnected(string accountName)
        {
            try { BotDisconnected?.Invoke(accountName); }
            catch { /* subscriber failures must not bubble into the listener */ }
        }
    }
}
