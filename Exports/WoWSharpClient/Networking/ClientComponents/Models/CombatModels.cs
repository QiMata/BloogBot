using System;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Represents combat state change data.
    /// </summary>
    /// <param name="IsInCombat">Whether the character is in combat.</param>
    /// <param name="TargetGuid">The current combat target GUID.</param>
    /// <param name="Strategy">The combat strategy being used.</param>
    /// <param name="Timestamp">When the state change occurred.</param>
    public record CombatStateData(
        bool IsInCombat,
        ulong? TargetGuid,
        CombatStrategy Strategy,
        DateTime Timestamp
    );

    /// <summary>
    /// Represents pet command operation data.
    /// </summary>
    /// <param name="PetGuid">The GUID of the pet.</param>
    /// <param name="Command">The command given to the pet.</param>
    /// <param name="TargetGuid">The target GUID for the command (if applicable).</param>
    /// <param name="AbilityId">The ability ID for ability commands (if applicable).</param>
    /// <param name="Timestamp">When the command was issued.</param>
    public record PetCommandData(
        ulong PetGuid,
        PetCommand Command,
        ulong? TargetGuid = null,
        uint? AbilityId = null,
        DateTime? Timestamp = null
    );

    /// <summary>
    /// Represents aura/buff update data.
    /// </summary>
    /// <param name="AuraId">The ID of the aura.</param>
    /// <param name="IsActive">Whether the aura is active or was removed.</param>
    /// <param name="CasterGuid">The GUID of the aura caster.</param>
    /// <param name="Duration">The duration of the aura in milliseconds.</param>
    /// <param name="Timestamp">When the update occurred.</param>
    public record AuraUpdateData(
        uint AuraId,
        bool IsActive,
        ulong? CasterGuid = null,
        uint? Duration = null,
        DateTime? Timestamp = null
    );

    /// <summary>
    /// Represents combat item usage data.
    /// </summary>
    /// <param name="ItemGuid">The GUID of the item used.</param>
    /// <param name="TargetGuid">The target GUID for the item (if applicable).</param>
    /// <param name="UsageType">The type of item usage.</param>
    /// <param name="Timestamp">When the item was used.</param>
    public record CombatItemUseData(
        ulong ItemGuid,
        ulong? TargetGuid,
        CombatItemUsageType UsageType,
        DateTime Timestamp
    );

    /// <summary>
    /// Represents combat error data.
    /// </summary>
    /// <param name="Operation">The operation that failed.</param>
    /// <param name="ErrorMessage">The error message.</param>
    /// <param name="TargetGuid">The target GUID (if applicable).</param>
    /// <param name="Timestamp">When the error occurred.</param>
    public record CombatErrorData(
        string Operation,
        string ErrorMessage,
        ulong? TargetGuid = null,
        DateTime? Timestamp = null
    );

    /// <summary>
    /// Represents detailed aura information.
    /// </summary>
    /// <param name="AuraId">The ID of the aura.</param>
    /// <param name="CasterGuid">The GUID of the caster.</param>
    /// <param name="Duration">The total duration in milliseconds.</param>
    /// <param name="AppliedTime">When the aura was applied.</param>
    /// <param name="IsActive">Whether the aura is currently active.</param>
    public record AuraData(
        uint AuraId,
        ulong? CasterGuid,
        uint? Duration,
        DateTime AppliedTime,
        bool IsActive
    );

    /// <summary>
    /// Represents buff information.
    /// </summary>
    /// <param name="BuffId">The ID of the buff.</param>
    /// <param name="SourceGuid">The GUID of the buff source.</param>
    /// <param name="StackCount">The number of stacks.</param>
    /// <param name="Duration">The remaining duration in milliseconds.</param>
    /// <param name="AppliedTime">When the buff was applied.</param>
    public record BuffData(
        uint BuffId,
        ulong? SourceGuid,
        uint StackCount,
        uint? Duration,
        DateTime AppliedTime
    );

    /// <summary>
    /// Represents pet state information.
    /// </summary>
    /// <param name="PetGuid">The GUID of the pet.</param>
    /// <param name="Health">Current health of the pet.</param>
    /// <param name="MaxHealth">Maximum health of the pet.</param>
    /// <param name="Mana">Current mana of the pet.</param>
    /// <param name="MaxMana">Maximum mana of the pet.</param>
    /// <param name="State">The current state of the pet.</param>
    /// <param name="TargetGuid">The current target of the pet.</param>
    public record PetState(
        ulong PetGuid,
        uint Health,
        uint MaxHealth,
        uint Mana,
        uint MaxMana,
        PetAiState State,
        ulong? TargetGuid = null
    );

    /// <summary>
    /// Combat strategies available for the enhanced combat agent.
    /// </summary>
    public enum CombatStrategy
    {
        /// <summary>
        /// No specific strategy.
        /// </summary>
        None = 0,

        /// <summary>
        /// Automatic strategy selection based on situation.
        /// </summary>
        Auto = 1,

        /// <summary>
        /// Aggressive combat focusing on maximum damage output.
        /// </summary>
        Aggressive = 2,

        /// <summary>
        /// Defensive combat focusing on survival.
        /// </summary>
        Defensive = 3,

        /// <summary>
        /// Balanced approach between offense and defense.
        /// </summary>
        Balanced = 4
    }

    /// <summary>
    /// MaNGOS 1.12.1 CommandStates — pet command actions sent via CMSG_PET_ACTION.
    /// Packed with ACT_COMMAND (0x07) via MAKE_UNIT_ACTION_BUTTON.
    /// </summary>
    public enum PetCommand
    {
        Stay = 0,
        Follow = 1,
        Attack = 2,
        Dismiss = 3
    }

    /// <summary>
    /// MaNGOS 1.12.1 ReactStates — pet reaction modes sent via CMSG_PET_ACTION.
    /// Packed with ACT_REACTION (0x06) via MAKE_UNIT_ACTION_BUTTON.
    /// </summary>
    public enum PetReactState
    {
        Passive = 0,
        Defensive = 1,
        Aggressive = 2
    }

    /// <summary>
    /// MaNGOS 1.12.1 ActiveStates — action type byte for UNIT_ACTION_BUTTON packing.
    /// </summary>
    public static class PetActionType
    {
        public const byte ACT_PASSIVE = 0x01;
        public const byte ACT_DISABLED = 0x81;
        public const byte ACT_ENABLED = 0xC1;
        public const byte ACT_COMMAND = 0x07;
        public const byte ACT_REACTION = 0x06;

        /// <summary>
        /// Packs action/spell ID and type into uint32 for CMSG_PET_ACTION data field.
        /// Lower 24 bits = action ID, upper 8 bits = action type.
        /// </summary>
        public static uint Pack(uint actionId, byte actionType) =>
            (actionId & 0x00FFFFFF) | ((uint)actionType << 24);
    }

    /// <summary>
    /// Pet AI states for tracking pet behavior.
    /// </summary>
    public enum PetAiState
    {
        /// <summary>
        /// Pet is passive and will not attack.
        /// </summary>
        Passive = 0,

        /// <summary>
        /// Pet will defend the player when attacked.
        /// </summary>
        Defensive = 1,

        /// <summary>
        /// Pet will attack any enemy in range.
        /// </summary>
        Aggressive = 2,

        /// <summary>
        /// Pet is following the player.
        /// </summary>
        Following = 3,

        /// <summary>
        /// Pet is staying in place.
        /// </summary>
        Staying = 4,

        /// <summary>
        /// Pet is attacking a target.
        /// </summary>
        Attacking = 5
    }

    /// <summary>
    /// Types of combat item usage for tracking and analytics.
    /// </summary>
    public enum CombatItemUsageType
    {
        /// <summary>
        /// Manual item usage by direct command.
        /// </summary>
        Manual = 0,

        /// <summary>
        /// Automatic healing item usage.
        /// </summary>
        AutomaticHealing = 1,

        /// <summary>
        /// Automatic mana restoration item usage.
        /// </summary>
        AutomaticMana = 2,

        /// <summary>
        /// Automatic buff item usage.
        /// </summary>
        AutomaticBuff = 3,

        /// <summary>
        /// Emergency item usage (e.g., when health is critically low).
        /// </summary>
        Emergency = 4,

        /// <summary>
        /// Consumable item usage for combat enhancement.
        /// </summary>
        CombatEnhancement = 5
    }
}