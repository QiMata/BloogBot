using GameData.Core.Interfaces;

namespace BotRunner.Helpers;

/// <summary>
/// Extracts physics wall-contact state from the object manager.
/// The interface already exposes default-implemented properties, so this
/// helper simply bundles them into a single tuple for call-site brevity.
/// </summary>
public static class PhysicsStateHelper
{
    public static (bool HitWall, float NormalX, float NormalY, float BlockedFraction) GetPhysicsState(IObjectManager objectManager)
    {
        var wallNormal = objectManager.PhysicsWallNormal2D;
        return (objectManager.PhysicsHitWall, wallNormal.X, wallNormal.Y, objectManager.PhysicsBlockedFraction);
    }
}
