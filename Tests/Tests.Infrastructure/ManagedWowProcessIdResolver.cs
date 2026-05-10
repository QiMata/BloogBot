using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tests.Infrastructure;

/// <summary>
/// Resolves the WoW.exe PID for a given account by scanning StateManager
/// service output for the launch lines emitted at FG/BG client startup.
/// Used by both the legacy <c>CaptureFailureScreenshot</c> path in
/// <c>LongPathingTests</c> and the new
/// <c>LiveBakeValidationHost.CaptureMultiAngleAsync</c> screenshot path.
/// Iterates lines in reverse so the most recent launch wins after a
/// crash/relaunch cycle.
/// </summary>
public static class ManagedWowProcessIdResolver
{
    public static int? Resolve(string accountName, IEnumerable<string> stateManagerOutput)
    {
        if (string.IsNullOrWhiteSpace(accountName) || stateManagerOutput == null)
            return null;

        var escaped = Regex.Escape(accountName);
        var patterns = new[]
        {
            new Regex($@"WoW\.exe started for account\s+{escaped}\s+\(Process ID:\s*(\d+)", RegexOptions.IgnoreCase),
            new Regex($@"Added\s+{escaped}\s+to managed services with PID\s+(\d+)", RegexOptions.IgnoreCase),
        };

        foreach (var line in stateManagerOutput.Reverse())
        {
            foreach (var pattern in patterns)
            {
                var match = pattern.Match(line);
                if (match.Success
                    && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
                {
                    return pid;
                }
            }
        }
        return null;
    }
}
