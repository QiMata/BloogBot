namespace GameData.Core.Enums;

[Flags]
public enum DynamicFlags
{
    None = 0x00,
    CanBeLooted = 0x01,
    Tapped = 0x02,
    TappedByMe = 0x04
}