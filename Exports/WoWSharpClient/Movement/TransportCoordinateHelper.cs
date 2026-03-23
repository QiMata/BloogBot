using GameData.Core.Models;
using System;

namespace WoWSharpClient.Movement
{
    internal static class TransportCoordinateHelper
    {
        private const float TwoPi = MathF.PI * 2f;

        public static Position LocalToWorld(Position localOffset, Position transportPosition, float transportOrientation)
        {
            float cosO = MathF.Cos(transportOrientation);
            float sinO = MathF.Sin(transportOrientation);

            return new Position(
                transportPosition.X + localOffset.X * cosO - localOffset.Y * sinO,
                transportPosition.Y + localOffset.X * sinO + localOffset.Y * cosO,
                transportPosition.Z + localOffset.Z);
        }

        public static Position WorldToLocal(Position worldPosition, Position transportPosition, float transportOrientation)
        {
            float cosO = MathF.Cos(transportOrientation);
            float sinO = MathF.Sin(transportOrientation);
            float dx = worldPosition.X - transportPosition.X;
            float dy = worldPosition.Y - transportPosition.Y;

            return new Position(
                dx * cosO + dy * sinO,
                dy * cosO - dx * sinO,
                worldPosition.Z - transportPosition.Z);
        }

        public static float LocalToWorldFacing(float localFacing, float transportOrientation)
            => NormalizeFacing(localFacing + transportOrientation);

        public static float WorldToLocalFacing(float worldFacing, float transportOrientation)
            => NormalizeFacing(worldFacing - transportOrientation);

        public static float NormalizeFacing(float facing)
        {
            float normalized = facing % TwoPi;
            if (normalized < 0f)
                normalized += TwoPi;

            return normalized;
        }
    }
}
