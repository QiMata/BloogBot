# Loader — .NET 8 CLR Bootstrap via DLL Injection (C++)

Bootstraps .NET 8 runtime inside WoW.exe process, loads ForegroundBotRunner.dll, and enters managed code.

## Build

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  Exports/Loader/Loader.vcxproj -p:Configuration=Release -p:Platform=Win32 -p:PlatformToolset=v145 -v:minimal
```

Output: `Bot/Release/net8.0/Loader.dll`

## Files

| File | Lines | Purpose |
|------|-------|---------|
| `dllmain.cpp` | 380 | DLL_PROCESS_ATTACH → bootstrap thread → CLR init |
| `nethost_helpers.h` | 304 | hostfxr API wrappers (find, load, initialize) |
| `BotHostControl.h` | — | Bot domain control interface |

## Injection Pipeline

```
StateManager calls CreateRemoteThread(LoadLibraryW, "Loader.dll")
  → DLL_PROCESS_ATTACH
    → _beginthreadex(ThreadMain)
      → InitializeNetHost() — find & load hostfxr.dll
      → LoadAndRunManagedCode()
        → hostfxr_initialize_for_runtime_config("ForegroundBotRunner.runtimeconfig.json")
        → load_assembly_and_get_function_pointer("ForegroundBotRunner.dll",
            "ForegroundBotRunner.Loader, ForegroundBotRunner", "Load")
        → entry_point(null, 0)
          → ForegroundBotRunner.Loader.Load() — managed code starts
```

## Environment Variables

- `WWOW_LOADER_CONSOLE` — Set to "0" or "N" to suppress console window (default: show)

## Common Errors

| Code | Meaning |
|------|---------|
| `0x80008083` | FrameworkMissingFailure — .NET 8 runtime not installed |
| `0x80131522` | TypeLoadException — wrong type name in config |
| `0x80131523` | MissingMethodException — wrong method signature |

## Shutdown

- `DLL_PROCESS_DETACH` signals shutdown event → `hostfxr_close()` → waits 5s for thread exit
- Platform: Win32 (x86) — C++17, v145 toolset
