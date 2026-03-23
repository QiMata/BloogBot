using ForegroundBotRunner.Mem;

namespace ForegroundBotRunner.Tests;

public class OffsetsBinaryAuditTests
{
    private readonly WoWExeImage _wowExe = WoWExeImage.LoadDefault();

    [Fact]
    public void SnapshotGlobals_ResideInWritableDataSections()
    {
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.Player.Class));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.Player.CharacterCount));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.Player.CorpsePositionX));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.Player.CorpsePositionY));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.Player.CorpsePositionZ));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)(nint)Offsets.ObjectManager.ManagerBase));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)MemoryAddresses.LocalPlayerSpellsBase));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)MemoryAddresses.LocalPlayerFirstExtraBag));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)MemoryAddresses.FallingSpeedPtr));
        Assert.Equal(".data", _wowExe.GetSectionName((uint)MemoryAddresses.SetControlBitDevicePtr));
    }

    [Fact]
    public void SharedForegroundOffsetTables_RemainInSync()
    {
        Assert.Equal((int)Offsets.Player.Class, MemoryAddresses.LocalPlayerClass);
        Assert.Equal((int)Offsets.Player.CorpsePositionX, MemoryAddresses.LocalPlayerCorpsePositionX);
        Assert.Equal((int)Offsets.Player.CorpsePositionY, MemoryAddresses.LocalPlayerCorpsePositionY);
        Assert.Equal((int)Offsets.Player.CorpsePositionZ, MemoryAddresses.LocalPlayerCorpsePositionZ);
        Assert.Equal((int)Offsets.Functions.LastHardwareAction, MemoryAddresses.LastHardwareAction);
        Assert.Equal(Offsets.Player.MovementStruct, MemoryAddresses.LocalPlayer_SetFacingOffset);
        Assert.Equal(Offsets.Unit.SwimPitch, MemoryAddresses.WoWUnit_SwimPitchOffset);
        Assert.Equal(Offsets.Unit.TransportGuid, MemoryAddresses.WoWUnit_TransportGuidOffset);
        Assert.Equal(Offsets.Unit.FallStartTime, MemoryAddresses.WoWUnit_FallStartTimeOffset);
        Assert.Equal(Offsets.Unit.FallStartHeight, MemoryAddresses.WoWUnit_FallStartHeightOffset);
        Assert.Equal(Offsets.Unit.CurrentSpeed, MemoryAddresses.WoWUnit_CurrentSpeedOffset);
        Assert.Equal(Offsets.Unit.WalkSpeed, MemoryAddresses.WoWUnit_WalkSpeedOffset);
        Assert.Equal(Offsets.Unit.RunSpeed, MemoryAddresses.WoWUnit_RunSpeedOffset);
        Assert.Equal(Offsets.Unit.RunBackSpeed, MemoryAddresses.WoWUnit_RunBackSpeedOffset);
        Assert.Equal(Offsets.Unit.SwimSpeed, MemoryAddresses.WoWUnit_SwimSpeedOffset);
        Assert.Equal(Offsets.Unit.SwimBackSpeed, MemoryAddresses.WoWUnit_SwimBackSpeedOffset);
        Assert.Equal(Offsets.Unit.TurnRate, MemoryAddresses.WoWUnit_TurnRateOffset);
        Assert.Equal(Offsets.Unit.MoveSplinePtr, MemoryAddresses.WoWUnit_MoveSplinePtrOffset);
        Assert.Equal(Offsets.Unit.JumpVelocity, MemoryAddresses.WoWUnit_JumpVelocityOffset);
    }

    [Fact]
    public void MovementStructLayout_CoversFacingTransportFallSpeedsAndSpline()
    {
        Assert.Equal(Offsets.Player.MovementStruct + 0x10, Offsets.Unit.PosX);
        Assert.Equal(Offsets.Player.MovementStruct + 0x14, Offsets.Unit.PosY);
        Assert.Equal(Offsets.Player.MovementStruct + 0x18, Offsets.Unit.PosZ);
        Assert.Equal(Offsets.Player.MovementStruct + 0x1C, Offsets.Unit.Facing);
        Assert.Equal(Offsets.Player.MovementStruct + 0x20, Offsets.Unit.SwimPitch);
        Assert.Equal(Offsets.Player.MovementStruct + 0x38, Offsets.Unit.TransportGuid);
        Assert.Equal(Offsets.Player.MovementStruct + 0x40, Offsets.Descriptors.MovementFlags);
        Assert.Equal(Offsets.Player.MovementStruct + 0x6C, Offsets.Unit.JumpSinAngle);
        Assert.Equal(Offsets.Player.MovementStruct + 0x70, Offsets.Unit.JumpCosAngle);
        Assert.Equal(Offsets.Player.MovementStruct + 0x78, Offsets.Unit.FallStartTime);
        Assert.Equal(Offsets.Player.MovementStruct + 0x80, Offsets.Unit.FallStartHeight);
        Assert.Equal(Offsets.Player.MovementStruct + 0x84, Offsets.Unit.CurrentSpeed);
        Assert.Equal(Offsets.Player.MovementStruct + 0x88, Offsets.Unit.WalkSpeed);
        Assert.Equal(Offsets.Player.MovementStruct + 0x8C, Offsets.Unit.RunSpeed);
        Assert.Equal(Offsets.Player.MovementStruct + 0x90, Offsets.Unit.RunBackSpeed);
        Assert.Equal(Offsets.Player.MovementStruct + 0x94, Offsets.Unit.SwimSpeed);
        Assert.Equal(Offsets.Player.MovementStruct + 0x98, Offsets.Unit.SwimBackSpeed);
        Assert.Equal(Offsets.Player.MovementStruct + 0x9C, Offsets.Unit.TurnRate);
        Assert.Equal(Offsets.Player.MovementStruct + 0xA4, Offsets.Unit.MoveSplinePtr);
        Assert.Equal(Offsets.Player.MovementStruct + 0xA8, Offsets.Unit.JumpVelocity);
    }

    [Fact]
    public void IntersectEntryPoints_AreDistinctAndBinaryBacked()
    {
        uint wrapper = (uint)MemoryAddresses.IntersectFunPtr;
        uint worldIntersect = (uint)(nint)Offsets.Functions.Intersect;

        Assert.Equal(".text", _wowExe.GetSectionName(wrapper));
        Assert.Equal(".text", _wowExe.GetSectionName(worldIntersect));
        Assert.NotEqual(wrapper, worldIntersect);

        byte[] wrapperBytes = _wowExe.ReadBytes(wrapper, 20);
        Assert.Equal(0x55, wrapperBytes[0]);
        Assert.Equal(0xE8, wrapperBytes[15]);

        int relativeCall = unchecked((int)_wowExe.ReadUInt32(wrapper + 16));
        uint callTarget = unchecked(wrapper + 20u + (uint)relativeCall);

        Assert.Equal(0x0069BFF0u, callTarget);
    }
}
