using System;
namespace WoWSharpClient.Utils
{
    public class WorldTimeTracker
    {
        /// <summary>
        /// Returns the current monotonic time in ms using the same time base
        /// as MaNGOS's WorldTimer::getMSTime(), which calls GetTickCount().
        /// Environment.TickCount64 wraps the same Win32 API, so packet
        /// timestamps will be consistent with server-side expectations.
        /// </summary>
        public TimeSpan NowMS => TimeSpan.FromMilliseconds(Environment.TickCount64);
    }
}

