namespace GameData.Core.Enums;

public static class SceneEnvironmentFlagExtensions
{
    public static bool IsIndoors(this SceneEnvironmentFlags flags)
        => (flags & SceneEnvironmentFlags.Indoors) != 0;

    public static bool AllowsMountByEnvironment(this SceneEnvironmentFlags flags)
        => !flags.IsIndoors() || (flags & SceneEnvironmentFlags.MountAllowed) != 0;
}
