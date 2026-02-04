namespace GameData.Core.Enums;

public enum DiminishingLevels
{
    DIMINISHING_LEVEL_1 = 0,         ///<Won't make a difference to stun duration
    DIMINISHING_LEVEL_2 = 1,         ///<Reduces stun time by 50%
    DIMINISHING_LEVEL_3 = 2,         ///<Reduces stun time by 75%
    DIMINISHING_LEVEL_IMMUNE = 3          ///<The target is immune to the DiminishingGrouop
}