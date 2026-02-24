using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using WoWSharpClient.Handlers;

namespace WoWSharpClient.Tests.Handlers
{
    [Collection("Sequential ObjectManager tests")]
    public class WorldStateHandlerTests(ObjectManagerFixture _) : IClassFixture<ObjectManagerFixture>
    {
        [Fact]
        public void HandleInitWorldStates_ValidPacket_FiresEvent()
        {
            // Arrange: mapId(4) + zoneId(4) + phaseId(4) + unknown1(4) + unknown2(2) + worldStates
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((uint)0);   // mapId (Eastern Kingdoms)
            writer.Write((uint)12);  // zoneId (Elwynn Forest)
            writer.Write((uint)0);   // phaseId
            writer.Write((uint)0);   // unknown1
            writer.Write((ushort)0); // unknown2

            // Two world state entries
            writer.Write((uint)2264); // WSG flag state
            writer.Write((uint)1);
            writer.Write((uint)2265);
            writer.Write((uint)0);

            // Terminator
            writer.Write((uint)0);
            writer.Write((uint)0);

            byte[] data = ms.ToArray();

            List<WorldState> receivedStates = null;
            EventHandler<List<WorldState>> handler = (_, states) => receivedStates = states;
            WoWSharpEventEmitter.Instance.OnWorldStatesInit += handler;

            try
            {
                WorldStateHandler.HandleInitWorldStates(Opcode.SMSG_INIT_WORLD_STATES, data);

                Assert.NotNull(receivedStates);
                Assert.Equal(2, receivedStates.Count);
                Assert.Equal(2264u, receivedStates[0].StateId);
                Assert.Equal(1u, receivedStates[0].StateValue);
                Assert.Equal(2265u, receivedStates[1].StateId);
                Assert.Equal(0u, receivedStates[1].StateValue);
            }
            finally
            {
                WoWSharpEventEmitter.Instance.OnWorldStatesInit -= handler;
            }
        }

        [Fact]
        public void HandleInitWorldStates_TooSmall_DoesNotThrow()
        {
            // Arrange: less than 18 bytes (minimum header)
            byte[] data = new byte[10];

            WorldStateHandler.HandleInitWorldStates(Opcode.SMSG_INIT_WORLD_STATES, data);
        }

        [Fact]
        public void HandleInitWorldStates_EmptyData_DoesNotThrow()
        {
            byte[] data = [];
            WorldStateHandler.HandleInitWorldStates(Opcode.SMSG_INIT_WORLD_STATES, data);
        }

        [Fact]
        public void HandleInitWorldStates_HeaderOnly_FiresWithEmptyList()
        {
            // Arrange: just the header, no world state entries, immediate terminator
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((uint)1);   // mapId (Kalimdor)
            writer.Write((uint)17);  // zoneId (Barrens)
            writer.Write((uint)0);   // phaseId
            writer.Write((uint)0);   // unknown1
            writer.Write((ushort)0); // unknown2
            // Terminator
            writer.Write((uint)0);
            writer.Write((uint)0);

            byte[] data = ms.ToArray();

            List<WorldState> receivedStates = null;
            EventHandler<List<WorldState>> handler = (_, states) => receivedStates = states;
            WoWSharpEventEmitter.Instance.OnWorldStatesInit += handler;

            try
            {
                WorldStateHandler.HandleInitWorldStates(Opcode.SMSG_INIT_WORLD_STATES, data);

                Assert.NotNull(receivedStates);
                Assert.Empty(receivedStates);
            }
            finally
            {
                WoWSharpEventEmitter.Instance.OnWorldStatesInit -= handler;
            }
        }
    }
}
