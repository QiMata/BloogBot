using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using Serilog;
using System;

namespace ForegroundBotRunner.CombatRotations
{
    /// <summary>
    /// Handles food/drink consumption during rest phases.
    /// Uses Lua tooltip scanning to find consumables in bags.
    /// </summary>
    public static class RestHelper
    {
        private static int _lastFoodAttempt;
        private static int _lastDrinkAttempt;
        private const int ATTEMPT_COOLDOWN_MS = 3000;

        /// <summary>
        /// Try to eat food from inventory. Returns true if food was used.
        /// </summary>
        public static bool TryEatFood(WoWPlayer player)
        {
            if (player.IsEating) return true; // Already eating

            // Throttle attempts
            if (Environment.TickCount - _lastFoodAttempt < ATTEMPT_COOLDOWN_MS)
                return false;

            _lastFoodAttempt = Environment.TickCount;

            try
            {
                // Lua: scan all bag slots, check tooltip for "Restores X health", use first match
                Functions.LuaCall(
                    "for bag=0,4 do " +
                        "for slot=1,GetContainerNumSlots(bag) do " +
                            "local link=GetContainerItemLink(bag,slot) " +
                            "if link then " +
                                "GameTooltip:SetOwner(UIParent,'ANCHOR_NONE') " +
                                "GameTooltip:SetBagItem(bag,slot) " +
                                "for i=1,GameTooltip:NumLines() do " +
                                    "local t=getglobal('GameTooltipTextLeft'..i):GetText() " +
                                    "if t and strfind(t,'Use: Restores %d+ health') then " +
                                        "UseContainerItem(bag,slot) " +
                                        "GameTooltip:Hide() " +
                                        "return " +
                                    "end " +
                                "end " +
                                "GameTooltip:Hide() " +
                            "end " +
                        "end " +
                    "end");
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[RestHelper] TryEatFood error");
                return false;
            }
        }

        /// <summary>
        /// Try to drink water from inventory. Returns true if drink was used.
        /// </summary>
        public static bool TryDrinkWater(WoWPlayer player)
        {
            if (player.IsDrinking) return true; // Already drinking

            // Throttle attempts
            if (Environment.TickCount - _lastDrinkAttempt < ATTEMPT_COOLDOWN_MS)
                return false;

            _lastDrinkAttempt = Environment.TickCount;

            try
            {
                // Lua: scan all bag slots, check tooltip for "Restores X mana", use first match
                Functions.LuaCall(
                    "for bag=0,4 do " +
                        "for slot=1,GetContainerNumSlots(bag) do " +
                            "local link=GetContainerItemLink(bag,slot) " +
                            "if link then " +
                                "GameTooltip:SetOwner(UIParent,'ANCHOR_NONE') " +
                                "GameTooltip:SetBagItem(bag,slot) " +
                                "for i=1,GameTooltip:NumLines() do " +
                                    "local t=getglobal('GameTooltipTextLeft'..i):GetText() " +
                                    "if t and strfind(t,'Use: Restores %d+ mana') then " +
                                        "UseContainerItem(bag,slot) " +
                                        "GameTooltip:Hide() " +
                                        "return " +
                                    "end " +
                                "end " +
                                "GameTooltip:Hide() " +
                            "end " +
                        "end " +
                    "end");
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[RestHelper] TryDrinkWater error");
                return false;
            }
        }

        /// <summary>
        /// Check if the player has mana (mana/mana-like resource).
        /// Warriors/Rogues don't need drink.
        /// </summary>
        public static bool UsesMana(LocalPlayer player)
        {
            return player.MaxMana > 0;
        }

        /// <summary>
        /// Get mana percentage, returns 100 if the class doesn't use mana.
        /// </summary>
        public static float GetManaPct(LocalPlayer player)
        {
            return player.MaxMana > 0 ? (float)player.Mana / player.MaxMana * 100 : 100;
        }

        /// <summary>
        /// Get health percentage.
        /// </summary>
        public static float GetHealthPct(LocalPlayer player)
        {
            return player.MaxHealth > 0 ? (float)player.Health / player.MaxHealth * 100 : 100;
        }
    }
}
