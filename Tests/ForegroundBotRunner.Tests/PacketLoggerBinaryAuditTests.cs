using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Mem.Hooks;

namespace ForegroundBotRunner.Tests;

public class PacketLoggerBinaryAuditTests
{
    private readonly WoWExeImage _wowExe = WoWExeImage.LoadDefault();

    [Fact]
    public void SendHookPrologue_MatchesKnownBuild5875Bytes()
    {
        byte[] bytes = _wowExe.ReadBytes((uint)(nint)Offsets.Functions.NetClientSend, 6);

        Assert.Equal(new byte[] { 0x55, 0x8B, 0xEC, 0x56, 0x8B, 0xF1 }, bytes);
        Assert.Equal(".text", _wowExe.GetSectionName((uint)(nint)Offsets.Functions.NetClientSend));
        Assert.Equal(6, PacketLogger.DetermineHookOverwriteSize(bytes, 5));
    }

    [Fact]
    public void RecvHookPrologue_ComputesSafeOverwriteBoundary()
    {
        byte[] bytes = _wowExe.ReadBytes((uint)(nint)Offsets.Functions.NetClientProcessMessage, 16);

        Assert.Equal(new byte[] { 0x55, 0x8B, 0xEC }, bytes[..3]);
        Assert.Equal(".text", _wowExe.GetSectionName((uint)(nint)Offsets.Functions.NetClientProcessMessage));
        Assert.Equal(9, PacketLogger.DetermineHookOverwriteSize(bytes, 5));
    }

    [Fact]
    public void PatternScan_FindsConfiguredProcessMessageAddress()
    {
        uint sendAddress = (uint)(nint)Offsets.Functions.NetClientSend;
        uint scanStart = sendAddress - 0x2000;
        byte[] code = _wowExe.ReadBytes(scanStart, 0x4000);

        uint? candidate = PacketLogger.FindProcessMessageCandidate(code, scanStart, sendAddress);

        Assert.True(candidate.HasValue);
        Assert.Equal((uint)(nint)Offsets.Functions.NetClientProcessMessage, candidate.Value);
    }

    [Fact]
    public void GameVersionAddress_PointsAtVanillaVersionString()
    {
        string version = _wowExe.ReadAsciiZ((uint)(nint)Offsets.Misc.GameVersion, 16);

        Assert.Equal("1.12.1", version);
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.Misc.GameVersion));
    }

    [Fact]
    public void CoreRuntimePointers_ResideInWritableDataSections()
    {
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.Map.ContinentId));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.Connection.ClientConnection));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.Misc.LuaState));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.Functions.LastHardwareAction));
    }

    [Fact]
    public void MovementStructLayout_RemainsInternallyConsistent()
    {
        Assert.Equal(Offsets.Player.MovementStruct + 0x10, Offsets.Unit.PosX);
        Assert.Equal(Offsets.Player.MovementStruct + 0x14, Offsets.Unit.PosY);
        Assert.Equal(Offsets.Player.MovementStruct + 0x18, Offsets.Unit.PosZ);
        Assert.Equal(Offsets.Player.MovementStruct + 0x40, Offsets.Descriptors.MovementFlags);
    }
}
