using GameData.Core.Enums;
using System.Runtime.InteropServices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ForegroundBotRunner.Mem
{
    static public class Functions
    {
        private static readonly object locker = new();
        private static readonly Random random = new();

        [DllImport("FastCall.dll", EntryPoint = "BuyVendorItem")]
        private static extern int BuyVendorItemFunction(int itemId, int quantity, ulong vendorGuid, nint ptr);

        static public void BuyVendorItem(ulong vendorGuid, int itemId, int quantity)
        {
            if (BuyVendorItemFunction(itemId, quantity, vendorGuid, MemoryAddresses.BuyVendorItemFunPtr) == 0)
                Log.Warning("[FG] BuyVendorItem SEH exception caught — skipping this frame");
        }

        [DllImport("FastCall.dll", EntryPoint = "EnumerateVisibleObjects")]
        private static extern int EnumerateVisibleObjectsFunction(nint callback, int filter, nint ptr);

        /// <summary>
        /// Enumerates visible objects via WoW's native iterator.
        /// Returns false if enumeration was aborted by an ACCESS_VIOLATION (zone boundary cache reset).
        /// </summary>
        static public bool EnumerateVisibleObjects(nint callback, int filter)
        {
            if (MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase) == nint.Zero) return false;
            return EnumerateVisibleObjectsFunction(callback, filter, MemoryAddresses.EnumerateVisibleObjectsFunPtr) != 0;
        }

        // ====================================================================
        // All WoW native function calls routed through FastCall.dll SEH wrappers.
        // .NET 8 ignores [HandleProcessCorruptedStateExceptions], so catch(AV)
        // is dead code. C++ __try/__except is the only reliable SEH protection.
        // ====================================================================

        [DllImport("FastCall.dll", EntryPoint = "GetCreatureRankSafe")]
        private static extern int GetCreatureRankSafeFunction(nint unitPtr, nint funcPtr);

        static public int GetCreatureRank(nint unitPtr)
        {
            var result = GetCreatureRankSafeFunction(unitPtr, MemoryAddresses.GetCreatureRankFunPtr);
            return result;
        }

        [DllImport("FastCall.dll", EntryPoint = "GetCreatureTypeSafe")]
        private static extern int GetCreatureTypeSafeFunction(nint unitPtr, nint funcPtr);

        static public CreatureType GetCreatureType(nint unitPtr)
        {
            return (CreatureType)GetCreatureTypeSafeFunction(unitPtr, MemoryAddresses.GetCreatureTypeFunPtr);
        }

        [DllImport("FastCall.dll", EntryPoint = "GetItemCacheEntrySafe")]
        private static extern nint GetItemCacheEntrySafeFunction(
            nint basePtr, int itemId, nint unknown,
            int unused1, int unused2, char unused3, nint funcPtr);

        static public nint GetItemCacheEntry(int itemId)
        {
            return GetItemCacheEntrySafeFunction(
                MemoryAddresses.ItemCacheEntryBasePtr, itemId, nint.Zero, 0, 0, (char)0,
                MemoryAddresses.GetItemCacheEntryFunPtr);
        }

        [DllImport("FastCall.dll", EntryPoint = "GetObjectPtrByGuidSafe")]
        private static extern nint GetObjectPtrByGuidSafeFunction(ulong guid, nint funcPtr);

        static public nint GetObjectPtr(ulong guid)
        {
            if (MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase) == nint.Zero) return nint.Zero;
            return GetObjectPtrByGuidSafeFunction(guid, MemoryAddresses.GetObjectPtrFunPtr);
        }

        [DllImport("FastCall.dll", EntryPoint = "GetPlayerGuidSafe")]
        private static extern ulong GetPlayerGuidSafeFunction(nint funcPtr);

        static public ulong GetPlayerGuid()
        {
            if (MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase) == nint.Zero) return 0;
            return GetPlayerGuidSafeFunction(MemoryAddresses.GetPlayerGuidFunPtr);
        }

        [DllImport("FastCall.dll", EntryPoint = "GetText")]
        private static extern nint GetTextFunction(string varName, nint ptr);

        static public nint GetText(string varName)
        {
            return GetTextFunction(varName, MemoryAddresses.GetTextFunPtr);
        }

        [DllImport("FastCall.dll", EntryPoint = "GetUnitReactionSafe")]
        private static extern int GetUnitReactionSafeFunction(nint unitPtr1, nint unitPtr2, nint funcPtr);

        static public UnitReaction GetUnitReaction(nint unitPtr1, nint unitPtr2)
        {
            return (UnitReaction)GetUnitReactionSafeFunction(unitPtr1, unitPtr2, MemoryAddresses.GetUnitReactionFunPtr);
        }

        [DllImport("FastCall.dll", EntryPoint = "IsSpellOnCooldownSafe")]
        private static extern int IsSpellOnCooldownSafeFunction(
            nint cooldownPtr, int spellId, int unused1,
            ref int cooldownDuration, int unused2, int unused3, nint funcPtr);

        static public bool IsSpellOnCooldown(int spellId)
        {
            var cooldownDuration = 0;
            var result = IsSpellOnCooldownSafeFunction(
                0x00CECAEC, spellId, 0,
                ref cooldownDuration, 0, 0,
                MemoryAddresses.IsSpellOnCooldownFunPtr);
            if (result == 0)
                Log.Warning("[FG] IsSpellOnCooldown SEH exception for spellId {SpellId}", spellId);
            return cooldownDuration != 0;
        }

        [DllImport("FastCall.dll", EntryPoint = "LootSlot")]
        private static extern int LootSlotFunction(int slot, nint ptr);

        static public void LootSlot(int slot)
        {
            if (LootSlotFunction(slot, MemoryAddresses.LootSlotFunPtr) == 0)
                Log.Warning("[FG] LootSlot SEH exception caught — skipping this frame");
        }

        [DllImport("FastCall.dll", EntryPoint = "LuaCall")]
        private static extern int LuaCallFunction(string code, int ptr);

        static public void LuaCall(string code)
        {
            ThreadSynchronizer.RunOnMainThread<int>(() =>
            {
                lock (locker)
                {
                    if (LuaCallFunction(code, MemoryAddresses.LuaCallFunPtr) == 0)
                        Log.Warning("[FG] LuaCall SEH exception caught — skipping this frame");
                }
                return 0;
            });
        }
        static public string[] LuaCallWithResult(string code)
        {
            return ThreadSynchronizer.RunOnMainThread<string[]>(() =>
            {
                lock (locker)
                {
                    var luaVarNames = new List<string>();
                    for (var i = 0; i < 11; i++)
                    {
                        var currentPlaceHolder = "{" + i + "}";
                        if (!code.Contains(currentPlaceHolder)) break;
                        var randomName = GetRandomLuaVarName();
                        code = code.Replace(currentPlaceHolder, randomName);
                        luaVarNames.Add(randomName);
                    }

                    if (LuaCallFunction(code, MemoryAddresses.LuaCallFunPtr) == 0)
                    {
                        Log.Warning("[FG] LuaCallWithResult SEH exception caught — returning empty results");
                        return Array.Empty<string>();
                    }

                    var results = new List<string>();
                    foreach (var varName in luaVarNames)
                    {
                        var address = GetText(varName);
                        results.Add(MemoryManager.ReadString(address));
                    }

                    return [.. results];
                }
            });
        }

        [DllImport("FastCall.dll", EntryPoint = "ReleaseCorpseSafe")]
        private static extern int ReleaseCorpseSafeFunction(nint ptr, nint funcPtr);

        static public void ReleaseCorpse(nint ptr)
        {
            if (ReleaseCorpseSafeFunction(ptr, MemoryAddresses.ReleaseCorpseFunPtr) == 0)
                Log.Warning("[FG] ReleaseCorpse SEH exception — skipping");
        }

        [DllImport("FastCall.dll", EntryPoint = "RetrieveCorpseSafe")]
        private static extern int RetrieveCorpseSafeFunction(nint funcPtr);

        static public void RetrieveCorpse()
        {
            var result = RetrieveCorpseSafeFunction(MemoryAddresses.RetrieveCorpseFunPtr);
            Log.Information("[FG] RetrieveCorpse() returned {Result}", result);
        }

        [DllImport("FastCall.dll", EntryPoint = "SetTargetSafe")]
        private static extern int SetTargetSafeFunction(ulong guid, nint funcPtr);

        static public void SetTarget(ulong guid)
        {
            if (MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase) == nint.Zero) return;
            if (SetTargetSafeFunction(guid, MemoryAddresses.SetTargetFunPtr) == 0)
                Log.Warning("[FG] SetTarget SEH exception for GUID 0x{Guid:X}", guid);
        }

        [DllImport("FastCall.dll", EntryPoint = "SellItemByGuid")]
        private static extern int SellItemByGuidFunction(uint itemCount, ulong npcGuid, ulong itemGuid, nint sellItemFunPtr);

        static public void SellItemByGuid(uint itemCount, ulong vendorGuid, ulong itemGuid)
        {
            if (SellItemByGuidFunction(itemCount, vendorGuid, itemGuid, MemoryAddresses.SellItemByGuidFunPtr) == 0)
                Log.Warning("[FG] SellItemByGuid SEH exception caught — skipping this frame");
        }

        [DllImport("FastCall.dll", EntryPoint = "SendMovementUpdateSafe")]
        private static extern int SendMovementUpdateSafeFunction(
            nint playerPtr, nint unknown, int opCode, int unused1, int unused2, nint funcPtr);

        static public void SendMovementUpdate(nint playerPtr, int opcode)
        {
            if (SendMovementUpdateSafeFunction(playerPtr, 0x00BE1E2C, opcode, 0, 0, MemoryAddresses.SendMovementUpdateFunPtr) == 0)
                Log.Warning("[FG] SendMovementUpdate SEH exception for opcode 0x{Opcode:X}", opcode);
        }

        [DllImport("FastCall.dll", EntryPoint = "SetControlBitSafe")]
        private static extern int SetControlBitSafeFunction(nint device, int bit, int state, int tickCount, nint funcPtr);

        static public void SetControlBit(int bit, int state, int tickCount)
        {
            var ptr = MemoryManager.ReadIntPtr(MemoryAddresses.SetControlBitDevicePtr);
            if (ptr == nint.Zero)
                return; // Device not initialized yet (early world entry)
            if (SetControlBitSafeFunction(ptr, bit, state, tickCount, MemoryAddresses.SetControlBitFunPtr) == 0)
                Log.Warning("[FG] SetControlBit SEH exception — skipping");
        }

        [DllImport("FastCall.dll", EntryPoint = "SetFacingSafe")]
        private static extern int SetFacingSafeFunction(nint ptr, float facing, nint funcPtr);

        static public void SetFacing(nint playerSetFacingPtr, float facing)
        {
            if (SetFacingSafeFunction(playerSetFacingPtr, facing, MemoryAddresses.SetFacingFunPtr) == 0)
                Log.Warning("[FG] SetFacing SEH exception — skipping");
        }

        [DllImport("FastCall.dll", EntryPoint = "UseItemSafe")]
        private static extern int UseItemSafeFunction(nint itemPtr, ref ulong unused1, int unused2, nint funcPtr);

        static public void UseItem(nint itemPtr)
        {
            ulong unused1 = 0;
            if (UseItemSafeFunction(itemPtr, ref unused1, 0, MemoryAddresses.UseItemFunPtr) == 0)
                Log.Warning("[FG] UseItem SEH exception for ptr 0x{Ptr:X}", itemPtr);
        }

        // NOTE: ClickToMove (CTM) is FORBIDDEN. CTM does not work for ghost players,
        // breaking corpse runs. Use SetFacing + SetControlBit(Front) for all movement.

        // ====================================================================
        // Crash diagnostics — enables visibility into silent AV catches
        // ====================================================================

        [DllImport("FastCall.dll", EntryPoint = "SetCrashDiagnosticMode")]
        private static extern void SetCrashDiagnosticModeFunction(int mode);

        [DllImport("FastCall.dll", EntryPoint = "GetCrashDiagnosticMode")]
        private static extern int GetCrashDiagnosticModeFunction();

        [DllImport("FastCall.dll", EntryPoint = "GetTotalAVCount")]
        private static extern int GetTotalAVCountFunction();

        /// <summary>
        /// Enable diagnostic mode: all SEH exceptions are logged to WWoWLogs/fastcall_crash.log
        /// with function name, faulting address, and instruction address.
        /// When letCrash=true, exceptions propagate instead of being caught (for dump analysis).
        /// </summary>
        static public void EnableCrashDiagnostics(bool letCrash = false)
        {
            SetCrashDiagnosticModeFunction(letCrash ? 1 : 0);
            Log.Information("[FG] FastCall crash diagnostics {Mode} (letCrash={LetCrash})",
                letCrash ? "ENABLED" : "logging-only", letCrash);
        }

        /// <summary>Returns the total number of ACCESS_VIOLATION exceptions caught since DLL load.</summary>
        static public int GetTotalAVCount() => GetTotalAVCountFunction();

        private static string GetRandomLuaVarName()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            return new string([.. chars.Select(c => chars[random.Next(chars.Length)]).Take(8)]);
        }
    }
}
