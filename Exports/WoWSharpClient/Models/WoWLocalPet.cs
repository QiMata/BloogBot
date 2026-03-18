using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Models
{
    public class WoWLocalPet(HighGuid highGuid, WoWObjectType objectType = WoWObjectType.Unit) : WoWUnit(highGuid, objectType), IWoWLocalPet, ICloneable
    {
        public void Attack()
        {
            var om = WoWSharpObjectManager.Instance;
            if (om == null) return;
            var targetGuid = om.Player?.TargetGuid ?? 0;
            if (targetGuid == 0)
            {
                Log.Warning("[PET] Attack called but player has no target");
                return;
            }
            var payload = CombatSpellNetworkClientComponent.CreatePetCommandPayload(
                Guid, PetCommand.Attack, targetGuid);
            om.SendPetAction(payload);
        }

        public bool CanUse(string spellName) => HasBuff(spellName) || !IsCasting;

        public void Cast(string spellName)
        {
            // Pet spell casting requires spell ID lookup from the pet's action bar.
            // SMSG_PET_SPELLS provides the action bar on summon; for now, log the attempt.
            Log.Information("[PET] Cast requested for '{SpellName}' — pet spell lookup not yet wired", spellName);
        }

        public void FollowPlayer()
        {
            var payload = CombatSpellNetworkClientComponent.CreatePetCommandPayload(
                Guid, PetCommand.Follow);
            WoWSharpObjectManager.Instance?.SendPetAction(payload);
        }

        public bool IsHappy() => true;

        public object Clone() => MemberwiseClone();
    }
}
