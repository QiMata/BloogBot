namespace GameData.Core.Enums;

public enum AuraRemoveMode
{
    AURA_REMOVE_BY_DEFAULT,
    AURA_REMOVE_BY_STACK,           ///< at replace by similar aura
    AURA_REMOVE_BY_CANCEL,          ///< It was cancelled by the user (needs confirmation)
    AURA_REMOVE_BY_DISPEL,          ///< It was dispelled by ie Remove Magic
    AURA_REMOVE_BY_DEATH,           ///< The \ref Unit died and there for it was removed
    AURA_REMOVE_BY_DELETE,          ///< use for speedup and prevent unexpected effects at player logout/pet unsummon (must be used _only_ after save), delete.
    AURA_REMOVE_BY_SHIELD_BREAK,    ///< when absorb shield is removed by damage
    AURA_REMOVE_BY_EXPIRE,          ///< at duration end
    AURA_REMOVE_BY_TRACKING         ///< aura is removed because of a conflicting tracked aura
};