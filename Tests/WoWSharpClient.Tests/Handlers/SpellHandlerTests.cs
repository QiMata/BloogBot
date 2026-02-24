using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System;
using System.IO;
using WoWSharpClient.Handlers;

namespace WoWSharpClient.Tests.Handlers
{
    [Collection("Sequential ObjectManager tests")]
    public class SpellHandlerTests(ObjectManagerFixture _) : IClassFixture<ObjectManagerFixture>
    {
        // --- SMSG_LOG_XPGAIN ---

        [Fact]
        public void HandleLogXpGain_KillXp_FiresOnXpGain()
        {
            // Arrange: victimGuid(8) + xpAmount(4) + type(1) = 13 bytes
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            ulong victimGuid = 0x0000000000001234;
            uint xpAmount = 150;
            byte type = 0; // kill XP
            writer.Write(victimGuid);
            writer.Write(xpAmount);
            writer.Write(type);
            byte[] data = ms.ToArray();

            int receivedXp = 0;
            bool eventFired = false;
            EventHandler<OnXpGainArgs> handler = (_, args) =>
            {
                receivedXp = args.Xp;
                eventFired = true;
            };
            WoWSharpEventEmitter.Instance.OnXpGain += handler;

            try
            {
                // Act
                SpellHandler.HandleLogXpGain(Opcode.SMSG_LOG_XPGAIN, data);

                // Assert
                Assert.True(eventFired, "OnXpGain event was not fired.");
                Assert.Equal(150, receivedXp);
            }
            finally
            {
                WoWSharpEventEmitter.Instance.OnXpGain -= handler;
            }
        }

        [Fact]
        public void HandleLogXpGain_QuestXp_FiresOnXpGain()
        {
            // Arrange: quest XP has victimGuid = 0
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((ulong)0);    // victimGuid = 0 for quest XP
            writer.Write((uint)500);   // xpAmount
            writer.Write((byte)1);     // type = 1 (non-kill/quest)
            byte[] data = ms.ToArray();

            int receivedXp = 0;
            bool eventFired = false;
            EventHandler<OnXpGainArgs> handler = (_, args) =>
            {
                receivedXp = args.Xp;
                eventFired = true;
            };
            WoWSharpEventEmitter.Instance.OnXpGain += handler;

            try
            {
                SpellHandler.HandleLogXpGain(Opcode.SMSG_LOG_XPGAIN, data);

                Assert.True(eventFired);
                Assert.Equal(500, receivedXp);
            }
            finally
            {
                WoWSharpEventEmitter.Instance.OnXpGain -= handler;
            }
        }

        [Fact]
        public void HandleLogXpGain_EmptyData_DoesNotThrow()
        {
            // Arrange: truncated packet — should silently catch EndOfStreamException
            byte[] data = [];

            // Act & Assert — no exception
            SpellHandler.HandleLogXpGain(Opcode.SMSG_LOG_XPGAIN, data);
        }

        // --- SMSG_LEVELUP_INFO ---

        [Fact]
        public void HandleLevelUpInfo_FiresLevelUpEvent()
        {
            // Arrange: newLevel(4) — only first uint32 is read
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((uint)10); // newLevel = 10
            // Real packet has stat deltas after, but handler only reads level
            byte[] data = ms.ToArray();

            bool eventFired = false;
            EventHandler handler = (_, _) => eventFired = true;
            WoWSharpEventEmitter.Instance.LevelUp += handler;

            try
            {
                SpellHandler.HandleLevelUpInfo(Opcode.SMSG_LEVELUP_INFO, data);

                Assert.True(eventFired, "LevelUp event was not fired.");
            }
            finally
            {
                WoWSharpEventEmitter.Instance.LevelUp -= handler;
            }
        }

        [Fact]
        public void HandleLevelUpInfo_EmptyData_DoesNotThrow()
        {
            byte[] data = [];
            SpellHandler.HandleLevelUpInfo(Opcode.SMSG_LEVELUP_INFO, data);
        }

        // --- SMSG_ATTACKSTART ---

        [Fact]
        public void HandleAttackStart_DifferentAttacker_DoesNotCrash()
        {
            // Arrange: attackerGuid(8) + targetGuid(8) = 16 bytes
            // When attacker is not the local player, should do nothing
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((ulong)0x9999); // attacker = some NPC
            writer.Write((ulong)0x1234); // target
            byte[] data = ms.ToArray();

            // Act & Assert — no exception even with no player set
            SpellHandler.HandleAttackStart(Opcode.SMSG_ATTACKSTART, data);
        }

        [Fact]
        public void HandleAttackStart_EmptyData_DoesNotThrow()
        {
            byte[] data = [];
            SpellHandler.HandleAttackStart(Opcode.SMSG_ATTACKSTART, data);
        }

        // --- SMSG_ATTACKSTOP ---

        [Fact]
        public void HandleAttackStop_EmptyData_DoesNotThrow()
        {
            byte[] data = [];
            SpellHandler.HandleAttackStop(Opcode.SMSG_ATTACKSTOP, data);
        }

        [Fact]
        public void HandleAttackStop_ValidPackedGuids_DoesNotCrash()
        {
            // Arrange: packed GUIDs — 1-byte mask + guid bytes
            // PackedGuid for 0x1234: mask=0x02 (bytes at positions 1), then 0x12, 0x34...
            // Simpler: single-byte guid = mask 0x01, value byte
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            // Packed GUID for attacker = 0x05: mask=0x01, byte=0x05
            writer.Write((byte)0x01); // mask: byte 0 present
            writer.Write((byte)0x05); // guid byte 0 = 5 → guid = 5
            // Packed GUID for target = 0x0A: mask=0x01, byte=0x0A
            writer.Write((byte)0x01);
            writer.Write((byte)0x0A);
            byte[] data = ms.ToArray();

            // Act & Assert — no exception (player guid won't match 0x05)
            SpellHandler.HandleAttackStop(Opcode.SMSG_ATTACKSTOP, data);
        }

        // --- SMSG_INITIAL_SPELLS ---

        [Fact]
        public void HandleInitialSpells_FiresEventAndPopulatesSpells()
        {
            // Arrange: talentSpec(1) + spellCount(2) + spells + cooldownCount(2)
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((byte)0);     // talentSpec
            writer.Write((ushort)2);   // spellCount = 2
            // Spell 1: spellID(2) + unknown(2)
            writer.Write((ushort)133); // Fireball
            writer.Write((short)0);
            // Spell 2
            writer.Write((ushort)116); // Frostbolt
            writer.Write((short)0);
            writer.Write((ushort)0);   // cooldownCount = 0
            byte[] data = ms.ToArray();

            bool eventFired = false;
            EventHandler handler = (_, _) => eventFired = true;
            WoWSharpEventEmitter.Instance.OnInitialSpellsLoaded += handler;

            try
            {
                SpellHandler.HandleInitialSpells(Opcode.SMSG_INITIAL_SPELLS, data);

                Assert.True(eventFired, "OnInitialSpellsLoaded event was not fired.");
                Assert.Equal(2, WoWSharpObjectManager.Instance.Spells.Count);
                Assert.Equal(133u, WoWSharpObjectManager.Instance.Spells[0].Id);
                Assert.Equal(116u, WoWSharpObjectManager.Instance.Spells[1].Id);
            }
            finally
            {
                WoWSharpEventEmitter.Instance.OnInitialSpellsLoaded -= handler;
            }
        }
    }
}
