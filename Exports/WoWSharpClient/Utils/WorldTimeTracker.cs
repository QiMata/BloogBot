using System;
using System.Threading;

namespace WoWSharpClient.Utils
{
    public class WorldTimeTracker
    {
        private readonly Func<long> _tickCountProvider;
        private long _timeOffsetMs;

        public WorldTimeTracker(Func<long>? tickCountProvider = null)
        {
            _tickCountProvider = tickCountProvider ?? (() => Environment.TickCount64);
        }

        /// <summary>
        /// Returns the current monotonic time in ms using the same time base
        /// as MaNGOS's WorldTimer::getMSTime(), which calls GetTickCount().
        /// Environment.TickCount64 wraps the same Win32 API, and
        /// MSG_MOVE_TIME_SKIPPED advances the same movement-time base by a
        /// packet-specified offset (WoW.exe 0x61AB90).
        /// </summary>
        public TimeSpan NowMS => TimeSpan.FromMilliseconds(_tickCountProvider() + Interlocked.Read(ref _timeOffsetMs));

        public void AdvanceBy(uint deltaMs) => Interlocked.Add(ref _timeOffsetMs, deltaMs);
    }
}
