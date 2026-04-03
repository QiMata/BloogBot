using System.Collections.Generic;

namespace WoWSharpClient.Movement;

public static class LocalPhysicsMapPreloader
{
    public static void EnsureMapPreloaded(uint mapId)
        => NativeLocalPhysics.EnsureMapPreloaded(mapId);

    public static IReadOnlyList<uint> PreloadAvailableMaps()
        => NativeLocalPhysics.PreloadAvailableMaps();

    public static IReadOnlyList<uint> PreloadedMapIds
        => NativeLocalPhysics.PreloadedMapIds;
}
