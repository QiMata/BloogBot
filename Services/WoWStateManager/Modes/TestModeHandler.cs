using System;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Microsoft.Extensions.Logging;
using WoWStateManager.Settings;

namespace WoWStateManager.Modes
{
    /// <summary>
    /// No-op handler for <see cref="StateManagerMode.Test"/>. Preserves the
    /// legacy behavior in which test fixtures drive every action explicitly
    /// via <c>SendActionAsync</c>; StateManager itself does not auto-dispatch.
    /// </summary>
    public sealed class TestModeHandler : IStateManagerModeHandler
    {
        private readonly ILogger<TestModeHandler> _logger;

        public TestModeHandler(ILogger<TestModeHandler> logger)
        {
            _logger = logger;
        }

        public StateManagerMode Mode => StateManagerMode.Test;

        public Task OnWorldEntryAsync(
            CharacterSettings character,
            Func<string, ActionMessage, bool> enqueueAction,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnSnapshotAsync(
            CharacterSettings character,
            WoWActivitySnapshot snapshot,
            Func<string, ActionMessage, bool> enqueueAction,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task OnExternalActivityRequestAsync(
            string requestingPlayer,
            string activityDescriptor,
            Func<string, ActionMessage, bool> enqueueAction,
            CancellationToken cancellationToken)
        {
            _logger.LogWarning(
                "[MODE=Test] Rejecting external activity request from '{Player}' for '{Descriptor}': " +
                "Test mode does not service on-demand requests.",
                requestingPlayer,
                activityDescriptor);
            throw new InvalidOperationException(
                $"StateManager is running in Test mode and cannot handle external activity requests " +
                $"(requestingPlayer='{requestingPlayer}', descriptor='{activityDescriptor}').");
        }
    }
}
