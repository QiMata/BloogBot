using System;
using System.Runtime.InteropServices;
using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;

namespace ForegroundBotRunner.Tests;

public sealed class ForegroundPlayerSnapshotParityTests
{
    [Fact]
    public void Coinage_ReadsDescriptorField()
    {
        using var fixture = new PlayerMemoryFixture();
        fixture.WriteDescriptorUInt(UpdateFields.EPlayerFields.PLAYER_FIELD_COINAGE, 12345u);

        var player = fixture.CreatePlayer();

        Assert.Equal(12345u, player.Coinage);
    }

    [Fact]
    public void Copper_UsesCoinageDescriptorField()
    {
        using var fixture = new PlayerMemoryFixture();
        fixture.WriteDescriptorUInt(UpdateFields.EPlayerFields.PLAYER_FIELD_COINAGE, 67890u);

        var player = fixture.CreateLocalPlayer();

        Assert.Equal(67890u, player.Copper);
    }

    [Fact]
    public void HasQuestTargets_FalseWhenQuestLogIsEmpty()
    {
        using var fixture = new PlayerMemoryFixture();

        var player = fixture.CreateLocalPlayer();

        Assert.False(player.HasQuestTargets);
    }

    [Fact]
    public void HasQuestTargets_TrueWhenQuestLogContainsActiveQuest()
    {
        using var fixture = new PlayerMemoryFixture();
        fixture.WriteDescriptorUInt(UpdateFields.EPlayerFields.PLAYER_QUEST_LOG_1_1, 4242u);

        var player = fixture.CreateLocalPlayer();

        Assert.True(player.HasQuestTargets);
    }

    [Fact]
    public void LocalPlayer_InterfaceRaceClassGender_UseUnitBytes0()
    {
        using var fixture = new PlayerMemoryFixture();
        fixture.WritePackedUnitBytes0((byte)Race.Orc, (byte)Class.Warrior, (byte)Gender.Female, 0);

        var player = fixture.CreateLocalPlayer();
        IWoWPlayer asPlayer = player;

        Assert.Equal(Race.Orc, player.Race);
        Assert.Equal(Class.Warrior, player.Class);
        Assert.Equal(Gender.Female, player.Gender);
        Assert.Equal(Race.Orc, asPlayer.Race);
        Assert.Equal(Class.Warrior, asPlayer.Class);
        Assert.Equal(Gender.Female, asPlayer.Gender);
    }

    [Fact]
    public void UnitPowersAndFactionTemplate_ReadDescriptorFields()
    {
        using var fixture = new PlayerMemoryFixture();
        fixture.WriteDescriptorUInt(UpdateFields.EUnitFields.UNIT_FIELD_POWER1, 1500u);
        fixture.WriteDescriptorUInt(UpdateFields.EUnitFields.UNIT_FIELD_POWER2, 420u);
        fixture.WriteDescriptorUInt(UpdateFields.EUnitFields.UNIT_FIELD_POWER4, 95u);
        fixture.WriteDescriptorUInt(UpdateFields.EUnitFields.UNIT_FIELD_MAXPOWER1, 2000u);
        fixture.WriteDescriptorUInt(UpdateFields.EUnitFields.UNIT_FIELD_MAXPOWER2, 1000u);
        fixture.WriteDescriptorUInt(UpdateFields.EUnitFields.UNIT_FIELD_MAXPOWER4, 100u);
        fixture.WriteDescriptorUInt(UpdateFields.EUnitFields.UNIT_FIELD_FACTIONTEMPLATE, 104u);

        var unit = fixture.CreateUnit();

        Assert.Equal(104u, unit.FactionTemplate);
        Assert.Equal(1500u, unit.Powers[Powers.MANA]);
        Assert.Equal(420u, unit.Powers[Powers.RAGE]);
        Assert.Equal(95u, unit.Powers[Powers.ENERGY]);
        Assert.Equal(2000u, unit.MaxPowers[Powers.MANA]);
        Assert.Equal(1000u, unit.MaxPowers[Powers.RAGE]);
        Assert.Equal(100u, unit.MaxPowers[Powers.ENERGY]);
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, false)]
    [InlineData(30u, true)]
    [InlineData(489u, true)]
    [InlineData(529u, true)]
    [InlineData(566u, true)]
    public void IsBattlegroundMapId_MatchesVanillaBattlegroundMapSet(uint mapId, bool expected)
    {
        Assert.Equal(expected, LocalPlayer.IsBattlegroundMapId(mapId));
    }

    private sealed class PlayerMemoryFixture : IDisposable
    {
        private readonly nint _objectBase;
        private readonly nint _descriptorBase;
        private bool _disposed;

        public PlayerMemoryFixture()
        {
            const int objectBytes = 64;
            int descriptorBytes = ((int)UpdateFields.EPlayerFields.PLAYER_AMMO_ID + 1) * sizeof(uint);

            _objectBase = Marshal.AllocHGlobal(objectBytes);
            _descriptorBase = Marshal.AllocHGlobal(descriptorBytes);

            Marshal.Copy(new byte[objectBytes], 0, _objectBase, objectBytes);
            Marshal.Copy(new byte[descriptorBytes], 0, _descriptorBase, descriptorBytes);

            Marshal.WriteIntPtr(nint.Add(_objectBase, MemoryAddresses.WoWObject_DescriptorOffset), _descriptorBase);
        }

        public WoWPlayer CreatePlayer()
            => new(_objectBase, new HighGuid(1ul), WoWObjectType.Player);

        public LocalPlayer CreateLocalPlayer()
            => new(_objectBase, new HighGuid(1ul), WoWObjectType.Player);

        public WoWUnit CreateUnit()
            => new(_objectBase, new HighGuid(2ul), WoWObjectType.Unit);

        public void WriteDescriptorUInt(UpdateFields.EPlayerFields field, uint value)
            => Marshal.WriteInt32(nint.Add(_descriptorBase, (int)field * sizeof(uint)), unchecked((int)value));

        public void WriteDescriptorUInt(UpdateFields.EUnitFields field, uint value)
            => Marshal.WriteInt32(nint.Add(_descriptorBase, (int)field * sizeof(uint)), unchecked((int)value));

        public void WritePackedUnitBytes0(byte race, byte @class, byte gender, byte powerType)
            => WriteDescriptorUInt(
                UpdateFields.EUnitFields.UNIT_FIELD_BYTES_0,
                race | ((uint)@class << 8) | ((uint)gender << 16) | ((uint)powerType << 24));

        public void Dispose()
        {
            if (_disposed)
                return;

            Marshal.FreeHGlobal(_descriptorBase);
            Marshal.FreeHGlobal(_objectBase);
            _disposed = true;
        }
    }
}
