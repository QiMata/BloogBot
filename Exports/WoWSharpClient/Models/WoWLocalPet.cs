using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Models
{
    public class WoWLocalPet(HighGuid highGuid, WoWObjectType objectType = WoWObjectType.Unit) : WoWUnit(highGuid, objectType), IWoWLocalPet, ICloneable
    {
        public void Attack()
        {
            var om = ObjectManager;
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
            var om = ObjectManager;
            if (om == null) return;

            // Resolve spell name → possible spell IDs via SpellData
            uint? abilityId = null;
            if (SpellData.SpellNameToIds.TryGetValue(spellName, out var candidates))
            {
                var petSpells = om.PetSpellIds;
                // Find highest-rank match in the pet's actual spell list
                for (int i = candidates.Length - 1; i >= 0; i--)
                {
                    if (petSpells.Contains(candidates[i]))
                    {
                        abilityId = candidates[i];
                        break;
                    }
                }
            }

            if (!abilityId.HasValue)
            {
                Log.Warning("[PET] Cast '{SpellName}' failed — spell not found in pet action bar", spellName);
                return;
            }

            var targetGuid = om.Player?.TargetGuid ?? 0;
            var payload = CombatSpellNetworkClientComponent.CreatePetAbilityPayload(
                Guid, abilityId.Value, targetGuid);
            om.SendPetAction(payload);
            Log.Information("[PET] Cast '{SpellName}' (spellId={SpellId}) on target 0x{Target:X}",
                spellName, abilityId.Value, targetGuid);
        }

        public void FollowPlayer()
        {
            var payload = CombatSpellNetworkClientComponent.CreatePetCommandPayload(
                Guid, PetCommand.Follow);
            ObjectManager?.SendPetAction(payload);
        }

        public bool IsHappy() => true;

        public object Clone() => MemberwiseClone();
    }
}
