using System;
using System.Collections.Generic;

namespace ForegroundBotRunner.Diagnostics;

internal static class LuaErrorDiagnostics
{
    private const int MaxReturnedErrors = 10;

    private const string InstallCaptureHandlerLua =
        "WWOW_LUA_ERROR_CAPTURE_INSTALLED = 1 " +
        "if not WWOW_LUA_ERROR_BUFFER then WWOW_LUA_ERROR_BUFFER = {} end " +
        "seterrorhandler(function(err) " +
        "  local msg = tostring(err or 'nil') " +
        "  local stamp = date('%H:%M:%S') " +
        "  local entry = tostring(stamp or '') .. '|' .. msg " +
        "  if not WWOW_LUA_ERROR_BUFFER then WWOW_LUA_ERROR_BUFFER = {} end " +
        "  table.insert(WWOW_LUA_ERROR_BUFFER, entry) " +
        "  local n = table.getn(WWOW_LUA_ERROR_BUFFER) " +
        "  if n > 50 then table.remove(WWOW_LUA_ERROR_BUFFER, 1) end " +
        "  return msg " +
        "end)";

    private const string DrainCapturedErrorsLua =
        "local q = WWOW_LUA_ERROR_BUFFER or {} " +
        "local n = table.getn(q) " +
        "if n < 1 then " +
        "  {0} = '0' " +
        "  {1} = '' " +
        "  {2} = '' " +
        "  {3} = '' " +
        "  {4} = '' " +
        "  {5} = '' " +
        "  {6} = '' " +
        "  {7} = '' " +
        "  {8} = '' " +
        "  {9} = '' " +
        "  {10} = '' " +
        "else " +
        "  local start = n - 9 " +
        "  if start < 1 then start = 1 end " +
        "  {0} = tostring(n) " +
        "  {1} = tostring(q[start] or '') " +
        "  {2} = tostring(q[start + 1] or '') " +
        "  {3} = tostring(q[start + 2] or '') " +
        "  {4} = tostring(q[start + 3] or '') " +
        "  {5} = tostring(q[start + 4] or '') " +
        "  {6} = tostring(q[start + 5] or '') " +
        "  {7} = tostring(q[start + 6] or '') " +
        "  {8} = tostring(q[start + 7] or '') " +
        "  {9} = tostring(q[start + 8] or '') " +
        "  {10} = tostring(q[start + 9] or '') " +
        "  WWOW_LUA_ERROR_BUFFER = {} " +
        "end";

    public static void InstallCaptureHandler(Action<string> luaCall)
    {
        ArgumentNullException.ThrowIfNull(luaCall);
        luaCall(InstallCaptureHandlerLua);
    }

    public static IReadOnlyList<string> DrainCapturedErrors(Func<string, string[]> luaCallWithResult)
    {
        ArgumentNullException.ThrowIfNull(luaCallWithResult);

        var results = luaCallWithResult(DrainCapturedErrorsLua);
        if (results is null || results.Length == 0)
            return Array.Empty<string>();

        if (!int.TryParse(results[0], out var totalBuffered) || totalBuffered <= 0)
            return Array.Empty<string>();

        var drained = new List<string>(Math.Min(MaxReturnedErrors, totalBuffered));
        var upperBound = Math.Min(results.Length, MaxReturnedErrors + 1);
        for (var i = 1; i < upperBound; i++)
        {
            var candidate = results[i]?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            drained.Add(candidate);
        }

        return drained;
    }
}
