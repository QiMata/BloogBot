﻿using ForegroundBotRunner.Mem;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;

namespace ForegroundBotRunner.Objects
{
    public class LocalPet : WoWUnit, IWoWLocalPet
    {
        internal LocalPet(
            nint pointer,
            HighGuid guid,
            WoWObjectType objectType)
            : base(pointer, guid, objectType)
        {
        }

        public void Attack() => Functions.LuaCall("PetAttack()");

        public void FollowPlayer() => Functions.LuaCall("PetFollow()");

        public bool IsHappy()
        {
            const string getPetHappiness = "happiness, damagePercentage, loyaltyRate = GetPetHappiness(); {0} = happiness;";
            var result = Functions.LuaCallWithResult(getPetHappiness);
            return result[0].Trim().Equals("3");
        }

        public bool CanUse(string parPetSpell)
        {
            const string getPetSpellCd1 = "{0} = 0; for index = 1,11,1 do {1} = GetPetActionInfo(index); if {1} == '";
            const string getPetSpellCd2 = "' then startTime, duration, enable = GetPetActionCooldown(index); PetSpellEnabled = duration; end end";

            var result = Functions.LuaCallWithResult(getPetSpellCd1 + parPetSpell + getPetSpellCd2);

            return result[0].Trim().Equals("0");
        }

        public void Cast(string parPetSpell)
        {
            const string castPetSpell1 = "for index = 1,11,1 do curName = GetPetActionInfo(index); if curName == '";
            const string castPetSpell2 = "' then CastPetAction(index); break end end";

            Functions.LuaCall(castPetSpell1 + parPetSpell + castPetSpell2);
        }
    }
}
