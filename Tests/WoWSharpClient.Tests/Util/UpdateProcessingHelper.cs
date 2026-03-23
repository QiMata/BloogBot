using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WoWSharpClient.Tests.Util
{
    /// <summary>
    /// Deterministic helper for draining the WoWSharpObjectManager pending update queue
    /// in tests. Replaces fixed Thread.Sleep calls with a polling approach that waits
    /// until the object count stabilizes, guaranteeing that all queued updates have been
    /// processed before assertions run.
    /// </summary>
    internal static class UpdateProcessingHelper
    {
        /// <summary>
        /// Kicks off ProcessUpdatesAsync and polls until both the pending update queue
        /// and object count remain stable for multiple intervals. This avoids cutting
        /// off movement-only updates, which do not change object count.
        /// </summary>
        /// <param name="maxWaitMs">Maximum time to wait before giving up (default 5000ms).</param>
        /// <param name="stabilityIntervalMs">How long the count must remain stable to consider processing complete (default 50ms).</param>
        public static void DrainPendingUpdates(int maxWaitMs = 5000, int stabilityIntervalMs = 50)
        {
            using var cts = new CancellationTokenSource();
            var processTask = WoWSharpObjectManager.Instance.ProcessUpdatesAsync(cts.Token);

            var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            int lastCount = WoWSharpObjectManager.Instance.Objects.Count();
            int lastPendingCount = WoWSharpObjectManager.Instance.PendingUpdateCount;
            int stableChecks = 0;
            const int requiredStableChecks = 3;

            // Poll until both object count and pending queue stabilize or timeout.
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(stabilityIntervalMs);

                int currentCount = WoWSharpObjectManager.Instance.Objects.Count();
                int currentPendingCount = WoWSharpObjectManager.Instance.PendingUpdateCount;
                if (currentCount == lastCount && currentPendingCount == 0 && lastPendingCount == 0)
                {
                    stableChecks++;
                    if (stableChecks >= requiredStableChecks)
                        break;
                }
                else
                {
                    stableChecks = 0;
                    lastCount = currentCount;
                    lastPendingCount = currentPendingCount;
                }
            }

            // Cancel the background loop
            cts.Cancel();

            // Allow the cancelled task to complete gracefully
            try { processTask.Wait(500); } catch (AggregateException) { }
        }
    }
}
