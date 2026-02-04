namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Data emitted when a spell cast starts.
    /// </summary>
    /// <param name="SpellId">The spell ID.</param>
    /// <param name="CastTime">Cast time in milliseconds.</param>
    /// <param name="TargetGuid">Optional target GUID.</param>
    /// <param name="CastType">Type of cast (normal, channeled, etc.).</param>
    /// <param name="Timestamp">Event timestamp.</param>
    public record SpellCastStartData(
        uint SpellId,
        uint CastTime,
        ulong? TargetGuid,
        WoWSharpClient.Networking.ClientComponents.I.SpellCastType CastType,
        DateTime Timestamp
    );

    /// <summary>
    /// Data emitted when a spell cast completes.
    /// </summary>
    /// <param name="SpellId">The spell ID.</param>
    /// <param name="TargetGuid">Optional target GUID.</param>
    /// <param name="Timestamp">Event timestamp.</param>
    public record SpellCastCompleteData(
        uint SpellId,
        ulong? TargetGuid,
        DateTime Timestamp
    );

    /// <summary>
    /// Data for channeling start/stop events.
    /// </summary>
    /// <param name="SpellId">The channeled spell ID.</param>
    /// <param name="IsChanneling">True if channeling started, false if ended.</param>
    /// <param name="Duration">Optional duration in ms.</param>
    /// <param name="Timestamp">Event timestamp.</param>
    public record ChannelingData(
        uint SpellId,
        bool IsChanneling,
        uint? Duration,
        DateTime Timestamp
    );

    /// <summary>
    /// Data for spell cooldown updates.
    /// </summary>
    /// <param name="SpellId">The spell ID.</param>
    /// <param name="CooldownTime">Cooldown duration in ms.</param>
    /// <param name="Timestamp">Event timestamp.</param>
    public record SpellCooldownData(
        uint SpellId,
        uint CooldownTime,
        DateTime Timestamp
    );

    /// <summary>
    /// Data for spell hit results (damage or heal).
    /// </summary>
    /// <param name="SpellId">The spell ID.</param>
    /// <param name="TargetGuid">The target GUID.</param>
    /// <param name="Damage">Optional damage amount.</param>
    /// <param name="Heal">Optional heal amount.</param>
    /// <param name="Timestamp">Event timestamp.</param>
    public record SpellHitData(
        uint SpellId,
        ulong TargetGuid,
        uint? Damage,
        uint? Heal,
        DateTime Timestamp
    );
}
