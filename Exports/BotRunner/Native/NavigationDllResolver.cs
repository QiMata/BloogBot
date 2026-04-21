using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Serilog; // TODO: migrate to ILogger when DI is available

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

    /// <summary>
    /// Auto-register when BotRunner assembly loads via module initializer.
    /// This ensures the resolver is active for ALL consumers (BackgroundBotRunner,
    /// tests, StateManager) without requiring explicit Register() calls.
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void AutoRegister() => Register();

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
        // Handle both Navigation.dll (pathfinding) and Physics.dll (local physics)
        if (!libraryName.Equals("Navigation", StringComparison.OrdinalIgnoreCase)
            && !libraryName.Equals("Physics", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        var dllFileName = libraryName + ".dll";
        var baseDir = AppContext.BaseDirectory;
        var arch = RuntimeInformation.ProcessArchitecture;

        foreach (var candidatePath in GetCandidatePaths(baseDir, arch, dllFileName))
        {
            if (File.Exists(candidatePath) && NativeLibrary.TryLoad(candidatePath, out var handle))
            {
                Log.Information("[NavigationDllResolver] Loaded {Arch} {Library} from {Path}", arch, dllFileName, candidatePath);
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    internal static IReadOnlyList<string> GetCandidatePaths(string baseDir, Architecture arch, string dllFileName)
    {
        var rootPath = Path.Combine(baseDir, dllFileName);
        var x64Path = Path.Combine(baseDir, "x64", dllFileName);
        var x86Path = Path.Combine(baseDir, "x86", dllFileName);

        return arch switch
        {
            Architecture.X86 => [x86Path, rootPath],
            Architecture.X64 => [rootPath, x64Path],
            _ => [rootPath, x64Path, x86Path]
        };
    }
}
