# FastCall - x86 Calling Convention Helper DLL

## Overview

**FastCall** is a native C++ dynamic-link library (DLL) that provides wrapper functions for invoking game APIs that use calling conventions incompatible with .NET's standard P/Invoke mechanisms. Specifically, it bridges the gap between managed C# code and native x86 `__fastcall` convention functions used in legacy game clients.

This component is essential for the **ForegroundBotRunner** when targeting the **Vanilla (1.12.1)** client, where many internal game functions use the `__fastcall` convention that passes the first two arguments in CPU registers (ECX and EDX) rather than on the stack.

## Purpose

The FastCall DLL solves a fundamental interoperability problem:

1. **The Problem**: .NET's `Marshal.GetDelegateForFunctionPointer` cannot properly handle true x86 `__fastcall` calling conventions where arguments are passed in registers
2. **The Solution**: Export `__stdcall` functions from a native DLL that accept the target function pointer as a parameter, then internally call the target using the correct `__fastcall` convention

This allows managed C# code to call game functions indirectly through FastCall, which handles the register setup and calling convention translation.

## Architecture

```
???????????????????????????????????????????????????????????????????????????
?                        Game Process (WoW.exe)                           ?
???????????????????????????????????????????????????????????????????????????
?                                                                         ?
?   ????????????????????     ????????????????     ????????????????????   ?
?   ?  Managed Code    ??????? FastCall.dll ???????  Game Function   ?   ?
?   ?  (C# P/Invoke)   ?     ?  (stdcall)   ?     ?  (__fastcall)    ?   ?
?   ?                  ?     ?              ?     ?                  ?   ?
?   ?  [DllImport(...)]?     ? ECX ? arg1   ?     ? void __fastcall  ?   ?
?   ?  void Func(...)  ?     ? EDX ? arg2   ?     ? GameFunc(a,b,...)?   ?
?   ????????????????????     ? call ptr     ?     ????????????????????   ?
?                            ????????????????                             ?
?                                                                         ?
???????????????????????????????????????????????????????????????????????????
```

## Technical Details

### Build Configuration

| Property | Value |
|----------|-------|
| Project Type | Dynamic Library (DLL) |
| Platform Toolset | v143 (Visual Studio 2022) |
| Character Set | Unicode |
| C++ Standard | C++14 |
| Target Platforms | Win32 (primary), x64 |
| Precompiled Headers | Yes (`stdafx.h`) |

### Output Locations

| Configuration | Output Path |
|---------------|-------------|
| Debug (Win32) | `..\..\Bot\Debug\net8.0\FastCall.dll` |
| Release (Win32) | `..\Bot\Release\net8.0\FastCall.dll` |

### x86 Calling Convention Reference

| Convention | First Arg | Second Arg | Stack Cleanup | Used By |
|------------|-----------|------------|---------------|---------|
| `__cdecl` | Stack | Stack | Caller | Standard C |
| `__stdcall` | Stack | Stack | Callee | Win32 API, P/Invoke default |
| `__fastcall` | ECX | EDX | Callee | Vanilla WoW internal functions |
| `__thiscall` | ECX (this) | Stack | Callee | C++ member functions |

## Exported Functions

All exports use `__stdcall` convention for easy P/Invoke from C#:

### `EnumerateVisibleObjects`

Enumerates all visible game objects in the world.

```cpp
void __stdcall EnumerateVisibleObjects(
    unsigned int callback,  // Callback function pointer
    int filter,             // Object type filter
    unsigned int ptr        // Game function address
);
```

**Usage**: Called to populate the object manager with game entities (players, NPCs, items, etc.)

### `LuaCall`

Executes Lua script in the game's Lua environment.

```cpp
void __stdcall LuaCall(
    char* code,             // Lua script to execute
    unsigned int ptr        // Game function address
);
```

**Usage**: Executes arbitrary Lua commands for game actions not directly accessible via memory

### `GetText`

Retrieves the value of a Lua global variable as a string.

```cpp
unsigned int __stdcall GetText(
    char* varName,          // Lua variable name
    unsigned int parPtr     // Game function address
);
// Returns: Pointer to string value
```

**Usage**: Used with `LuaCallWithResult` pattern to get return values from Lua calls

### `LootSlot`

Loots an item from the specified loot window slot.

```cpp
void __stdcall LootSlot(
    int slot,               // Slot index (0-based)
    unsigned int ptr        // Game function address
);
```

### `Intersect` / `Intersect2`

Performs ray-cast intersection tests against world geometry.

```cpp
BYTE __stdcall Intersect(
    XYZXYZ* points,         // Start and end points
    float* distance,        // Output: distance to intersection
    Intersection* result,   // Output: intersection point and radius
    unsigned int flags,     // Intersection flags
    unsigned int ptr        // Game function address
);

bool __stdcall Intersect2(
    XYZ* p1,                // Start point
    XYZ* p2,                // End point
    XYZ* intersection,      // Output: intersection point
    float* distance,        // Output: distance
    unsigned int flags,     // Intersection flags
    unsigned int ptr        // Game function address
);
```

**Usage**: Line-of-sight checks, pathfinding obstacle detection

### `SellItemByGuid`

Sells an item to a vendor.

```cpp
void __stdcall SellItemByGuid(
    unsigned int parCount,          // Quantity to sell
    unsigned long long parVendorGuid,  // Vendor GUID
    unsigned long long parItemGuid,    // Item GUID
    unsigned int parPtr             // Game function address
);
```

### `BuyVendorItem`

Purchases an item from a vendor.

```cpp
void __stdcall BuyVendorItem(
    int parItemIndex,               // Vendor item index
    int parQuantity,                // Quantity to buy
    unsigned long long parVendorGuid,  // Vendor GUID
    unsigned int parPtr             // Game function address
);
```

### `GetObjectPtr`

Gets the memory pointer for a game object by GUID.

```cpp
unsigned int __stdcall GetObjectPtr(
    int parTypemask,                // Object type mask
    unsigned long long parObjectGuid,  // Object GUID
    int parLine,                    // Debug: source line
    char* parFile,                  // Debug: source file
    unsigned int parPtr             // Game function address
);
// Returns: Object memory pointer
```

## Data Structures

```cpp
// 3D coordinate
struct XYZ { 
    float X; 
    float Y; 
    float Z; 
};

// Ray (two 3D points)
struct XYZXYZ { 
    float X1; float Y1; float Z1;  // Start point
    float X2; float Y2; float Z2;  // End point
};

// Intersection result
struct Intersection { 
    float X; float Y; float Z;     // Intersection point
    float R;                       // Radius/distance
};
```

## C# Integration

### P/Invoke Declarations

```csharp
// In ForegroundBotRunner/Mem/Functions.cs

[DllImport("FastCall.dll", EntryPoint = "BuyVendorItem")]
private static extern void BuyVendorItemFunction(
    int itemId, 
    int quantity, 
    ulong vendorGuid, 
    nint ptr);

[DllImport("FastCall.dll", EntryPoint = "EnumerateVisibleObjects")]
private static extern void EnumerateVisibleObjectsFunction(
    nint callback, 
    int filter, 
    nint ptr);

[DllImport("FastCall.dll", EntryPoint = "LuaCall")]
private static extern void LuaCallFunction(
    string code, 
    int ptr);

[DllImport("FastCall.dll", EntryPoint = "GetText")]
private static extern nint GetTextFunction(
    string varName, 
    nint ptr);
```

### Wrapper Pattern

The managed code wraps these imports to hide the function pointer parameter:

```csharp
public static void BuyVendorItem(ulong vendorGuid, int itemId, int quantity)
{
    BuyVendorItemFunction(itemId, quantity, vendorGuid, 
        MemoryAddresses.BuyVendorItemFunPtr);
}

public static void LuaCall(string code)
{
    lock (locker)
    {
        LuaCallFunction(code, MemoryAddresses.LuaCallFunPtr);
    }
}
```

### LuaCallWithResult Pattern

A common pattern for getting values back from Lua:

```csharp
public static string[] LuaCallWithResult(string code)
{
    // Replace placeholders {0}, {1}, etc. with random variable names
    var luaVarNames = new List<string>();
    for (var i = 0; i < 11; i++)
    {
        var placeholder = "{" + i + "}";
        if (!code.Contains(placeholder)) break;
        var randomName = GetRandomLuaVarName();
        code = code.Replace(placeholder, randomName);
        luaVarNames.Add(randomName);
    }

    // Execute the Lua code
    LuaCall(code);

    // Read back the results via GetText
    var results = new List<string>();
    foreach (var varName in luaVarNames)
    {
        var address = GetText(varName);
        results.Add(MemoryManager.ReadString(address));
    }

    return results.ToArray();
}

// Usage:
var results = Functions.LuaCallWithResult("{0} = UnitHealth('player')");
int health = int.Parse(results[0]);
```

## File Structure

```
Exports/FastCall/
??? FastCall.vcxproj           # Visual Studio C++ project file
??? FastCall.vcxproj.filters   # Project file organization
??? dllmain.cpp                # Main source file with exports
??? stdafx.h                   # Precompiled header
??? stdafx.cpp                 # Precompiled header source
??? targetver.h                # Windows SDK targeting
??? README.md                  # This documentation
```

## Building

### Prerequisites

- Visual Studio 2022 with C++ Desktop Development workload
- Windows 10/11 SDK

### Build Steps

1. Open `BloogBot.sln` in Visual Studio 2022
2. Select **Win32** platform (required for x86 `__fastcall` support)
3. Select desired configuration (Debug/Release)
4. Build the FastCall project: **Build ? Build FastCall**
5. Output DLL will be in the configured output directory

### Important Notes

- **Must be built for Win32 (x86)** - The `__fastcall` calling convention is x86-specific
- FastCall.dll must be deployed alongside the managed bot assembly
- The DLL is typically embedded as a resource in ForegroundBotRunner

## Version Compatibility

| Client Version | FastCall Required | Notes |
|----------------|-------------------|-------|
| Vanilla (1.12.1) | **Yes** | Primary target, most functions use `__fastcall` |
| TBC (2.4.3) | Partial | Some functions migrated to `__thiscall` |
| WotLK (3.3.5a) | Minimal | Most calls work via direct delegates |

For TBC and WotLK clients, the bot increasingly uses `Marshal.GetDelegateForFunctionPointer` with properly attributed delegates, reducing reliance on FastCall.

## Why Not Use Inline Assembly in C#?

While .NET does have some support for unmanaged code via `unsafe` blocks, it cannot:

1. Directly manipulate CPU registers (ECX, EDX)
2. Use arbitrary calling conventions
3. Execute inline assembly

FastCall.dll provides a clean, maintainable solution that:
- Keeps platform-specific code isolated in native C++
- Exposes a simple `__stdcall` interface to managed code
- Handles all register manipulation internally

## Troubleshooting

### "Entry point not found" Error
- Verify FastCall.dll is in the same directory as the managed assembly
- Check that the entry point name matches exactly (case-sensitive)
- Ensure the DLL was built for the correct platform (Win32)

### Game Crashes When Calling Functions
- Verify the function pointer address is correct for the client version
- Check that all parameters match the expected types and order
- Ensure the calling convention in the typedef matches the game function

### "Bad image format" Exception
- The DLL bitness (32-bit) must match the target process
- Cannot load 32-bit FastCall.dll in a 64-bit process

## Related Components

| Component | Relationship |
|-----------|--------------|
| **ForegroundBotRunner** | Primary consumer via P/Invoke |
| **Loader.dll** | Loads FastCall.dll into the game process |
| **MemoryAddresses.cs** | Provides function pointer addresses |
| **Functions.cs** | Contains P/Invoke declarations and wrappers |

## Security Considerations

?? **Important**: This component is designed for legitimate use cases such as:
- Game modding on private servers
- Reverse engineering research
- Educational purposes

**Do not use** this component to:
- Violate software terms of service
- Gain unfair advantages in competitive environments
- Access or modify systems without authorization

## Version History

| Version | Changes |
|---------|---------|
| 1.0 | Initial implementation for Vanilla client |
| 1.1 | Added Intersect/Intersect2 for line-of-sight |
| 1.2 | Added vendor interaction functions |
| 1.3 | Updated to VS2022 toolset (v143) |

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform. See [ARCHITECTURE.md](../../ARCHITECTURE.md) for system-wide documentation.*
