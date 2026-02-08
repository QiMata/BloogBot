using ForegroundBotRunner.Mem;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;

namespace ForegroundBotRunner.Objects
{
    public class WoWGameObject : WoWObject, IWoWGameObject
    {
        internal WoWGameObject(
            nint pointer,
            HighGuid guid,
            WoWObjectType objectType)
            : base(pointer, guid, objectType)
        {
        }

        public HighGuid CreatedBy
        {
            get
            {
                var raw = MemoryManager.ReadUlong(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_CreatedByOffset));
                var parts = BitConverter.GetBytes(raw);
                return new HighGuid(parts[0..4], parts[4..8]);
            }
        }

        public uint DisplayId =>
            (uint)MemoryManager.ReadInt(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_DisplayIdOffset));

        public uint Flags =>
            (uint)MemoryManager.ReadInt(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_FlagsOffset));

        public float[] Rotation
        {
            get
            {
                var basePtr = nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_RotationOffset);
                return [
                    MemoryManager.ReadFloat(basePtr),
                    MemoryManager.ReadFloat(nint.Add(basePtr, 4)),
                    MemoryManager.ReadFloat(nint.Add(basePtr, 8)),
                    MemoryManager.ReadFloat(nint.Add(basePtr, 12))
                ];
            }
        }

        public GOState GoState =>
            (GOState)MemoryManager.ReadInt(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_StateOffset));

        public DynamicFlags DynamicFlags =>
            (DynamicFlags)MemoryManager.ReadInt(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_DynFlagsOffset));

        public uint FactionTemplate =>
            (uint)MemoryManager.ReadInt(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_FactionOffset));

        public uint TypeId =>
            (uint)MemoryManager.ReadInt(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_TypeIdOffset));

        public uint Level =>
            (uint)MemoryManager.ReadInt(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_LevelOffset));

        public uint ArtKit =>
            (uint)MemoryManager.ReadInt(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_ArtKitOffset));

        public uint AnimProgress =>
            (uint)MemoryManager.ReadInt(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWGameObject_AnimProgressOffset));

        public Position GetPointBehindUnit(float distance)
        {
            var pos = Position;
            var facing = Facing;
            return new Position(
                pos.X - (float)Math.Cos(facing) * distance,
                pos.Y - (float)Math.Sin(facing) * distance,
                pos.Z);
        }
    }
}
