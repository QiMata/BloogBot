using System;
using System.Runtime.InteropServices;

namespace PathfindingService;

internal static class NativeProcessEnvironment
{
    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int _putenv(string envString);

    public static void Set(string key, string value)
    {
        Environment.SetEnvironmentVariable(key, value);
        try
        {
            _ = _putenv($"{key}={value}");
        }
        catch
        {
            // Best-effort only; managed environment is already set above.
        }
    }
}
