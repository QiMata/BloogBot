using System;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Tests.Agent
{
    public class CombatSpellNetworkClientComponentTests
    {
        #region PetCommand Enum Tests

        [Fact]
        public void PetCommand_Values_MatchMaNGOSCommandStates()
        {
            Assert.Equal(0, (int)PetCommand.Stay);
            Assert.Equal(1, (int)PetCommand.Follow);
            Assert.Equal(2, (int)PetCommand.Attack);
            Assert.Equal(3, (int)PetCommand.Dismiss);
        }

        [Fact]
        public void PetReactState_Values_MatchMaNGOSReactStates()
        {
            Assert.Equal(0, (int)PetReactState.Passive);
            Assert.Equal(1, (int)PetReactState.Defensive);
            Assert.Equal(2, (int)PetReactState.Aggressive);
        }

        #endregion

        #region PetActionType Packing Tests

        [Fact]
        public void PetActionType_Pack_CommandAttack_CorrectPacking()
        {
            // MAKE_UNIT_ACTION_BUTTON(COMMAND_ATTACK=2, ACT_COMMAND=0x07) = 0x07000002
            uint packed = PetActionType.Pack((uint)PetCommand.Attack, PetActionType.ACT_COMMAND);
            Assert.Equal(0x07000002u, packed);
        }

        [Fact]
        public void PetActionType_Pack_CommandFollow_CorrectPacking()
        {
            uint packed = PetActionType.Pack((uint)PetCommand.Follow, PetActionType.ACT_COMMAND);
            Assert.Equal(0x07000001u, packed);
        }

        [Fact]
        public void PetActionType_Pack_CommandStay_CorrectPacking()
        {
            uint packed = PetActionType.Pack((uint)PetCommand.Stay, PetActionType.ACT_COMMAND);
            Assert.Equal(0x07000000u, packed);
        }

        [Fact]
        public void PetActionType_Pack_CommandDismiss_CorrectPacking()
        {
            uint packed = PetActionType.Pack((uint)PetCommand.Dismiss, PetActionType.ACT_COMMAND);
            Assert.Equal(0x07000003u, packed);
        }

        [Fact]
        public void PetActionType_Pack_AbilityEnabled_CorrectPacking()
        {
            // MAKE_UNIT_ACTION_BUTTON(spellId=100, ACT_ENABLED=0xC1) = 0xC1000064
            uint packed = PetActionType.Pack(100, PetActionType.ACT_ENABLED);
            Assert.Equal(0xC1000064u, packed);
        }

        [Fact]
        public void PetActionType_Pack_LargeSpellId_Uses24Bits()
        {
            // 24-bit max = 0x00FFFFFF = 16777215
            uint packed = PetActionType.Pack(0x00FFFFFF, PetActionType.ACT_ENABLED);
            Assert.Equal(0xC1FFFFFFu, packed);
        }

        [Fact]
        public void PetActionType_Pack_Roundtrip_UnpacksCorrectly()
        {
            uint actionId = 12345;
            byte actionType = PetActionType.ACT_COMMAND;
            uint packed = PetActionType.Pack(actionId, actionType);

            // Unpack per MaNGOS macros
            uint unpackedAction = packed & 0x00FFFFFF;
            byte unpackedType = (byte)((packed >> 24) & 0xFF);

            Assert.Equal(actionId, unpackedAction);
            Assert.Equal(actionType, unpackedType);
        }

        [Fact]
        public void PetActionType_Constants_MatchMaNGOS()
        {
            Assert.Equal(0x01, PetActionType.ACT_PASSIVE);
            Assert.Equal(0x81, PetActionType.ACT_DISABLED);
            Assert.Equal(0xC1, PetActionType.ACT_ENABLED);
            Assert.Equal(0x07, PetActionType.ACT_COMMAND);
            Assert.Equal(0x06, PetActionType.ACT_REACTION);
        }

        #endregion

        #region CreatePetCommandPayload Tests

        [Fact]
        public void CreatePetCommandPayload_Attack_Always20Bytes()
        {
            const ulong petGuid = 0xDEADBEEF;
            const ulong targetGuid = 0x12345678;

            var payload = CombatSpellNetworkClientComponent.CreatePetCommandPayload(petGuid, PetCommand.Attack, targetGuid);

            Assert.Equal(20, payload.Length);
            Assert.Equal(petGuid, BitConverter.ToUInt64(payload, 0));
            Assert.Equal(0x07000002u, BitConverter.ToUInt32(payload, 8)); // ACT_COMMAND | COMMAND_ATTACK
            Assert.Equal(targetGuid, BitConverter.ToUInt64(payload, 12));
        }

        [Fact]
        public void CreatePetCommandPayload_Follow_NoTarget_TargetIsZero()
        {
            const ulong petGuid = 0xAAAABBBB;

            var payload = CombatSpellNetworkClientComponent.CreatePetCommandPayload(petGuid, PetCommand.Follow);

            Assert.Equal(20, payload.Length);
            Assert.Equal(petGuid, BitConverter.ToUInt64(payload, 0));
            Assert.Equal(0x07000001u, BitConverter.ToUInt32(payload, 8)); // ACT_COMMAND | COMMAND_FOLLOW
            Assert.Equal(0UL, BitConverter.ToUInt64(payload, 12)); // targetGuid always present as 0
        }

        [Fact]
        public void CreatePetCommandPayload_Stay_CorrectPacking()
        {
            var payload = CombatSpellNetworkClientComponent.CreatePetCommandPayload(0x1111, PetCommand.Stay);

            Assert.Equal(20, payload.Length);
            Assert.Equal(0x07000000u, BitConverter.ToUInt32(payload, 8)); // ACT_COMMAND | COMMAND_STAY
        }

        #endregion

        #region CreatePetAbilityPayload Tests

        [Fact]
        public void CreatePetAbilityPayload_SpellId_PackedWithACT_ENABLED()
        {
            const ulong petGuid = 0xCCCCDDDD;
            const uint abilityId = 500;
            const ulong targetGuid = 0xEEEEFFFF;

            var payload = CombatSpellNetworkClientComponent.CreatePetAbilityPayload(petGuid, abilityId, targetGuid);

            Assert.Equal(20, payload.Length);
            Assert.Equal(petGuid, BitConverter.ToUInt64(payload, 0));
            // ACT_ENABLED(0xC1) << 24 | abilityId(500=0x1F4) = 0xC10001F4
            Assert.Equal(0xC10001F4u, BitConverter.ToUInt32(payload, 8));
            Assert.Equal(targetGuid, BitConverter.ToUInt64(payload, 12));
        }

        [Fact]
        public void CreatePetAbilityPayload_NoTarget_TargetIsZero()
        {
            var payload = CombatSpellNetworkClientComponent.CreatePetAbilityPayload(0x1111, 42);

            Assert.Equal(20, payload.Length);
            Assert.Equal(0UL, BitConverter.ToUInt64(payload, 12)); // always present as 0
        }

        #endregion
    }
}
