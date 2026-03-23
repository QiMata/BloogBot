using System;
using System.Runtime.InteropServices;
using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Enums;
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

        public void WriteDescriptorUInt(UpdateFields.EPlayerFields field, uint value)
            => Marshal.WriteInt32(nint.Add(_descriptorBase, (int)field * sizeof(uint)), unchecked((int)value));

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
