using System.ComponentModel;

namespace GameData.Core.Enums;

public enum TargetMarker : byte
{
    [Description("None")]
    None,
    [Description("Star")]
    Star,
    [Description("Circle")]
    Circle,
    [Description("Diamond")]
    Diamond,
    [Description("Triangle")]
    Triangle,
    [Description("Moon")]
    Moon,
    [Description("Square")]
    Square,
    [Description("Cross")]
    Cross,
    [Description("Skull")]
    Skull
}