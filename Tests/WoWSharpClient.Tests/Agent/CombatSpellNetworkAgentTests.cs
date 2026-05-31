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

        #region PetObjectiveType Packing Tests

        [Fact]
        public void PetObjectiveType_Pack_CommandAttack_CorrectPacking()
        {
            // MAKE_UNIT_ACTION_BUTTON(COMMAND_ATTACK=2, ACT_COMMAND=0x07) = 0x07000002
            uint packed = PetObjectiveType.Pack((uint)PetCommand.Attack, PetObjectiveType.ACT_COMMAND);
            Assert.Equal(0x07000002u, packed);
        }

        [Fact]
        public void PetObjectiveType_Pack_CommandFollow_CorrectPacking()
        {
            uint packed = PetObjectiveType.Pack((uint)PetCommand.Follow, PetObjectiveType.ACT_COMMAND);
            Assert.Equal(0x07000001u, packed);
        }

        [Fact]
        public void PetObjectiveType_Pack_CommandStay_CorrectPacking()
        {
            uint packed = PetObjectiveType.Pack((uint)PetCommand.Stay, PetObjectiveType.ACT_COMMAND);
            Assert.Equal(0x07000000u, packed);
        }

        [Fact]
        public void PetObjectiveType_Pack_CommandDismiss_CorrectPacking()
        {
            uint packed = PetObjectiveType.Pack((uint)PetCommand.Dismiss, PetObjectiveType.ACT_COMMAND);
            Assert.Equal(0x07000003u, packed);
        }

        [Fact]
        public void PetObjectiveType_Pack_AbilityEnabled_CorrectPacking()
        {
            // MAKE_UNIT_ACTION_BUTTON(spellId=100, ACT_ENABLED=0xC1) = 0xC1000064
            uint packed = PetObjectiveType.Pack(100, PetObjectiveType.ACT_ENABLED);
            Assert.Equal(0xC1000064u, packed);
        }

        [Fact]
        public void PetObjectiveType_Pack_LargeSpellId_Uses24Bits()
        {
            // 24-bit max = 0x00FFFFFF = 16777215
            uint packed = PetObjectiveType.Pack(0x00FFFFFF, PetObjectiveType.ACT_ENABLED);
            Assert.Equal(0xC1FFFFFFu, packed);
        }

        [Fact]
        public void PetObjectiveType_Pack_Roundtrip_UnpacksCorrectly()
        {
            uint actionId = 12345;
            byte objectiveType = PetObjectiveType.ACT_COMMAND;
            uint packed = PetObjectiveType.Pack(actionId, objectiveType);

            // Unpack per MaNGOS macros
            uint unpackedAction = packed & 0x00FFFFFF;
            byte unpackedType = (byte)((packed >> 24) & 0xFF);

            Assert.Equal(actionId, unpackedAction);
            Assert.Equal(objectiveType, unpackedType);
        }

        [Fact]
        public void PetObjectiveType_Constants_MatchMaNGOS()
        {
            Assert.Equal(0x01, PetObjectiveType.ACT_PASSIVE);
            Assert.Equal(0x81, PetObjectiveType.ACT_DISABLED);
            Assert.Equal(0xC1, PetObjectiveType.ACT_ENABLED);
            Assert.Equal(0x07, PetObjectiveType.ACT_COMMAND);
            Assert.Equal(0x06, PetObjectiveType.ACT_REACTION);
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
