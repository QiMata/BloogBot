#include "stdafx.h"

// ====================================================================
// Crash diagnostics infrastructure
// ====================================================================

static volatile int g_diagnosticMode = 0;  // 0 = catch AVs (production), 1 = log + let crash (testing)
static char g_logPath[MAX_PATH] = {0};
static CRITICAL_SECTION g_logLock;
static volatile int g_logLockInitialized = 0;
static volatile int g_totalAVCount = 0;

static void InitLogLock()
{
    if (!g_logLockInitialized)
    {
        InitializeCriticalSection(&g_logLock);
        g_logLockInitialized = 1;
    }
}

static void LogCrash(const char* funcName, DWORD exceptionCode, void* faultAddress, void* instructionAddress)
{
    if (!g_logLockInitialized) return;

    EnterCriticalSection(&g_logLock);

    // Build log path on first use: next to WoW.exe -> WWoWLogs/fastcall_crash.log
    if (g_logPath[0] == 0)
    {
        GetModuleFileNameA(NULL, g_logPath, MAX_PATH);
        // Strip filename, go up to WoW directory
        char* lastSlash = strrchr(g_logPath, '\\');
        if (lastSlash) *(lastSlash + 1) = 0;
        // Create WWoWLogs directory
        char dirPath[MAX_PATH];
        snprintf(dirPath, MAX_PATH, "%sWWoWLogs", g_logPath);
        CreateDirectoryA(dirPath, NULL);
        snprintf(g_logPath, MAX_PATH, "%sWWoWLogs\\fastcall_crash.log", g_logPath);
    }

    FILE* f = fopen(g_logPath, "a");
    if (f)
    {
        SYSTEMTIME st;
        GetLocalTime(&st);

        int avCount = InterlockedIncrement((volatile LONG*)&g_totalAVCount);

        fprintf(f, "[%02d:%02d:%02d.%03d] SEH #%d in %s: code=0x%08X fault_addr=0x%p instr_addr=0x%p diag_mode=%d\n",
            st.wHour, st.wMinute, st.wSecond, st.wMilliseconds,
            avCount, funcName, exceptionCode, faultAddress, instructionAddress,
            g_diagnosticMode);
        fflush(f);
        fclose(f);
    }

    LeaveCriticalSection(&g_logLock);
}

// Exception filter: logs crash details, then decides whether to catch or propagate
static int CrashFilter(const char* funcName, EXCEPTION_POINTERS* ep)
{
    void* faultAddr = NULL;
    void* instrAddr = NULL;
    DWORD code = 0;

    if (ep && ep->ExceptionRecord)
    {
        code = ep->ExceptionRecord->ExceptionCode;
        instrAddr = ep->ExceptionRecord->ExceptionAddress;
        // For ACCESS_VIOLATION (0xC0000005), param[1] is the faulting address
        if (code == 0xC0000005 && ep->ExceptionRecord->NumberParameters >= 2)
            faultAddr = (void*)(ep->ExceptionRecord->ExceptionInformation[1]);
    }

    LogCrash(funcName, code, faultAddr, instrAddr);

    if (g_diagnosticMode)
        return EXCEPTION_CONTINUE_SEARCH;  // Let it crash — we want the full dump
    else
        return EXCEPTION_EXECUTE_HANDLER;   // Catch it — production behavior
}

BOOL APIENTRY DllMain(HMODULE hModule,
    DWORD  ul_reason_for_call,
    LPVOID lpReserved
)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        InitLogLock();
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        if (g_logLockInitialized)
        {
            DeleteCriticalSection(&g_logLock);
            g_logLockInitialized = 0;
        }
        break;
    }

    return TRUE;
}

extern "C"
{
    struct XYZXYZ { float X1; float Y1; float Z1; float X2; float Y2; float Z2; };
    struct Intersection { float X; float Y; float Z; float R; };
    struct XYZ { float X; float Y; float Z; };

    // ====================================================================
    // Diagnostic mode control — call from C# to enable crash-through
    // ====================================================================

    // mode=0: production (catch AVs, return error codes)
    // mode=1: diagnostic (log + let crash for dump analysis)
    void __declspec(dllexport) __stdcall SetCrashDiagnosticMode(int mode)
    {
        g_diagnosticMode = mode;

        // Log the mode change
        if (g_logLockInitialized)
        {
            EnterCriticalSection(&g_logLock);
            if (g_logPath[0] == 0)
            {
                GetModuleFileNameA(NULL, g_logPath, MAX_PATH);
                char* lastSlash = strrchr(g_logPath, '\\');
                if (lastSlash) *(lastSlash + 1) = 0;
                char dirPath[MAX_PATH];
                snprintf(dirPath, MAX_PATH, "%sWWoWLogs", g_logPath);
                CreateDirectoryA(dirPath, NULL);
                snprintf(g_logPath, MAX_PATH, "%sWWoWLogs\\fastcall_crash.log", g_logPath);
            }
            FILE* f = fopen(g_logPath, "a");
            if (f)
            {
                SYSTEMTIME st;
                GetLocalTime(&st);
                fprintf(f, "[%02d:%02d:%02d.%03d] === DIAGNOSTIC MODE %s (total AVs so far: %d) ===\n",
                    st.wHour, st.wMinute, st.wSecond, st.wMilliseconds,
                    mode ? "ENABLED — AVs will crash process" : "DISABLED — AVs will be caught",
                    g_totalAVCount);
                fflush(f);
                fclose(f);
            }
            LeaveCriticalSection(&g_logLock);
        }
    }

    // Returns the current diagnostic mode
    int __declspec(dllexport) __stdcall GetCrashDiagnosticMode()
    {
        return g_diagnosticMode;
    }

    // Returns total AV count since DLL load
    int __declspec(dllexport) __stdcall GetTotalAVCount()
    {
        return g_totalAVCount;
    }

    // ====================================================================
    // SEH-protected WoW function wrappers — now with crash logging
    // ====================================================================

    int __declspec(dllexport) __stdcall EnumerateVisibleObjects(unsigned int callback, int filter, unsigned int ptr)
    {
        __try
        {
            typedef void __fastcall func(unsigned int callback, int filter);
            func* function = (func*)ptr;
            function(callback, filter);
            return 1;
        }
        __except (CrashFilter("EnumerateVisibleObjects", GetExceptionInformation()))
        {
            return 0;
        }
    }

    int __declspec(dllexport) __stdcall LuaCall(char* code, unsigned int ptr)
    {
        __try
        {
            typedef void __fastcall func(char* code, const char* unused);
            func* f = (func*)ptr;
            f(code, "LuaCall");
            return 1;
        }
        __except (CrashFilter("LuaCall", GetExceptionInformation()))
        {
            return 0;
        }
    }

    int __declspec(dllexport) __stdcall LootSlot(int slot, unsigned int ptr)
    {
        __try
        {
            typedef void __fastcall func(unsigned int slot, int unused);
            func* f = (func*)ptr;
            f(slot, 0);
            return 1;
        }
        __except (CrashFilter("LootSlot", GetExceptionInformation()))
        {
            return 0;
        }
    }

    unsigned int __declspec(dllexport) __stdcall GetText(char* varName, unsigned int parPtr)
    {
        __try
        {
            typedef unsigned int __fastcall func(char* varName, unsigned int nonSense, int zero);
            func* f = (func*)parPtr;
            return f(varName, 0xFFFFFFFF, 0);
        }
        __except (CrashFilter("GetText", GetExceptionInformation()))
        {
            return 0;
        }
    }

    BYTE __declspec(dllexport) __stdcall Intersect(XYZXYZ* points, float* distance, Intersection* intersection, unsigned int flags, unsigned int ptr)
    {
        __try
        {
            typedef BYTE __fastcall func(struct XYZXYZ* addrPoints, float* addrDistance, struct Intersection* addrIntersection, unsigned int flags);
            func* f = (func*)ptr;
            return f(points, distance, intersection, flags);
        }
        __except (CrashFilter("Intersect", GetExceptionInformation()))
        {
            return 0;
        }
    }

    bool __declspec(dllexport) __stdcall Intersect2(XYZ* p1, XYZ* p2, XYZ* intersection, float* distance, unsigned int flags, unsigned int ptr)
    {
        __try
        {
            typedef bool __fastcall func(XYZ* p1, XYZ* p2, int ignore, XYZ* intersection, float* distance, unsigned int flags);
            func* f = (func*)ptr;
            return f(p1, p2, 0, intersection, distance, flags);
        }
        __except (CrashFilter("Intersect2", GetExceptionInformation()))
        {
            return false;
        }
    }

    int __declspec(dllexport) __stdcall SellItemByGuid(unsigned int parCount, unsigned long long parVendorGuid, unsigned long long parItemGuid, unsigned int parPtr)
    {
        __try
        {
            typedef void __fastcall func(unsigned int itemCount, unsigned int _zero, unsigned long long vendorGuid, unsigned long long itemGuid);
            func* f = (func*)parPtr;
            f(parCount, 0, parVendorGuid, parItemGuid);
            return 1;
        }
        __except (CrashFilter("SellItemByGuid", GetExceptionInformation()))
        {
            return 0;
        }
    }

    int __declspec(dllexport) __stdcall BuyVendorItem(int parItemIndex, int parQuantity, unsigned long long parVendorGuid, unsigned int parPtr)
    {
        __try
        {
            typedef void __fastcall func(unsigned int itemIndex, unsigned int Quantity, unsigned long long vendorGuid, int _one);
            func* f = (func*)parPtr;
            f(parItemIndex, parQuantity, parVendorGuid, 5);
            return 1;
        }
        __except (CrashFilter("BuyVendorItem", GetExceptionInformation()))
        {
            return 0;
        }
    }

    unsigned int __declspec(dllexport) __stdcall GetObjectPtr(int parTypemask, unsigned long long parObjectGuid, int parLine, char* parFile, unsigned int parPtr)
    {
        __try
        {
            typedef unsigned int __fastcall func(int typemask, unsigned long long objectGuid, int line, char* file);
            func* f = (func*)parPtr;
            return f(parTypemask, parObjectGuid, parLine, parFile);
        }
        __except (CrashFilter("GetObjectPtr", GetExceptionInformation()))
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

    unsigned long long __declspec(dllexport) __stdcall GetPlayerGuidSafe(unsigned int parPtr)
    {
        __try
        {
            typedef unsigned long long __cdecl func();
            func* f = (func*)parPtr;
            return f();
        }
        __except (CrashFilter("GetPlayerGuidSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

    unsigned int __declspec(dllexport) __stdcall GetObjectPtrByGuidSafe(unsigned long long parGuid, unsigned int parPtr)
    {
        __try
        {
            typedef unsigned int __stdcall func(unsigned long long guid);
            func* f = (func*)parPtr;
            return f(parGuid);
        }
        __except (CrashFilter("GetObjectPtrByGuidSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

    int __declspec(dllexport) __stdcall GetCreatureRankSafe(unsigned int parUnitPtr, unsigned int parFuncPtr)
    {
        __try
        {
            typedef int (__thiscall *func)(unsigned int thisPtr);
            func f = (func)parFuncPtr;
            return f(parUnitPtr);
        }
        __except (CrashFilter("GetCreatureRankSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

    int __declspec(dllexport) __stdcall GetCreatureTypeSafe(unsigned int parUnitPtr, unsigned int parFuncPtr)
    {
        __try
        {
            typedef int (__thiscall *func)(unsigned int thisPtr);
            func f = (func)parFuncPtr;
            return f(parUnitPtr);
        }
        __except (CrashFilter("GetCreatureTypeSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

    int __declspec(dllexport) __stdcall GetUnitReactionSafe(unsigned int parUnitPtr1, unsigned int parUnitPtr2, unsigned int parFuncPtr)
    {
        __try
        {
            typedef int (__thiscall *func)(unsigned int thisPtr, unsigned int otherPtr);
            func f = (func)parFuncPtr;
            return f(parUnitPtr1, parUnitPtr2);
        }
        __except (CrashFilter("GetUnitReactionSafe", GetExceptionInformation()))
        {
            return 3; // Neutral
        }
    }

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
        __except (CrashFilter("GetItemCacheEntrySafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

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
        __except (CrashFilter("IsSpellOnCooldownSafe", GetExceptionInformation()))
        {
            if (parCooldownDuration) *parCooldownDuration = 0;
            return 0;
        }
    }

    int __declspec(dllexport) __stdcall SetTargetSafe(unsigned long long parGuid, unsigned int parFuncPtr)
    {
        __try
        {
            typedef void __stdcall func(unsigned long long guid);
            func* f = (func*)parFuncPtr;
            f(parGuid);
            return 1;
        }
        __except (CrashFilter("SetTargetSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

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
        __except (CrashFilter("SendMovementUpdateSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

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
        __except (CrashFilter("SetControlBitSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

    int __declspec(dllexport) __stdcall SetFacingSafe(unsigned int parPtr, float parFacing, unsigned int parFuncPtr)
    {
        __try
        {
            typedef void (__thiscall *func)(unsigned int thisPtr, float facing);
            func f = (func)parFuncPtr;
            f(parPtr, parFacing);
            return 1;
        }
        __except (CrashFilter("SetFacingSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

    int __declspec(dllexport) __stdcall ReleaseCorpseSafe(unsigned int parPtr, unsigned int parFuncPtr)
    {
        __try
        {
            typedef int (__thiscall *func)(unsigned int thisPtr);
            func f = (func)parFuncPtr;
            return f(parPtr);
        }
        __except (CrashFilter("ReleaseCorpseSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

    int __declspec(dllexport) __stdcall RetrieveCorpseSafe(unsigned int parFuncPtr)
    {
        __try
        {
            typedef int __cdecl func();
            func* f = (func*)parFuncPtr;
            return f();
        }
        __except (CrashFilter("RetrieveCorpseSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

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
        __except (CrashFilter("UseItemSafe", GetExceptionInformation()))
        {
            return 0;
        }
    }

    // NOTE: ClickToMove (CTM) was removed — it does not work for ghost players,
    // breaking corpse runs. Use SetFacing + SetControlBit(Front) for all FG movement.

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

    int __declspec(dllexport) __stdcall SafeCallback1(unsigned int parCallbackPtr, unsigned int parArg1)
    {
        __try
        {
            typedef void (__stdcall *func)(unsigned int);
            func f = (func)parCallbackPtr;
            f(parArg1);
            return 1;
        }
        __except (CrashFilter("SafeCallback1", GetExceptionInformation()))
        {
            return 0;
        }
    }

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
        __except (CrashFilter("SafeCallback3", GetExceptionInformation()))
        {
            return 0;
        }
    }
}
