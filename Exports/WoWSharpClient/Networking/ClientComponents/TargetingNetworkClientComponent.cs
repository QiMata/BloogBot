using System.Reactive.Linq;
using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.Helpers;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of targeting network agent that handles target selection operations in World of Warcraft.
    /// Manages target selection and assist functionality using the Mangos protocol.
    /// This agent focuses solely on targeting without combat functionality.
    /// Uses reactive observables for better composability and filtering.
    /// </summary>
    public class TargetingNetworkClientComponent : NetworkClientComponent, ITargetingNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<TargetingNetworkClientComponent> _logger;
        private ulong? _currentTarget;

        // Local subjects for emitting events triggered by client-side actions/handlers
        private readonly Subject<TargetingData> _localTargetChanges = new();
        private readonly Subject<AssistData> _localAssistOperations = new();
        private readonly Subject<TargetingErrorData> _localTargetingErrors = new();

        // Exposed reactive observables (merged from opcode-backed streams and local subjects)
        private readonly IObservable<TargetingData> _targetChanges;
        private readonly IObservable<AssistData> _assistOperations;
        private readonly IObservable<TargetingErrorData> _targetingErrors;

        // Legacy callback manager for backwards compatibility
        private readonly CallbackManager<ulong?> _targetChangedCallbackManager = new();

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the TargetingNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public TargetingNetworkClientComponent(IWorldClient worldClient, ILogger<TargetingNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Server-driven streams (best-effort) built from opcode handlers
            // Note: Protocols differ between cores; we conservatively parse 8-byte GUID from payload start when present.
            var serverTargetUpdates = SafeOpcodeStream(Opcode.SMSG_CLIENT_CONTROL_UPDATE)
                .Select(ParseServerTargetChanged)
                .Do(UpdateTargetFromServer)
                .Publish()
                .RefCount();

            // If there are specific server targeting error opcodes in your core, wire them here similarly.
            var serverTargetErrors = Observable.Empty<TargetingErrorData>();

            // If assist has a corresponding server opcode in your core, wire and parse it here.
            var serverAssistOps = Observable.Empty<AssistData>();

            // Merge server streams with local subjects and publish/refcount for multicast behavior consistent with other components
            _targetChanges = Observable.Merge(serverTargetUpdates, _localTargetChanges).Publish().RefCount();
            _assistOperations = Observable.Merge(serverAssistOps, _localAssistOperations).Publish().RefCount();
            _targetingErrors = Observable.Merge(serverTargetErrors, _localTargetingErrors).Publish().RefCount();
        }

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        #region Properties

        /// <inheritdoc />
        public ulong? CurrentTarget => _currentTarget;

        #endregion

        #region Reactive Observables

        /// <inheritdoc />
        public IObservable<TargetingData> TargetChanges => _targetChanges;

        /// <inheritdoc />
        public IObservable<AssistData> AssistOperations => _assistOperations;

        /// <inheritdoc />
        public IObservable<TargetingErrorData> TargetingErrors => _targetingErrors;

        #endregion

        #region Operations

        /// <inheritdoc />
        public async Task SetTargetAsync(ulong targetGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TargetingNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Setting target to GUID: {TargetGuid:X}", targetGuid);

                // Create packet payload with target GUID
                var payload = new byte[8];
                BitConverter.GetBytes(targetGuid).CopyTo(payload, 0);

                // Send CMSG_SET_SELECTION packet
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SET_SELECTION, payload, cancellationToken);

                // Update internal state optimistically; server stream will also update when confirmed
                var previousTarget = _currentTarget;
                _currentTarget = targetGuid == 0 ? null : targetGuid;

                // Emit local change if target actually changed
                if (previousTarget != _currentTarget)
                {
                    _logger.LogInformation("Target changed from {PreviousTarget:X} to {NewTarget:X}",
                        previousTarget ?? 0, _currentTarget ?? 0);

                    var targetingData = new TargetingData(previousTarget, _currentTarget, DateTime.UtcNow);
                    _localTargetChanges.OnNext(targetingData);

                    // Legacy callback support
                    _targetChangedCallbackManager.InvokeCallbacks(_currentTarget);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set target to {TargetGuid:X}", targetGuid);

                var errorData = new TargetingErrorData(ex.Message, targetGuid, DateTime.UtcNow);
                _localTargetingErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task ClearTargetAsync(CancellationToken cancellationToken = default)
        {
            await SetTargetAsync(0, cancellationToken);
        }

        /// <inheritdoc />
        public async Task AssistAsync(ulong playerGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TargetingNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Assisting player: {PlayerGuid:X}", playerGuid);

                // Target the player we want to assist
                await SetTargetAsync(playerGuid, cancellationToken);

                // Small delay to ensure selection is processed
                await Task.Delay(100, cancellationToken);

                // The assist functionality works by targeting the player first,
                // then the server will automatically switch our target to whatever they're targeting
                // This is handled server-side in Mangos when we target a friendly player

                _logger.LogInformation("Assist command sent for player: {PlayerGuid:X}", playerGuid);

                var assistData = new AssistData(playerGuid, _currentTarget, DateTime.UtcNow);
                _localAssistOperations.OnNext(assistData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assist player: {PlayerGuid:X}", playerGuid);

                var errorData = new TargetingErrorData(ex.Message, playerGuid, DateTime.UtcNow);
                _localTargetingErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        #region Utility Methods

        /// <inheritdoc />
        public bool IsTargeted(ulong guid)
        {
            return _currentTarget.HasValue && _currentTarget.Value == guid;
        }

        /// <inheritdoc />
        public bool HasTarget()
        {
            return _currentTarget.HasValue;
        }

        #endregion

        #region Server Response Handling

        private TargetingData ParseServerTargetChanged(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                ulong newTargetGuid = span.Length >= 8 ? BitConverter.ToUInt64(span[..8]) : 0UL;
                ulong? newTarget = newTargetGuid == 0 ? null : newTargetGuid;
                var targetingData = new TargetingData(_currentTarget, newTarget, DateTime.UtcNow);
                return targetingData;
            }
            catch
            {
                // If parsing fails, emit a no-op change (previous = current, new = current)
                return new TargetingData(_currentTarget, _currentTarget, DateTime.UtcNow);
            }
        }

        private void UpdateTargetFromServer(TargetingData targetingData)
        {
            if (_disposed) return;

            if (_currentTarget != targetingData.CurrentTarget)
            {
                var previousTarget = _currentTarget;
                _currentTarget = targetingData.CurrentTarget;

                _logger.LogDebug("Server updated target from {PreviousTarget:X} to {NewTarget:X}",
                    previousTarget ?? 0, _currentTarget ?? 0);

                // Legacy callback support
                _targetChangedCallbackManager.InvokeCallbacks(_currentTarget);
            }
        }

        /// <inheritdoc />
        public void HandleTargetChanged(ulong? newTarget)
        {
            if (_disposed) return;

            if (_currentTarget != newTarget)
            {
                var previousTarget = _currentTarget;
                _currentTarget = newTarget;

                _logger.LogDebug("Server updated target from {PreviousTarget:X} to {NewTarget:X}",
                    previousTarget ?? 0, _currentTarget ?? 0);

                var targetingData = new TargetingData(previousTarget, _currentTarget, DateTime.UtcNow);
                _localTargetChanges.OnNext(targetingData);

                // Legacy callback support
                _targetChangedCallbackManager.InvokeCallbacks(_currentTarget);
            }
        }

        /// <inheritdoc />
        public void HandleTargetingError(string errorMessage, ulong? targetGuid = null)
        {
            if (_disposed) return;

            _logger.LogError("Targeting error: {ErrorMessage} (Target: {TargetGuid:X})", errorMessage, targetGuid ?? 0);

            var errorData = new TargetingErrorData(errorMessage, targetGuid, DateTime.UtcNow);
            _localTargetingErrors.OnNext(errorData);
        }

        #endregion

        #region Legacy Callback Support

        /// <inheritdoc />
        [Obsolete("Use TargetChanges observable instead")]
        public void SetTargetChangedCallback(Action<ulong?>? callback)
        {
            _targetChangedCallbackManager.SetPermanentCallback(callback);
        }

        /// <summary>
        /// Adds a temporary callback for target changes that can be removed later.
        /// This is useful for operations that need to temporarily monitor target changes.
        /// </summary>
        /// <param name="callback">The temporary callback to add.</param>
        /// <returns>A disposable that, when disposed, removes the temporary callback.</returns>
        [Obsolete("Use TargetChanges observable instead")]
        public IDisposable AddTemporaryTargetChangedCallback(Action<ulong?> callback)
        {
            return _targetChangedCallbackManager.AddTemporaryCallback(callback);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the targeting network agent and completes all observables.
        /// </summary>
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _localTargetChanges.OnCompleted();
            _localAssistOperations.OnCompleted();
            _localTargetingErrors.OnCompleted();

            base.Dispose();
        }

        #endregion

        #region Legacy Server Response Methods (for backwards compatibility)

        /// <summary>
        /// Updates the current target based on server response.
        /// This should be called when receiving target update packets.
        /// Use HandleTargetChanged instead.
        /// </summary>
        /// <param name="targetGuid">The new target GUID from the server.</param>
        [Obsolete("Use HandleTargetChanged instead")]
        public void UpdateCurrentTarget(ulong targetGuid)
        {
            var newTarget = targetGuid == 0 ? null : (ulong?)targetGuid;
            HandleTargetChanged(newTarget);
        }

        #endregion
    }
}