namespace GameData.Core.Enums;

public enum UnitVisibility
{
    VISIBILITY_OFF = 0,                      // absolute, not detectable, GM-like, can see all other
    VISIBILITY_ON = 1,
    VISIBILITY_GROUP_STEALTH = 2,                      // detect chance, seen and can see group members
    VISIBILITY_GROUP_INVISIBILITY = 3,                      // invisibility, can see and can be seen only another invisible unit or invisible detection unit, set only if not stealthed, and in checks not used (mask used instead)
    VISIBILITY_GROUP_NO_DETECT = 4,                      // state just at stealth apply for update Grid state. Don't remove, otherwise stealth spells will break
    VISIBILITY_REMOVE_CORPSE = 5                       // special totally not detectable visibility for force delete object while removing a corpse
};