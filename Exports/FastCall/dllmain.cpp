#include "stdafx.h"

BOOL APIENTRY DllMain(HMODULE hModule,
    DWORD  ul_reason_for_call,
    LPVOID lpReserved
)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }

    return TRUE;
}

extern "C"
{
    struct XYZXYZ { float X1; float Y1; float Z1; float X2; float Y2; float Z2; };
    struct Intersection { float X; float Y; float Z; float R; };
    struct XYZ { float X; float Y; float Z; };

    // SEH-protected: WoW's internal object iteration can hit stale pointers during
    // area boundary cache resets (sub-zone transitions on the same continent).
    // Without __try/__except, the ACCESS_VIOLATION crashes the entire process.
    int __declspec(dllexport) __stdcall EnumerateVisibleObjects(unsigned int callback, int filter, unsigned int ptr)
    {
        __try
        {
            typedef void __fastcall func(unsigned int callback, int filter);
            func* function = (func*)ptr;
            function(callback, filter);
            return 1; // Success
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0; // ACCESS_VIOLATION or other SEH exception — caller should skip this frame
        }
    }

    // SEH-protected: Lua execution can crash during zone transitions when
    // WoW's internal state (unit pointers, object cache) is being rebuilt.
    int __declspec(dllexport) __stdcall LuaCall(char* code, unsigned int ptr)
    {
        __try
        {
            typedef void __fastcall func(char* code, const char* unused);
            func* f = (func*)ptr;
            f(code, "LuaCall");
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // SEH-protected: Looting can crash if corpse pointer becomes stale during zone transition.
    int __declspec(dllexport) __stdcall LootSlot(int slot, unsigned int ptr)
    {
        __try
        {
            typedef void __fastcall func(unsigned int slot, int unused);
            func* f = (func*)ptr;
            f(slot, 0);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // SEH-protected: GetText reads from Lua variable storage which can be stale.
    // Returns 0 on exception (null pointer — caller must check).
    unsigned int __declspec(dllexport) __stdcall GetText(char* varName, unsigned int parPtr)
    {
        __try
        {
            typedef unsigned int __fastcall func(char* varName, unsigned int nonSense, int zero);
            func* f = (func*)parPtr;
            return f(varName, 0xFFFFFFFF, 0);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // SEH-protected: Intersection tests read from WoW's geometry cache.
    BYTE __declspec(dllexport) __stdcall Intersect(XYZXYZ* points, float* distance, Intersection* intersection, unsigned int flags, unsigned int ptr)
    {
        __try
        {
            typedef BYTE __fastcall func(struct XYZXYZ* addrPoints, float* addrDistance, struct Intersection* addrIntersection, unsigned int flags);
            func* f = (func*)ptr;
            return f(points, distance, intersection, flags);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // SEH-protected: Intersection tests read from WoW's geometry cache.
    bool __declspec(dllexport) __stdcall Intersect2(XYZ* p1, XYZ* p2, XYZ* intersection, float* distance, unsigned int flags, unsigned int ptr)
    {
        __try
        {
            typedef bool __fastcall func(XYZ* p1, XYZ* p2, int ignore, XYZ* intersection, float* distance, unsigned int flags);
            func* f = (func*)ptr;
            return f(p1, p2, 0, intersection, distance, flags);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return false;
        }
    }

    // SEH-protected: Vendor interaction can crash if NPC pointer is stale.
    int __declspec(dllexport) __stdcall SellItemByGuid(unsigned int parCount, unsigned long long parVendorGuid, unsigned long long parItemGuid, unsigned int parPtr)
    {
        __try
        {
            typedef void __fastcall func(unsigned int itemCount, unsigned int _zero, unsigned long long vendorGuid, unsigned long long itemGuid);
            func* f = (func*)parPtr;
            f(parCount, 0, parVendorGuid, parItemGuid);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // SEH-protected: Vendor interaction can crash if NPC pointer is stale.
    int __declspec(dllexport) __stdcall BuyVendorItem(int parItemIndex, int parQuantity, unsigned long long parVendorGuid, unsigned int parPtr)
    {
        __try
        {
            typedef void __fastcall func(unsigned int itemIndex, unsigned int Quantity, unsigned long long vendorGuid, int _one);
            func* f = (func*)parPtr;
            f(parItemIndex, parQuantity, parVendorGuid, 5);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // SEH-protected: Object pointer lookup can crash during zone transitions
    // when the object manager is being rebuilt.
    // Returns 0 on exception (null pointer — caller must check).
    unsigned int __declspec(dllexport) __stdcall GetObjectPtr(int parTypemask, unsigned long long parObjectGuid, int parLine, char* parFile, unsigned int parPtr)
    {
        __try
        {
            typedef unsigned int __fastcall func(int typemask, unsigned long long objectGuid, int line, char* file);
            func* f = (func*)parPtr;
            return f(parTypemask, parObjectGuid, parLine, parFile);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // ====================================================================
    // SEH-protected wrappers for WoW native functions called from .NET 8.
    // .NET 8 ignores [HandleProcessCorruptedStateExceptions], so
    // AccessViolationException CANNOT be caught. All native WoW function
    // calls MUST go through these C++ SEH wrappers.
    // ====================================================================

    // WoW's GetPlayerGuid — cdecl, no params, returns uint64 GUID.
    unsigned long long __declspec(dllexport) __stdcall GetPlayerGuidSafe(unsigned int parPtr)
    {
        __try
        {
            typedef unsigned long long __cdecl func();
            func* f = (func*)parPtr;
            return f();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's GetObjectByGuid — stdcall, single GUID param, returns object pointer.
    unsigned int __declspec(dllexport) __stdcall GetObjectPtrByGuidSafe(unsigned long long parGuid, unsigned int parPtr)
    {
        __try
        {
            typedef unsigned int __stdcall func(unsigned long long guid);
            func* f = (func*)parPtr;
            return f(parGuid);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's CGUnit_C::GetCreatureRank — thiscall(unitPtr), returns int.
    int __declspec(dllexport) __stdcall GetCreatureRankSafe(unsigned int parUnitPtr, unsigned int parFuncPtr)
    {
        __try
        {
            typedef int (__thiscall *func)(unsigned int thisPtr);
            func f = (func)parFuncPtr;
            return f(parUnitPtr);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's CGUnit_C::GetCreatureType — thiscall(unitPtr), returns int.
    int __declspec(dllexport) __stdcall GetCreatureTypeSafe(unsigned int parUnitPtr, unsigned int parFuncPtr)
    {
        __try
        {
            typedef int (__thiscall *func)(unsigned int thisPtr);
            func f = (func)parFuncPtr;
            return f(parUnitPtr);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's CGUnit_C::GetFactionReaction — thiscall(unitPtr1, unitPtr2), returns int.
    int __declspec(dllexport) __stdcall GetUnitReactionSafe(unsigned int parUnitPtr1, unsigned int parUnitPtr2, unsigned int parFuncPtr)
    {
        __try
        {
            typedef int (__thiscall *func)(unsigned int thisPtr, unsigned int otherPtr);
            func f = (func)parFuncPtr;
            return f(parUnitPtr1, parUnitPtr2);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 3; // Neutral
        }
    }

    // WoW's ItemCacheGetRow — thiscall with complex signature.
    unsigned int __declspec(dllexport) __stdcall GetItemCacheEntrySafe(
        unsigned int parBasePtr, int parItemId, unsigned int parUnknown,
        int parUnused1, int parUnused2, char parUnused3, unsigned int parFuncPtr)
    {
        __try
        {
            typedef unsigned int (__thiscall *func)(unsigned int thisPtr, int itemId,
                unsigned int unknown, int unused1, int unused2, char unused3);
            func f = (func)parFuncPtr;
            return f(parBasePtr, parItemId, parUnknown, parUnused1, parUnused2, parUnused3);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's IsSpellOnCooldown — thiscall with ref param. Returns 1 on success, 0 on exception.
    int __declspec(dllexport) __stdcall IsSpellOnCooldownSafe(
        unsigned int parCooldownPtr, int parSpellId, int parUnused1,
        int* parCooldownDuration, int parUnused2, int parUnused3, unsigned int parFuncPtr)
    {
        __try
        {
            typedef void (__thiscall *func)(unsigned int thisPtr, int spellId, int unused1,
                int* cooldownDuration, int unused2, int unused3);
            func f = (func)parFuncPtr;
            f(parCooldownPtr, parSpellId, parUnused1, parCooldownDuration, parUnused2, parUnused3);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            if (parCooldownDuration) *parCooldownDuration = 0;
            return 0;
        }
    }

    // WoW's SetTarget — stdcall(guid).
    int __declspec(dllexport) __stdcall SetTargetSafe(unsigned long long parGuid, unsigned int parFuncPtr)
    {
        __try
        {
            typedef void __stdcall func(unsigned long long guid);
            func* f = (func*)parFuncPtr;
            f(parGuid);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's SendMovementUpdate — thiscall(playerPtr, unknown, opcode, 0, 0).
    int __declspec(dllexport) __stdcall SendMovementUpdateSafe(
        unsigned int parPlayerPtr, unsigned int parUnknown, int parOpCode,
        int parUnused1, int parUnused2, unsigned int parFuncPtr)
    {
        __try
        {
            typedef void (__thiscall *func)(unsigned int thisPtr, unsigned int unknown,
                int opcode, int unused1, int unused2);
            func f = (func)parFuncPtr;
            f(parPlayerPtr, parUnknown, parOpCode, parUnused1, parUnused2);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's SetControlBit — thiscall(device, bit, state, tickCount).
    int __declspec(dllexport) __stdcall SetControlBitSafe(
        unsigned int parDevice, int parBit, int parState, int parTickCount, unsigned int parFuncPtr)
    {
        __try
        {
            typedef void (__thiscall *func)(unsigned int thisPtr, int bit, int state, int tickCount);
            func f = (func)parFuncPtr;
            f(parDevice, parBit, parState, parTickCount);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's SetFacing — thiscall(playerSetFacingPtr, facing).
    int __declspec(dllexport) __stdcall SetFacingSafe(unsigned int parPtr, float parFacing, unsigned int parFuncPtr)
    {
        __try
        {
            typedef void (__thiscall *func)(unsigned int thisPtr, float facing);
            func f = (func)parFuncPtr;
            f(parPtr, parFacing);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's ReleaseCorpse — thiscall(ptr).
    int __declspec(dllexport) __stdcall ReleaseCorpseSafe(unsigned int parPtr, unsigned int parFuncPtr)
    {
        __try
        {
            typedef int (__thiscall *func)(unsigned int thisPtr);
            func f = (func)parFuncPtr;
            return f(parPtr);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's RetrieveCorpse — cdecl, no params.
    int __declspec(dllexport) __stdcall RetrieveCorpseSafe(unsigned int parFuncPtr)
    {
        __try
        {
            typedef int __cdecl func();
            func* f = (func*)parFuncPtr;
            return f();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // WoW's UseItem — thiscall(itemPtr, &unused, 0).
    int __declspec(dllexport) __stdcall UseItemSafe(
        unsigned int parItemPtr, unsigned long long* parUnused, int parUnused2, unsigned int parFuncPtr)
    {
        __try
        {
            typedef void (__thiscall *func)(unsigned int thisPtr, unsigned long long* unused, int unused2);
            func f = (func)parFuncPtr;
            f(parItemPtr, parUnused, parUnused2);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // ====================================================================
    // Generic SEH-protected callback wrappers for .NET 8 managed delegates.
    //
    // .NET 8 ignores [HandleProcessCorruptedStateExceptions], so managed
    // catch(AccessViolationException) is dead code. Hook callbacks
    // (SignalEventManager, PacketLogger) that read WoW memory can hit
    // stale pointers during zone transitions. Without SEH protection, the
    // AV propagates into WoW's native call stack -> ERROR #132.
    //
    // Usage: Assembly code caves call these wrappers instead of calling
    // the managed delegate directly. Returns 1 on success, 0 on SEH.
    // ====================================================================

    // Wraps a stdcall void callback(arg1) with SEH protection.
    // Used by PacketLogger (1 arg: CDataStore*) and SignalEventNoArgs (1 arg: eventName).
    // .NET delegates default to __stdcall on x86 Windows (no [UnmanagedFunctionPointer]).
    int __declspec(dllexport) __stdcall SafeCallback1(unsigned int parCallbackPtr, unsigned int parArg1)
    {
        __try
        {
            typedef void (__stdcall *func)(unsigned int);
            func f = (func)parCallbackPtr;
            f(parArg1);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    // Wraps a stdcall void callback(arg1, arg2, arg3) with SEH protection.
    // Used by SignalEventManager (3 args: eventName, format, firstArgPtr).
    // .NET delegates default to __stdcall on x86 Windows (no [UnmanagedFunctionPointer]).
    int __declspec(dllexport) __stdcall SafeCallback3(
        unsigned int parCallbackPtr,
        unsigned int parArg1, unsigned int parArg2, unsigned int parArg3)
    {
        __try
        {
            typedef void (__stdcall *func)(unsigned int, unsigned int, unsigned int);
            func f = (func)parCallbackPtr;
            f(parArg1, parArg2, parArg3);
            return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }
}
