using System;
using System.IO;
using System.Threading;
using Serilog;

namespace ForegroundBotRunner.Mem.Hooks
{
    /// <summary>
    /// Packet-driven connection state machine for the FG bot.
    ///
    /// Tracks the client's lifecycle based on observed packets and memory state:
    ///   DISCONNECTED → AUTHENTICATING → CHAR_SELECT → ENTERING_WORLD
    ///     → IN_WORLD → TRANSFERRING → IN_WORLD (or back to DISCONNECTED)
    ///
    /// This replaces heuristic polling of ContinentId/ManagerBase with
    /// deterministic state transitions based on:
    ///   1. PacketLogger send/recv events (CMSG/SMSG opcodes)
    ///   2. ContinentId memory reads (as confirmation, not primary signal)
    ///   3. SignalEventManager game events
    ///
    /// The state machine provides:
    ///   - IsLuaSafe: whether Lua calls can be dispatched
    ///   - IsObjectManagerValid: whether object iteration is safe
    ///   - IsSendingSafe: whether outbound packets can be sent
    ///   - State change events for ForegroundBotWorker to react to
    /// </summary>
    public class ConnectionStateMachine
    {
        // Well-known 1.12.1 opcodes relevant to state transitions
        private static class Opcodes
        {
            // Auth / Login
            public const ushort CMSG_AUTH_SESSION = 0x01ED;
            public const ushort SMSG_AUTH_RESPONSE = 0x01EE;
            public const ushort CMSG_CHAR_ENUM = 0x0037;
            public const ushort SMSG_CHAR_ENUM = 0x003B;
            public const ushort CMSG_PLAYER_LOGIN = 0x003D;
            public const ushort SMSG_LOGIN_VERIFY_WORLD = 0x0236;

            // Map transitions
            public const ushort SMSG_TRANSFER_PENDING = 0x003F;
            public const ushort SMSG_NEW_WORLD = 0x003E;
            public const ushort MSG_MOVE_WORLDPORT_ACK = 0x00DC;
            public const ushort SMSG_TRANSFER_ABORT = 0x0040;

            // Same-map teleport (e.g., GM .tele within same continent)
            public const ushort MSG_MOVE_TELEPORT = 0x00C5;
            public const ushort MSG_MOVE_TELEPORT_ACK = 0x00C7;

            // Logout
            public const ushort CMSG_LOGOUT_REQUEST = 0x004B;
            public const ushort SMSG_LOGOUT_COMPLETE = 0x004D;

            // Object updates
            public const ushort SMSG_UPDATE_OBJECT = 0x00A9;
            public const ushort SMSG_COMPRESSED_UPDATE_OBJECT = 0x01F6;

            // Movement
            public const ushort MSG_MOVE_HEARTBEAT = 0x00EE;
            public const ushort CMSG_MOVE_STOP = 0x00B7;
        }

        /// <summary>Connection lifecycle states.</summary>
        public enum State
        {
            /// <summary>No connection to server.</summary>
            Disconnected,

            /// <summary>Auth handshake in progress.</summary>
            Authenticating,

            /// <summary>At character select screen. Lua is safe, ObjectManager is NOT.</summary>
            CharSelect,

            /// <summary>CMSG_PLAYER_LOGIN sent, waiting for world data. Lua may be unsafe.</summary>
            EnteringWorld,

            /// <summary>In world. Lua safe, ObjectManager valid, commands OK.</summary>
            InWorld,

            /// <summary>Map transfer in progress (SMSG_TRANSFER_PENDING received).
            /// Lua is NOT safe. ObjectManager is NOT valid. Block everything.</summary>
            Transferring,

            /// <summary>Logging out (CMSG_LOGOUT_REQUEST sent).</summary>
            LoggingOut
        }

        private State _currentState = State.Disconnected;
        private readonly object _stateLock = new();
        private DateTime _lastStateChange = DateTime.UtcNow;

        /// <summary>
        /// Teleport cooldown: when MSG_MOVE_TELEPORT is received (same-map teleport),
        /// ObjectManager reads are unsafe while WoW reshuffles its internal structures.
        /// The cooldown clears when MSG_MOVE_TELEPORT_ACK is sent or after a timeout.
        /// </summary>
        private long _teleportCooldownUntilTicks = DateTime.MinValue.Ticks;
        private const int TeleportCooldownMs = 2000;

        // Diagnostic logging
        private static readonly string DiagnosticLogPath;
        private static readonly object DiagnosticLogLock = new();

        /// <summary>Fired when state changes. Args: (oldState, newState).</summary>
        public event Action<State, State>? OnStateChanged;

        static ConnectionStateMachine()
        {
            string wowDir;
            try
            {
                wowDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
                    ?? AppContext.BaseDirectory;
            }
            catch { wowDir = AppContext.BaseDirectory; }

            var logsDir = Path.Combine(wowDir, "WWoWLogs");
            try { Directory.CreateDirectory(logsDir); } catch { }
            DiagnosticLogPath = Path.Combine(logsDir, "connection_state.log");
            try
            {
                File.WriteAllText(DiagnosticLogPath,
                    $"=== ConnectionStateMachine Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
            }
            catch { }
        }

        /// <summary>Current connection state.</summary>
        public State CurrentState
        {
            get { lock (_stateLock) return _currentState; }
        }

        /// <summary>How long we've been in the current state.</summary>
        public TimeSpan TimeInCurrentState => DateTime.UtcNow - _lastStateChange;

        /// <summary>Whether Lua calls can be safely dispatched.</summary>
        /// <remarks>
        /// Authenticating is included because the realm wizard dialog (loginState="realmwizard")
        /// appears BEFORE SMSG_CHAR_ENUM — Lua calls on Glue screens are safe.
        /// Without this, ThreadSynchronizer blocks all WM_USER processing during realm selection,
        /// preventing FgRealmSelectScreen strategies from executing.
        /// </remarks>
        public bool IsLuaSafe
        {
            get
            {
                var s = CurrentState;
                return s == State.Authenticating || s == State.CharSelect || s == State.InWorld;
            }
        }

        /// <summary>Whether the ObjectManager is valid for object iteration.</summary>
        /// <remarks>
        /// Returns false during same-map teleport cooldown (MSG_MOVE_TELEPORT received).
        /// WoW reshuffles internal structures during teleport — reading memory is unsafe.
        /// </remarks>
        public bool IsObjectManagerValid => CurrentState == State.InWorld && !IsTeleportCooldownActive;

        /// <summary>Whether outbound gameplay packets (movement, spells) can be sent.</summary>
        public bool IsSendingSafe => CurrentState == State.InWorld;

        /// <summary>True when a same-map teleport is being processed.</summary>
        public bool IsTeleportCooldownActive => DateTime.UtcNow.Ticks < Interlocked.Read(ref _teleportCooldownUntilTicks);

        /// <summary>
        /// Process a captured packet event from PacketLogger.
        /// Call this from PacketLogger.OnPacketCaptured handler.
        /// </summary>
        public void ProcessPacket(PacketDirection direction, ushort opcode, int size)
        {
            // Same-map teleport cooldown: pause ObjectManager when teleport received,
            // clear when ACK sent (client has finished processing the teleport)
            if (direction == PacketDirection.Recv && opcode == Opcodes.MSG_MOVE_TELEPORT)
            {
                Interlocked.Exchange(ref _teleportCooldownUntilTicks, DateTime.UtcNow.AddMilliseconds(TeleportCooldownMs).Ticks);
                Statics.ObjectManager.BeginTeleportPause();
                DiagLog($"TELEPORT: same-map teleport received, ObjectManager paused for {TeleportCooldownMs}ms");
            }
            else if (direction == PacketDirection.Send && opcode == Opcodes.MSG_MOVE_TELEPORT_ACK)
            {
                Interlocked.Exchange(ref _teleportCooldownUntilTicks, DateTime.MinValue.Ticks);
                Statics.ObjectManager.EndTeleportPause();
                DiagLog("TELEPORT: ACK sent, ObjectManager resumed");
            }

            lock (_stateLock)
            {
                var oldState = _currentState;
                var newState = ComputeTransition(direction, opcode);

                if (newState != oldState)
                {
                    _currentState = newState;
                    _lastStateChange = DateTime.UtcNow;
                    DiagLog($"STATE: {oldState} → {newState} (opcode=0x{opcode:X4} dir={direction})");

                    // Cross-map transfer: pause ObjectManager IMMEDIATELY before WoW starts teardown.
                    // ContinentId hasn't changed yet, so the polling loop's isContinentTransition check
                    // won't catch it in time. PauseDuringTeleport is a volatile flag checked every poll.
                    if (newState == State.Transferring)
                    {
                        Statics.ObjectManager.BeginTeleportPause();
                        DiagLog("TRANSFER: ObjectManager paused (cross-map transfer)");
                    }
                    else if (oldState == State.Transferring && newState == State.InWorld)
                    {
                        Statics.ObjectManager.EndTeleportPause();
                        DiagLog("TRANSFER: ObjectManager resumed (back in world)");
                    }

                    try { OnStateChanged?.Invoke(oldState, newState); }
                    catch (Exception ex) { DiagLog($"OnStateChanged handler error: {ex.Message}"); }
                }
            }
        }

        /// <summary>
        /// Force a state transition from external observation (e.g., ContinentId reads).
        /// Used as a safety net when packet hooks miss a transition.
        /// </summary>
        public void ForceState(State newState, string reason)
        {
            lock (_stateLock)
            {
                if (_currentState == newState) return;

                var oldState = _currentState;
                _currentState = newState;
                _lastStateChange = DateTime.UtcNow;
                DiagLog($"STATE (forced): {oldState} → {newState} ({reason})");

                try { OnStateChanged?.Invoke(oldState, newState); }
                catch (Exception ex) { DiagLog($"OnStateChanged handler error: {ex.Message}"); }
            }
        }

        private State ComputeTransition(PacketDirection dir, ushort opcode)
        {
            // Send-side transitions (what the client sends)
            if (dir == PacketDirection.Send)
            {
                return opcode switch
                {
                    Opcodes.CMSG_AUTH_SESSION => State.Authenticating,
                    Opcodes.CMSG_PLAYER_LOGIN => State.EnteringWorld,
                    Opcodes.CMSG_LOGOUT_REQUEST => State.LoggingOut,
                    // MSG_MOVE_WORLDPORT_ACK: client acked the transfer
                    // We stay in Transferring until SMSG_LOGIN_VERIFY_WORLD arrives
                    Opcodes.MSG_MOVE_WORLDPORT_ACK => State.Transferring,
                    _ => _currentState
                };
            }

            // Recv-side transitions (what the server sends)
            return opcode switch
            {
                Opcodes.SMSG_AUTH_RESPONSE => State.Authenticating,
                Opcodes.SMSG_CHAR_ENUM => State.CharSelect,
                Opcodes.SMSG_TRANSFER_PENDING => State.Transferring,
                Opcodes.SMSG_NEW_WORLD => State.Transferring,
                Opcodes.SMSG_TRANSFER_ABORT => RestorePreTransferState(),
                Opcodes.SMSG_LOGIN_VERIFY_WORLD => State.InWorld,
                Opcodes.SMSG_LOGOUT_COMPLETE => State.CharSelect,
                _ => _currentState
            };
        }

        /// <summary>
        /// When a transfer is aborted, return to the previous in-world state.
        /// </summary>
        private State RestorePreTransferState()
        {
            DiagLog("Transfer aborted — restoring InWorld state");
            return State.InWorld;
        }

        private static void DiagLog(string message)
        {
            try
            {
                lock (DiagnosticLogLock)
                {
                    File.AppendAllText(DiagnosticLogPath,
                        $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                }
            }
            catch { }
        }
    }
}
