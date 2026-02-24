using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;

namespace WoWSharpClient.Models
{
    public class WoWContainer(HighGuid highGuid, WoWObjectType objectType = WoWObjectType.Container) : WoWItem(highGuid, objectType), IWoWContainer
    {
        public int NumOfSlots { get; set; }

        /// <summary>
        /// Raw GUID field values for container slots. Stored as pairs: [slot*2]=low, [slot*2+1]=high.
        /// Supports up to 36 slots (72 uint32 entries).
        /// </summary>
        public uint[] Slots { get; } = new uint[72];

        public ulong GetItemGuid(int parSlot)
        {
            int index = parSlot * 2;
            if (index < 0 || index + 1 >= Slots.Length) return 0;
            return ((ulong)Slots[index + 1] << 32) | Slots[index];
        }

        public override WoWObject Clone()
        {
            var clone = new WoWContainer(HighGuid, ObjectType);
            clone.CopyFrom(this);
            return clone;
        }

        public override void CopyFrom(WoWObject sourceBase)
        {
            base.CopyFrom(sourceBase);

            if (sourceBase is not WoWContainer source)
                return;

            NumOfSlots = source.NumOfSlots;
            Array.Copy(source.Slots, Slots, Slots.Length);
        }
    }
}
