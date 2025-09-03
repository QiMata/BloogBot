# FastCall

## Overview

FastCall is a native C++ DLL project that provides a bridge between managed .NET code and unmanaged game client functions for the BloogBot World of Warcraft automation framework. It serves as a critical interop layer that enables the bot to interact with the game client's internal functions through fast calling conventions.

## Purpose

The FastCall DLL exports wrapper functions that:
- Enable calling game client functions with proper calling conventions (__fastcall, __stdcall)
- Provide a stable interface for interacting with WoW client internals
- Handle memory marshaling between managed and unmanaged code
- Support various game operations like Lua execution, object interaction, and vendor transactions

## Project Structure

```
Exports/FastCall/
??? FastCall.vcxproj     # Visual Studio C++ project file
??? dllmain.cpp          # Main DLL implementation with exported functions
??? stdafx.h             # Precompiled header with Windows includes
??? stdafx.cpp           # Precompiled header implementation
??? targetver.h          # Windows target version definitions
??? README.md            # This documentation
```

## Build Configuration

- **Project Type**: Dynamic Library (DLL)
- **Platform Toolset**: v143 (Visual Studio 2022)
- **Language Standard**: C++14
- **Character Set**: Unicode
- **Output Directory**: `..\..\Bot\$(Configuration)\net8.0`
- **Supported Platforms**: Win32, x64
- **Configurations**: Debug, Release

## Exported Functions

The DLL exports the following functions used by the bot's managed code:

### Lua Execution
- **`LuaCall`**: Executes Lua code within the game client
  - Parameters: `char* code`, `unsigned int ptr`
  - Used for: Game commands, UI interactions, spell casting

### Object Enumeration
- **`EnumerateVisibleObjects`**: Enumerates visible game objects
  - Parameters: `unsigned int callback`, `int filter`, `unsigned int ptr`
  - Used for: Finding targets, NPCs, and interactive objects

### Item and Inventory Management
- **`LootSlot`**: Loots items from a specific slot
  - Parameters: `int slot`, `unsigned int ptr`
  - Used for: Automated looting after combat

- **`SellItemByGuid`**: Sells items to vendors
  - Parameters: `unsigned int itemCount`, `unsigned long long vendorGuid`, `unsigned long long itemGuid`, `unsigned int ptr`
  - Used for: Automated vendor interactions

- **`BuyVendorItem`**: Purchases items from vendors
  - Parameters: `int itemIndex`, `int quantity`, `unsigned long long vendorGuid`, `unsigned int ptr`
  - Used for: Buying consumables and equipment

### Text and Variable Access
- **`GetText`**: Retrieves text values from game variables
  - Parameters: `char* varName`, `unsigned int ptr`
  - Used for: Reading Lua variable values

### Spatial Operations
- **`Intersect`**: Performs 3D intersection testing
  - Parameters: `XYZXYZ* points`, `float* distance`, `Intersection* intersection`, `unsigned int flags`, `unsigned int ptr`
  - Used for: Collision detection and pathfinding

- **`Intersect2`**: Alternative intersection testing
  - Parameters: `XYZ* p1`, `XYZ* p2`, `XYZ* intersection`, `float* distance`, `unsigned int flags`, `unsigned int ptr`
  - Used for: Line-of-sight calculations

### Object Management
- **`GetObjectPtr`**: Retrieves object pointers by GUID
  - Parameters: `int typemask`, `unsigned long long objectGuid`, `int line`, `char* file`, `unsigned int ptr`
  - Used for: Object reference management

## Data Structures

The DLL defines several structures for 3D operations:

```cpp
struct XYZXYZ { 
    float X1; float Y1; float Z1; 
    float X2; float Y2; float Z2; 
};

struct Intersection { 
    float X; float Y; float Z; float R; 
};

struct XYZ { 
    float X; float Y; float Z; 
};
```

## Integration with .NET Code

The FastCall DLL is consumed by the `Functions` class in `Services/ForegroundBotRunner/Mem/Functions.cs`, which provides managed wrappers using P/Invoke declarations:

```csharp
[DllImport("FastCall.dll", EntryPoint = "LuaCall")]
private static extern void LuaCallFunction(string code, int ptr);

[DllImport("FastCall.dll", EntryPoint = "LootSlot")]
private static extern byte LootSlotFunction(int slot, nint ptr);
```

## Usage in Bot Framework

The FastCall functions are used throughout the bot for:

1. **Combat Operations**: Casting spells, targeting enemies
2. **Movement**: Pathfinding and collision detection
3. **Inventory Management**: Looting, selling, buying items
4. **Game State Queries**: Reading player status, object information
5. **UI Interaction**: Executing Lua commands for game interface

## Dependencies

- **Windows SDK**: Required for Windows API headers
- **Visual C++ Runtime**: For DLL runtime support
- **Game Client**: Functions operate on memory addresses within the WoW client process

## Build Instructions

1. Open the solution in Visual Studio 2022 or compatible
2. Ensure the Windows 10 SDK is installed
3. Build the project for the desired platform (Win32/x64)
4. The output DLL will be placed in the Bot output directory
5. Ensure the DLL is available to the .NET application at runtime

## Thread Safety

The DLL functions are designed to be called from the game client's main thread. The calling .NET code handles thread synchronization to ensure proper execution context.

## Performance Considerations

- Functions use optimized calling conventions (__fastcall, __stdcall) for performance
- Minimal parameter marshaling to reduce overhead
- Direct memory access patterns for efficiency
- Compiled with optimization flags in Release builds

## Maintenance Notes

- Memory addresses are managed by the calling .NET code via `MemoryAddresses` class
- Function pointers are resolved at runtime by the managed layer
- No direct game client dependencies in the DLL itself
- Functions act as thin wrappers around game client calls

## Version Compatibility

This DLL is designed for specific versions of the World of Warcraft client. Memory addresses and function signatures may need updates for different client versions or patches.