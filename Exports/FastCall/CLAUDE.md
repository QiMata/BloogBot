# FastCall — x86 Calling Convention Bridge (C++)

SEH-protected wrapper DLL for calling native WoW functions from .NET 8 managed code. All functions wrapped in `__try/__except` to prevent AccessViolationException crashes from stale pointers.

## Build

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  Exports/FastCall/FastCall.vcxproj -p:Configuration=Release -p:Platform=Win32 -p:PlatformToolset=v145 -v:minimal
```

Output: `Bot/Release/net8.0/FastCall.dll`

## Files

| File | Lines | Purpose |
|------|-------|---------|
| `dllmain.cpp` | 406 | 15+ exported SEH-wrapped functions |
| `stdafx.h` | — | Precompiled header |

## Exported Functions

| Function | Calling Conv | Purpose |
|----------|-------------|---------|
| `EnumerateVisibleObjects` | thiscall | Iterate visible game objects |
| `LuaCall` | cdecl | Execute Lua code |
| `LootSlot` | thiscall | Loot specific slot |
| `GetText` | cdecl | Fetch Lua variable value |
| `Intersect` / `Intersect2` | cdecl | Ray-cast intersection test |
| `GetObjectPtr` | stdcall | Object pointer by GUID |
| `GetPlayerGuidSafe` | cdecl | Player GUID |
| `SetTargetSafe` | stdcall | Set target |
| `SendMovementUpdateSafe` | thiscall | Movement packet |
| `SetControlBitSafe` | thiscall | Input bit (forward, strafe, etc.) |
| `SetFacingSafe` | thiscall | Set player facing |
| `UseItemSafe` | thiscall | Use inventory item |
| `SellItemByGuid` / `BuyVendorItem` | stdcall | Vendor interaction |
| `ReleaseCorpseSafe` / `RetrieveCorpseSafe` | thiscall/cdecl | Corpse management |
| `IsSpellOnCooldownSafe` | thiscall | Spell cooldown check |

## Design

- Zero logic — purely passes through to WoW native addresses
- Function pointers passed as last parameter (`ptr`)
- Returns 0/false on SEH exception (caller must validate)
- Platform: Win32 (x86) only — matches WoW 1.12.1 client
