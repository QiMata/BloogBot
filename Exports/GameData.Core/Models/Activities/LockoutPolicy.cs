namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Scheduling hint about how an activity locks out the participating
    /// character. The authoritative "is this character locked from this
    /// instance right now?" answer comes from the MaNGOS
    /// <c>character_instance</c> table and is consulted by the Phase 2
    /// <c>LockoutVerifier</c> per Spec/04.
    /// </summary>
    public sealed record LockoutPolicy(LockoutType Type, string? LockoutKey)
    {
        public static LockoutPolicy None() => new(LockoutType.None, null);
    }

    public enum LockoutType
    {
        None,
        PerCharacterDaily,
        PerCharacterWeekly,
        PerInstanceId,
    }
}
