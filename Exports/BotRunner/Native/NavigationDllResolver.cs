using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Serilog;

namespace BotRunner.Native;

/// <summary>
/// Resolves Navigation.dll from platform-specific subdirectory.
///
/// Problem: StateManager=x86 → BackgroundBotRunner=x86 → needs x86 Navigation.dll.
///          PhysicsTests=x64 → needs x64 Navigation.dll.
///          Both output to Bot/Release/net8.0/ — conflict.
///
/// Solution: x86 build goes to Bot/Release/net8.0/x86/Navigation.dll.
///           x64 build goes to Bot/Release/net8.0/Navigation.dll (default).
///           This resolver loads from the correct path based on process architecture.
///
/// Registration: Call NavigationDllResolver.Register() once at startup.
/// </summary>
public static class NavigationDllResolver
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        // Register for BotRunner assembly (PathfindingClient P/Invoke)
        NativeLibrary.SetDllImportResolver(
            typeof(NavigationDllResolver).Assembly,
            ResolveNavigationDll);

        // Register for WoWSharpClient assembly (NativePhysicsInterop P/Invoke)
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "WoWSharpClient")
            {
                try { NativeLibrary.SetDllImportResolver(asm, ResolveNavigationDll); }
                catch { /* Already registered or not applicable */ }
                break;
            }
        }

        // Handle late-loaded assemblies
        AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
        {
            if (args.LoadedAssembly.GetName().Name == "WoWSharpClient")
            {
                try { NativeLibrary.SetDllImportResolver(args.LoadedAssembly, ResolveNavigationDll); }
                catch { /* Already registered */ }
            }
        };
    }

    private static IntPtr ResolveNavigationDll(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals("Navigation", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        var baseDir = AppContext.BaseDirectory;
        var arch = RuntimeInformation.ProcessArchitecture;

        // Try platform-specific subdirectory first (x86/ or x64/)
        var subdir = arch == Architecture.X86 ? "x86" : "x64";
        var platformPath = Path.Combine(baseDir, subdir, "Navigation.dll");

        if (File.Exists(platformPath) && NativeLibrary.TryLoad(platformPath, out var handle))
        {
            Log.Information("[NavigationDllResolver] Loaded {Arch} from {Path}", arch, platformPath);
            return handle;
        }

        // Fall back to default location
        var defaultPath = Path.Combine(baseDir, "Navigation.dll");
        if (File.Exists(defaultPath) && NativeLibrary.TryLoad(defaultPath, out handle))
        {
            Log.Information("[NavigationDllResolver] Loaded from default {Path}", defaultPath);
            return handle;
        }

        return IntPtr.Zero;
    }
}
