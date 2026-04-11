using System;

namespace GameData.Core.Enums;

[Flags]
public enum SceneEnvironmentFlags : uint
{
    None = 0,
    Indoors = 1 << 0,
    MountAllowed = 1 << 1,
}
