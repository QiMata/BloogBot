namespace GameData.Core.Enums;

public enum CreateCharacterResult : byte
{
    InProgress = 0x2E,
    Success = 0x2F,
    Error = 0x30,
    Failed = 0x31,
    NameInUse = 0x32,
    Disabled = 0x33,
    PvpTeamsViolation = 0x34,
    ServerLimit = 0x35,
    AccountLimit = 0x36
}