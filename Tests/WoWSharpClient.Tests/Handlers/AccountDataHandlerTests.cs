using GameData.Core.Enums;
using System;
using WoWSharpClient.Handlers;

namespace WoWSharpClient.Tests.Handlers
{
    [Collection("Sequential ObjectManager tests")]
    public class AccountDataHandlerTests(ObjectManagerFixture _) : IClassFixture<ObjectManagerFixture>
    {
        [Fact]
        public void HandleAccountDataTimes_ValidPacket_DoesNotThrow()
        {
            // Arrange: 36 bytes = serverTime(4) + 8 timestamps(32)
            byte[] data = new byte[36];
            BitConverter.GetBytes((uint)1000000).CopyTo(data, 0); // serverTime
            for (int i = 0; i < 8; i++)
                BitConverter.GetBytes((uint)(i * 100)).CopyTo(data, 4 + i * 4);

            // Act & Assert — no exception
            AccountDataHandler.HandleAccountData(Opcode.SMSG_ACCOUNT_DATA_TIMES, data);
        }

        [Fact]
        public void HandleAccountDataTimes_TooSmall_DoesNotThrow()
        {
            // Arrange: only 20 bytes (less than required 36)
            byte[] data = new byte[20];

            // Act & Assert — should log error but not throw
            AccountDataHandler.HandleAccountData(Opcode.SMSG_ACCOUNT_DATA_TIMES, data);
        }

        [Fact]
        public void HandleUpdateAccountData_ValidPacket_DoesNotThrow()
        {
            // Arrange: guid(4) + type(4) + timestamp(4) + size(4) + payload
            var payload = "test_data"u8.ToArray();
            byte[] data = new byte[16 + payload.Length];
            BitConverter.GetBytes((uint)0).CopyTo(data, 0);              // guid
            BitConverter.GetBytes((uint)1).CopyTo(data, 4);              // type
            BitConverter.GetBytes((uint)1234567890).CopyTo(data, 8);     // timestamp
            BitConverter.GetBytes((uint)payload.Length).CopyTo(data, 12); // size
            Array.Copy(payload, 0, data, 16, payload.Length);

            // Act & Assert — no exception
            AccountDataHandler.HandleAccountData(Opcode.SMSG_UPDATE_ACCOUNT_DATA, data);
        }

        [Fact]
        public void HandleUpdateAccountData_TooSmall_DoesNotThrow()
        {
            // Arrange: less than 16 bytes
            byte[] data = new byte[10];

            // Act & Assert — should log error but not throw
            AccountDataHandler.HandleAccountData(Opcode.SMSG_UPDATE_ACCOUNT_DATA, data);
        }

        [Fact]
        public void HandleUpdateAccountData_SizeMismatch_DoesNotThrow()
        {
            // Arrange: declares size=100 but only has 5 bytes of payload
            byte[] data = new byte[21]; // 16 header + 5 payload
            BitConverter.GetBytes((uint)0).CopyTo(data, 0);
            BitConverter.GetBytes((uint)0).CopyTo(data, 4);
            BitConverter.GetBytes((uint)0).CopyTo(data, 8);
            BitConverter.GetBytes((uint)100).CopyTo(data, 12); // claimed size >> actual

            // Act & Assert — should log error but not throw
            AccountDataHandler.HandleAccountData(Opcode.SMSG_UPDATE_ACCOUNT_DATA, data);
        }

        [Fact]
        public void HandleAccountData_EmptyData_DoesNotThrow()
        {
            byte[] data = [];
            AccountDataHandler.HandleAccountData(Opcode.SMSG_ACCOUNT_DATA_TIMES, data);
            AccountDataHandler.HandleAccountData(Opcode.SMSG_UPDATE_ACCOUNT_DATA, data);
        }
    }
}
