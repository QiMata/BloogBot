using System;
using System.Globalization;

namespace ForegroundBotRunner.Frames;

internal static class FrameLuaReader
{
    internal static bool ReadBool(Func<string, string[]> luaCallWithResult, string lua)
        => ReadInt(luaCallWithResult, lua) != 0;

    internal static int ReadInt(Func<string, string[]> luaCallWithResult, string lua)
    {
        var results = luaCallWithResult(lua);
        if (results.Length == 0)
            return 0;

        return int.TryParse(results[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }
}
