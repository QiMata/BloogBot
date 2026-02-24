using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;

namespace WoWSharpClient.Models
{
    public class WoWLocalPet(HighGuid highGuid, WoWObjectType objectType = WoWObjectType.Unit) : WoWUnit(highGuid, objectType), IWoWLocalPet, ICloneable
    {
        public void Attack() { }

        public bool CanUse(string spellName) => HasBuff(spellName) || !IsCasting;

        public void Cast(string spellName) { }

        public void FollowPlayer() { }

        public bool IsHappy() => true;

        public object Clone() => MemberwiseClone();
    }
}
