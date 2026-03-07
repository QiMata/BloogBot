using GameData.Core.Enums;
using System.Runtime.ExceptionServices;
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

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int GetCreatureRankDelegate
            (nint unitPtr);

        private static readonly GetCreatureRankDelegate GetCreatureRankFunction =
            Marshal.GetDelegateForFunctionPointer<GetCreatureRankDelegate>(MemoryAddresses.GetCreatureRankFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public int GetCreatureRank(nint unitPtr)
        {
            try
            {
                return GetCreatureRankFunction(unitPtr);
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] GetCreatureRank SEH exception for ptr 0x{Ptr:X} — returning 0", unitPtr);
                return 0;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int GetCreatureTypeDelegate(nint unitPtr);

        private static readonly GetCreatureTypeDelegate GetCreatureTypeFunction =
            Marshal.GetDelegateForFunctionPointer<GetCreatureTypeDelegate>(MemoryAddresses.GetCreatureTypeFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public CreatureType GetCreatureType(nint unitPtr)
        {
            try
            {
                return (CreatureType)GetCreatureTypeFunction(unitPtr);
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] GetCreatureType SEH exception for ptr 0x{Ptr:X} — returning NotSpecified", unitPtr);
                return CreatureType.NotSpecified;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate nint ItemCacheGetRowDelegate(
            nint ptr,
            int itemId,
            nint unknown,
            int unused1,
            int unused2,
            char unused3);

        private static readonly ItemCacheGetRowDelegate GetItemCacheEntryFunction =
            Marshal.GetDelegateForFunctionPointer<ItemCacheGetRowDelegate>(MemoryAddresses.GetItemCacheEntryFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public nint GetItemCacheEntry(int itemId)
        {
            try
            {
                return GetItemCacheEntryFunction(MemoryAddresses.ItemCacheEntryBasePtr, itemId, nint.Zero, 0, 0, (char)0);
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] GetItemCacheEntry SEH exception for itemId {ItemId} — returning null", itemId);
                return nint.Zero;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate nint GetObjectPtrDelegate(ulong guid);

        private static readonly GetObjectPtrDelegate GetObjectPtrFunction =
            Marshal.GetDelegateForFunctionPointer<GetObjectPtrDelegate>(MemoryAddresses.GetObjectPtrFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public nint GetObjectPtr(ulong guid)
        {
            try
            {
                if (MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase) == nint.Zero) return nint.Zero;
                return GetObjectPtrFunction(guid);
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] GetObjectPtr SEH exception for GUID 0x{Guid:X} — returning null", guid);
                return nint.Zero;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ulong GetPlayerGuidDelegate();

        private static readonly GetPlayerGuidDelegate GetPlayerGuidFunction =
            Marshal.GetDelegateForFunctionPointer<GetPlayerGuidDelegate>(MemoryAddresses.GetPlayerGuidFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public ulong GetPlayerGuid()
        {
            try
            {
                if (MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase) == nint.Zero) return 0;
                return GetPlayerGuidFunction();
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] GetPlayerGuid SEH exception — returning 0");
                return 0;
            }
        }

        [DllImport("FastCall.dll", EntryPoint = "GetText")]
        private static extern nint GetTextFunction(string varName, nint ptr);

        static public nint GetText(string varName)
        {
            return GetTextFunction(varName, MemoryAddresses.GetTextFunPtr);
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int GetUnitReactionDelegate(nint unitPtr1, nint unitPtr2);

        private static readonly GetUnitReactionDelegate GetUnitReactionFunction =
            Marshal.GetDelegateForFunctionPointer<GetUnitReactionDelegate>(MemoryAddresses.GetUnitReactionFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public UnitReaction GetUnitReaction(nint unitPtr1, nint unitPtr2)
        {
            try
            {
                return (UnitReaction)GetUnitReactionFunction(unitPtr1, unitPtr2);
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] GetUnitReaction SEH exception — returning Neutral");
                return UnitReaction.Neutral;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void IsSpellOnCooldownDelegate(
            nint spellCooldownPtr,
            int spellId,
            int unused1,
            ref int cooldownDuration,
            int unused2,
            bool unused3);

        private static readonly IsSpellOnCooldownDelegate IsSpellOnCooldownFunction =
            Marshal.GetDelegateForFunctionPointer<IsSpellOnCooldownDelegate>(MemoryAddresses.IsSpellOnCooldownFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public bool IsSpellOnCooldown(int spellId)
        {
            try
            {
                var cooldownDuration = 0;
                IsSpellOnCooldownFunction(
                    0x00CECAEC,
                    spellId,
                    0,
                    ref cooldownDuration,
                    0,
                    false);

                return cooldownDuration != 0;
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] IsSpellOnCooldown SEH exception for spellId {SpellId} — returning false", spellId);
                return false;
            }
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

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int ReleaseCorpseDelegate(nint ptr);

        private static readonly ReleaseCorpseDelegate ReleaseCorpseFunction =
            Marshal.GetDelegateForFunctionPointer<ReleaseCorpseDelegate>(MemoryAddresses.ReleaseCorpseFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public void ReleaseCorpse(nint ptr)
        {
            try
            {
                ReleaseCorpseFunction(ptr);
            }
            catch (AccessViolationException)
            {
                Log.Error("AccessViolationException occurred while trying to release corpse. Most likely, this is due to a transient error that caused the player pointer to temporarily equal IntPtr.Zero. The bot should keep trying to release and recover from this error.");
            }
        }

        private delegate int RetrieveCorpseDelegate();

        private static readonly RetrieveCorpseDelegate RetrieveCorpseFunction =
            Marshal.GetDelegateForFunctionPointer<RetrieveCorpseDelegate>(MemoryAddresses.RetrieveCorpseFunPtr);

        static public void RetrieveCorpse()
        {
            var result = RetrieveCorpseFunction();
            Log.Information("[FG] RetrieveCorpse() returned {Result}", result);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetTargetDelegate(ulong guid);

        private static readonly SetTargetDelegate SetTargetFunction =
            Marshal.GetDelegateForFunctionPointer<SetTargetDelegate>(MemoryAddresses.SetTargetFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public void SetTarget(ulong guid)
        {
            try
            {
                if (MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase) == nint.Zero) return;
                SetTargetFunction(guid);
            }
            catch (AccessViolationException)
            {
                Log.Error("[FG] AccessViolationException in SetTarget for GUID 0x{Guid:X}", guid);
            }
        }

        [DllImport("FastCall.dll", EntryPoint = "SellItemByGuid")]
        private static extern int SellItemByGuidFunction(uint itemCount, ulong npcGuid, ulong itemGuid, nint sellItemFunPtr);

        static public void SellItemByGuid(uint itemCount, ulong vendorGuid, ulong itemGuid)
        {
            if (SellItemByGuidFunction(itemCount, vendorGuid, itemGuid, MemoryAddresses.SellItemByGuidFunPtr) == 0)
                Log.Warning("[FG] SellItemByGuid SEH exception caught — skipping this frame");
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void SendMovementUpdateDelegate(
            nint playerPtr,
            nint unknown,
            int OpCode,
            int unknown2,
            int unknown3);

        private static readonly SendMovementUpdateDelegate SendMovementUpdateFunction =
            Marshal.GetDelegateForFunctionPointer<SendMovementUpdateDelegate>(MemoryAddresses.SendMovementUpdateFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public void SendMovementUpdate(nint playerPtr, int opcode)
        {
            try
            {
                SendMovementUpdateFunction(playerPtr, 0x00BE1E2C, opcode, 0, 0);
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] SendMovementUpdate SEH exception for opcode 0x{Opcode:X} — skipping", opcode);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void SetControlBitDelegate(nint device, int bit, int state, int tickCount);

        private static readonly SetControlBitDelegate SetControlBitFunction =
            Marshal.GetDelegateForFunctionPointer<SetControlBitDelegate>(MemoryAddresses.SetControlBitFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public void SetControlBit(int bit, int state, int tickCount)
        {
            try
            {
                var ptr = MemoryManager.ReadIntPtr(MemoryAddresses.SetControlBitDevicePtr);
                SetControlBitFunction(ptr, bit, state, tickCount);
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] SetControlBit SEH exception — skipping");
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void SetFacingDelegate(nint playerSetFacingPtr, float facing);

        private static readonly SetFacingDelegate SetFacingFunction =
            Marshal.GetDelegateForFunctionPointer<SetFacingDelegate>(MemoryAddresses.SetFacingFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public void SetFacing(nint playerSetFacingPtr, float facing)
        {
            try
            {
                SetFacingFunction(playerSetFacingPtr, facing);
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] SetFacing SEH exception — skipping");
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void UseItemDelegate(nint itemPtr, ref ulong unused1, int unused2);

        private static readonly UseItemDelegate UseItemFunction =
            Marshal.GetDelegateForFunctionPointer<UseItemDelegate>(MemoryAddresses.UseItemFunPtr);

        [HandleProcessCorruptedStateExceptions]
        static public void UseItem(nint itemPtr)
        {
            try
            {
                ulong unused1 = 0;
                UseItemFunction(itemPtr, ref unused1, 0);
            }
            catch (AccessViolationException)
            {
                Log.Warning("[FG] UseItem SEH exception for ptr 0x{Ptr:X} — skipping", itemPtr);
            }
        }

        private static string GetRandomLuaVarName()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            return new string([.. chars.Select(c => chars[random.Next(chars.Length)]).Take(8)]);
        }
    }
}
