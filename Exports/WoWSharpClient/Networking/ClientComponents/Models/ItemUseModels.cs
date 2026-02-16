using System;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Emitted when item use begins (may include cast time for use-on-use items).
    /// </summary>
    public readonly record struct ItemUseStartedData(
        ulong? ItemGuid,
        uint SpellId,
        uint CastTime,
        DateTime Timestamp
    );

    /// <summary>
    /// Emitted when item use completes (item consumed/activated or effect applied).
    /// </summary>
    public readonly record struct ItemUseCompletedData(
        ulong? ItemGuid,
        uint ItemId,
        ulong? TargetGuid,
        uint SpellId,
        DateTime Timestamp
    );

    /// <summary>
    /// Emitted when item use fails (cooldown, invalid target, etc.).
    /// </summary>
    public readonly record struct ItemUseErrorData(
        ulong? ItemGuid,
        string ErrorMessage,
        DateTime Timestamp
    );

    /// <summary>
    /// Emitted when a consumable effect is applied (if detectable from opcodes).
    /// </summary>
    public readonly record struct ConsumableEffectData(
        uint ItemId,
        uint SpellId,
        DateTime Timestamp
    );
}
