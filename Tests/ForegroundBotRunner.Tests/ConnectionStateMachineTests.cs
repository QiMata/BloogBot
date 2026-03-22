using ForegroundBotRunner.Mem.Hooks;
using static ForegroundBotRunner.Mem.Hooks.ConnectionStateMachine;

namespace ForegroundBotRunner.Tests;

/// <summary>
/// Unit tests for ConnectionStateMachine state transitions and teleport safety.
/// These prevent regressions in the FG bot's cross-map transfer crash fix.
///
/// Key invariant: When SMSG_TRANSFER_PENDING arrives, the state machine MUST
/// transition to Transferring AND call BeginTeleportPause BEFORE WoW starts
/// internal object teardown. ContinentId changes LATE — it's not a reliable sentinel.
/// </summary>
public class ConnectionStateMachineTests
{
    // Well-known 1.12.1 opcodes (matching ConnectionStateMachine.Opcodes)
    private const ushort CMSG_AUTH_SESSION = 0x01ED;
    private const ushort SMSG_AUTH_RESPONSE = 0x01EE;
    private const ushort CMSG_CHAR_ENUM = 0x0037;
    private const ushort SMSG_CHAR_ENUM = 0x003B;
    private const ushort CMSG_PLAYER_LOGIN = 0x003D;
    private const ushort SMSG_LOGIN_VERIFY_WORLD = 0x0236;
    private const ushort SMSG_TRANSFER_PENDING = 0x003F;
    private const ushort SMSG_NEW_WORLD = 0x003E;
    private const ushort MSG_MOVE_WORLDPORT_ACK = 0x00DC;
    private const ushort SMSG_TRANSFER_ABORT = 0x0040;
    private const ushort MSG_MOVE_TELEPORT = 0x00C5;
    private const ushort MSG_MOVE_TELEPORT_ACK = 0x00C7;
    private const ushort CMSG_LOGOUT_REQUEST = 0x004B;
    private const ushort SMSG_LOGOUT_COMPLETE = 0x004D;
    private const ushort MSG_MOVE_HEARTBEAT = 0x00EE;

    private readonly ConnectionStateMachine _csm;
    private readonly List<string> _pauseCalls = new();

    public ConnectionStateMachineTests()
    {
        _csm = new ConnectionStateMachine
        {
            BeginTeleportPauseCallback = () => _pauseCalls.Add("BEGIN"),
            EndTeleportPauseCallback = () => _pauseCalls.Add("END")
        };
    }

    private void Send(ushort opcode) => _csm.ProcessPacket(PacketDirection.Send, opcode, 0);
    private void Recv(ushort opcode) => _csm.ProcessPacket(PacketDirection.Recv, opcode, 0);

    // --- Initial state ---

    [Fact]
    public void InitialState_IsDisconnected()
    {
        Assert.Equal(State.Disconnected, _csm.CurrentState);
    }

    // --- Auth / Login lifecycle ---

    [Fact]
    public void AuthSession_TransitionsToAuthenticating()
    {
        Send(CMSG_AUTH_SESSION);
        Assert.Equal(State.Authenticating, _csm.CurrentState);
    }

    [Fact]
    public void AuthResponse_TransitionsToAuthenticating()
    {
        Recv(SMSG_AUTH_RESPONSE);
        Assert.Equal(State.Authenticating, _csm.CurrentState);
    }

    [Fact]
    public void CharEnum_TransitionsToCharSelect()
    {
        Send(CMSG_AUTH_SESSION);
        Recv(SMSG_CHAR_ENUM);
        Assert.Equal(State.CharSelect, _csm.CurrentState);
    }

    [Fact]
    public void PlayerLogin_TransitionsToEnteringWorld()
    {
        Send(CMSG_AUTH_SESSION);
        Recv(SMSG_CHAR_ENUM);
        Send(CMSG_PLAYER_LOGIN);
        Assert.Equal(State.EnteringWorld, _csm.CurrentState);
    }

    [Fact]
    public void LoginVerifyWorld_TransitionsToInWorld()
    {
        Send(CMSG_AUTH_SESSION);
        Recv(SMSG_CHAR_ENUM);
        Send(CMSG_PLAYER_LOGIN);
        Recv(SMSG_LOGIN_VERIFY_WORLD);
        Assert.Equal(State.InWorld, _csm.CurrentState);
    }

    [Fact]
    public void FullLoginLifecycle()
    {
        // Disconnected → Authenticating → CharSelect → EnteringWorld → InWorld
        Assert.Equal(State.Disconnected, _csm.CurrentState);

        Send(CMSG_AUTH_SESSION);
        Assert.Equal(State.Authenticating, _csm.CurrentState);

        Recv(SMSG_CHAR_ENUM);
        Assert.Equal(State.CharSelect, _csm.CurrentState);

        Send(CMSG_PLAYER_LOGIN);
        Assert.Equal(State.EnteringWorld, _csm.CurrentState);

        Recv(SMSG_LOGIN_VERIFY_WORLD);
        Assert.Equal(State.InWorld, _csm.CurrentState);
    }

    // --- IsLuaSafe ---

    [Fact]
    public void IsLuaSafe_FalseWhenDisconnected()
    {
        Assert.False(_csm.IsLuaSafe);
    }

    [Fact]
    public void IsLuaSafe_TrueWhenAuthenticating()
    {
        Send(CMSG_AUTH_SESSION);
        Assert.True(_csm.IsLuaSafe);
    }

    [Fact]
    public void IsLuaSafe_TrueWhenCharSelect()
    {
        Send(CMSG_AUTH_SESSION);
        Recv(SMSG_CHAR_ENUM);
        Assert.True(_csm.IsLuaSafe);
    }

    [Fact]
    public void IsLuaSafe_FalseWhenEnteringWorld()
    {
        Send(CMSG_AUTH_SESSION);
        Recv(SMSG_CHAR_ENUM);
        Send(CMSG_PLAYER_LOGIN);
        Assert.False(_csm.IsLuaSafe);
    }

    [Fact]
    public void IsLuaSafe_TrueWhenInWorld()
    {
        GoToInWorld();
        Assert.True(_csm.IsLuaSafe);
    }

    [Fact]
    public void IsLuaSafe_FalseWhenTransferring()
    {
        GoToInWorld();
        Recv(SMSG_TRANSFER_PENDING);
        Assert.Equal(State.Transferring, _csm.CurrentState);
        Assert.False(_csm.IsLuaSafe);
    }

    // --- IsObjectManagerValid ---

    [Fact]
    public void IsObjectManagerValid_TrueWhenInWorld()
    {
        GoToInWorld();
        Assert.True(_csm.IsObjectManagerValid);
    }

    [Fact]
    public void IsObjectManagerValid_FalseWhenTransferring()
    {
        GoToInWorld();
        Recv(SMSG_TRANSFER_PENDING);
        Assert.False(_csm.IsObjectManagerValid);
    }

    [Fact]
    public void IsObjectManagerValid_FalseWhenCharSelect()
    {
        Send(CMSG_AUTH_SESSION);
        Recv(SMSG_CHAR_ENUM);
        Assert.False(_csm.IsObjectManagerValid);
    }

    [Fact]
    public void IsObjectManagerValid_FalseDuringSameMapTeleportCooldown()
    {
        GoToInWorld();
        Recv(MSG_MOVE_TELEPORT);
        Assert.Equal(State.InWorld, _csm.CurrentState); // State doesn't change
        Assert.True(_csm.IsTeleportCooldownActive);
        Assert.False(_csm.IsObjectManagerValid); // But ObjectManager is NOT valid
    }

    // --- IsSendingSafe ---

    [Fact]
    public void IsSendingSafe_TrueWhenInWorld()
    {
        GoToInWorld();
        Assert.True(_csm.IsSendingSafe);
    }

    [Fact]
    public void IsSendingSafe_FalseWhenTransferring()
    {
        GoToInWorld();
        Recv(SMSG_TRANSFER_PENDING);
        Assert.False(_csm.IsSendingSafe);
    }

    // --- Cross-map transfer: THE critical crash-prevention path ---

    [Fact]
    public void CrossMapTransfer_TransferPending_PausesObjectManager()
    {
        GoToInWorld();
        _pauseCalls.Clear();

        Recv(SMSG_TRANSFER_PENDING);

        Assert.Equal(State.Transferring, _csm.CurrentState);
        Assert.Contains("BEGIN", _pauseCalls);
    }

    [Fact]
    public void CrossMapTransfer_LoginVerifyWorld_ResumesObjectManager()
    {
        GoToInWorld();
        Recv(SMSG_TRANSFER_PENDING);
        _pauseCalls.Clear();

        Recv(SMSG_LOGIN_VERIFY_WORLD);

        Assert.Equal(State.InWorld, _csm.CurrentState);
        Assert.Contains("END", _pauseCalls);
    }

    [Fact]
    public void CrossMapTransfer_FullLifecycle_PauseAndResume()
    {
        GoToInWorld();
        _pauseCalls.Clear();

        // Server says "you're going to a new map"
        Recv(SMSG_TRANSFER_PENDING);
        Assert.Equal(State.Transferring, _csm.CurrentState);
        Assert.False(_csm.IsLuaSafe);
        Assert.False(_csm.IsObjectManagerValid);
        Assert.False(_csm.IsSendingSafe);

        // Client ACKs the worldport (stays Transferring)
        Send(MSG_MOVE_WORLDPORT_ACK);
        Assert.Equal(State.Transferring, _csm.CurrentState);

        // Server confirms new world loaded
        Recv(SMSG_LOGIN_VERIFY_WORLD);
        Assert.Equal(State.InWorld, _csm.CurrentState);
        Assert.True(_csm.IsLuaSafe);
        Assert.True(_csm.IsObjectManagerValid);
        Assert.True(_csm.IsSendingSafe);

        // Verify pause/resume sequence
        Assert.Equal(new[] { "BEGIN", "END" }, _pauseCalls);
    }

    [Fact]
    public void CrossMapTransfer_NewWorld_StaysTransferring()
    {
        GoToInWorld();
        Recv(SMSG_TRANSFER_PENDING);
        _pauseCalls.Clear();

        // SMSG_NEW_WORLD also keeps us in Transferring (no state change, no extra pause call)
        Recv(SMSG_NEW_WORLD);
        Assert.Equal(State.Transferring, _csm.CurrentState);
    }

    [Fact]
    public void CrossMapTransfer_Abort_RestoresToInWorld()
    {
        GoToInWorld();
        Recv(SMSG_TRANSFER_PENDING);
        Assert.Equal(State.Transferring, _csm.CurrentState);

        Recv(SMSG_TRANSFER_ABORT);
        Assert.Equal(State.InWorld, _csm.CurrentState);
    }

    // --- Same-map teleport (MSG_MOVE_TELEPORT) ---

    [Fact]
    public void SameMapTeleport_PausesObjectManager()
    {
        GoToInWorld();
        _pauseCalls.Clear();

        Recv(MSG_MOVE_TELEPORT);

        Assert.Equal(State.InWorld, _csm.CurrentState); // State doesn't change for same-map
        Assert.True(_csm.IsTeleportCooldownActive);
        Assert.Contains("BEGIN", _pauseCalls);
    }

    [Fact]
    public void SameMapTeleport_AckResumesObjectManager()
    {
        GoToInWorld();
        Recv(MSG_MOVE_TELEPORT);
        _pauseCalls.Clear();

        Send(MSG_MOVE_TELEPORT_ACK);

        Assert.False(_csm.IsTeleportCooldownActive);
        Assert.Contains("END", _pauseCalls);
    }

    [Fact]
    public void SameMapTeleport_FullLifecycle()
    {
        GoToInWorld();
        _pauseCalls.Clear();

        Recv(MSG_MOVE_TELEPORT);
        Assert.True(_csm.IsTeleportCooldownActive);
        Assert.False(_csm.IsObjectManagerValid);

        Send(MSG_MOVE_TELEPORT_ACK);
        Assert.False(_csm.IsTeleportCooldownActive);
        Assert.True(_csm.IsObjectManagerValid);

        Assert.Equal(new[] { "BEGIN", "END" }, _pauseCalls);
    }

    // --- Logout ---

    [Fact]
    public void LogoutRequest_TransitionsToLoggingOut()
    {
        GoToInWorld();
        Send(CMSG_LOGOUT_REQUEST);
        Assert.Equal(State.LoggingOut, _csm.CurrentState);
    }

    [Fact]
    public void LogoutComplete_TransitionsToCharSelect()
    {
        GoToInWorld();
        Send(CMSG_LOGOUT_REQUEST);
        Recv(SMSG_LOGOUT_COMPLETE);
        Assert.Equal(State.CharSelect, _csm.CurrentState);
    }

    // --- ForceState ---

    [Fact]
    public void ForceState_OverridesCurrentState()
    {
        _csm.ForceState(State.InWorld, "test override");
        Assert.Equal(State.InWorld, _csm.CurrentState);
    }

    [Fact]
    public void ForceState_NoOpWhenAlreadyInState()
    {
        var stateChanges = new List<(State old, State @new)>();
        _csm.OnStateChanged += (o, n) => stateChanges.Add((o, n));

        _csm.ForceState(State.InWorld, "first");
        _csm.ForceState(State.InWorld, "second"); // should be no-op

        Assert.Single(stateChanges);
    }

    // --- OnStateChanged event ---

    [Fact]
    public void StateChange_FiresEvent()
    {
        var stateChanges = new List<(State old, State @new)>();
        _csm.OnStateChanged += (o, n) => stateChanges.Add((o, n));

        Send(CMSG_AUTH_SESSION);

        Assert.Single(stateChanges);
        Assert.Equal(State.Disconnected, stateChanges[0].old);
        Assert.Equal(State.Authenticating, stateChanges[0].@new);
    }

    [Fact]
    public void UnrelatedPacket_DoesNotChangeState()
    {
        GoToInWorld();
        var stateChanges = new List<(State old, State @new)>();
        _csm.OnStateChanged += (o, n) => stateChanges.Add((o, n));

        // Heartbeat should not change state
        Send(MSG_MOVE_HEARTBEAT);
        Recv(MSG_MOVE_HEARTBEAT);

        Assert.Empty(stateChanges);
        Assert.Equal(State.InWorld, _csm.CurrentState);
    }

    // --- Regression: consecutive transfers must each pause/resume ---

    [Fact]
    public void ConsecutiveTransfers_EachPausesAndResumes()
    {
        GoToInWorld();
        _pauseCalls.Clear();

        // First transfer
        Recv(SMSG_TRANSFER_PENDING);
        Send(MSG_MOVE_WORLDPORT_ACK);
        Recv(SMSG_LOGIN_VERIFY_WORLD);

        // Second transfer (e.g., instance → overworld → another instance)
        Recv(SMSG_TRANSFER_PENDING);
        Send(MSG_MOVE_WORLDPORT_ACK);
        Recv(SMSG_LOGIN_VERIFY_WORLD);

        Assert.Equal(new[] { "BEGIN", "END", "BEGIN", "END" }, _pauseCalls);
        Assert.Equal(State.InWorld, _csm.CurrentState);
    }

    // --- Helper ---

    private void GoToInWorld()
    {
        Send(CMSG_AUTH_SESSION);
        Recv(SMSG_CHAR_ENUM);
        Send(CMSG_PLAYER_LOGIN);
        Recv(SMSG_LOGIN_VERIFY_WORLD);
        Assert.Equal(State.InWorld, _csm.CurrentState);
    }
}
