using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;
using System.Collections.Generic;

namespace WoWSharpClient.Models
{
    public class WoWGameObject(HighGuid highGuid, WoWObjectType objectType = WoWObjectType.GameObj) : WoWObject(highGuid, objectType), IWoWGameObject
    {
        /// <summary>
        /// Back-reference to the owning ObjectManager instance.
        /// Set after construction by the ObjectManager when creating/tracking objects.
        /// Used to replace static WoWSharpObjectManager.Instance references.
        /// </summary>
        internal WoWSharpObjectManager ObjectManager { get; set; }

        public HighGuid CreatedBy { get; private set; } = new(new byte[4], new byte[4]);
        public uint DisplayId { get; set; }
        public uint Flags { get; set; }
        public float[] Rotation { get; private set; } = new float[32];
        public GOState GoState { get; set; }
        public DynamicFlags DynamicFlags { get; set; }
        public uint FactionTemplate { get; set; }
        public uint TypeId { get; set; }
        public uint Level { get; set; }
        public uint ArtKit { get; set; }
        public uint AnimProgress { get; set; }
        public string Name { get; set; } = string.Empty;
        public SplineType MovementSplineType { get; set; }
        public ulong MovementSplineTargetGuid { get; set; }
        public float MovementFacingAngle { get; set; }
        public Position MovementFacingSpot { get; set; } = new(0, 0, 0);
        public uint MovementSplineTimestamp { get; set; }
        public List<Position> MovementSplinePoints { get; set; } = [];

        public override WoWObject Clone()
        {
            var clone = new WoWGameObject(HighGuid, ObjectType);
            clone.CopyFrom(this);
            return clone;
        }

        public override void CopyFrom(WoWObject sourceBase)
        {
            base.CopyFrom(sourceBase);

            if (sourceBase is not WoWGameObject source) return;

            CreatedBy = source.CreatedBy;
            DisplayId = source.DisplayId;
            Flags = source.Flags;
            GoState = source.GoState;
            DynamicFlags = source.DynamicFlags;
            FactionTemplate = source.FactionTemplate;
            TypeId = source.TypeId;
            Level = source.Level;
            ArtKit = source.ArtKit;
            AnimProgress = source.AnimProgress;
            Name = source.Name;
            MovementSplineType = source.MovementSplineType;
            MovementSplineTargetGuid = source.MovementSplineTargetGuid;
            MovementFacingAngle = source.MovementFacingAngle;
            MovementFacingSpot = source.MovementFacingSpot;
            MovementSplineTimestamp = source.MovementSplineTimestamp;
            MovementSplinePoints = [.. source.MovementSplinePoints];

            if (Rotation.Length != source.Rotation.Length)
                Rotation = new float[source.Rotation.Length];

            Array.Copy(source.Rotation, Rotation, Rotation.Length);
        }

        public Position GetPointBehindUnit(float distance)
        {
            float behindAngle = Facing + (float)Math.PI;
            float x = Position.X + (float)Math.Cos(behindAngle) * distance;
            float y = Position.Y + (float)Math.Sin(behindAngle) * distance;
            return new Position(x, y, Position.Z);
        }

        public void Interact()
        {
            ObjectManager.InteractWithGameObject(Guid);
        }
    }
}
