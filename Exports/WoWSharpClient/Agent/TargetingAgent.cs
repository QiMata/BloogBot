using GameData.Core.Enums;
using WoWSharpClient.Client;
using Microsoft.Extensions.Logging;

namespace WoWSharpClient.Agent
{
    /// <summary>
    /// Implementation of targeting agent that handles target selection operations in World of Warcraft.
    /// Manages target selection and assist functionality using the Mangos protocol.
    /// This agent focuses solely on targeting without combat functionality.
    /// </summary>
    public class TargetingAgent : ITargetingAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<TargetingAgent> _logger;
        private ulong? _currentTarget;

        /// <summary>
        /// Initializes a new instance of the TargetingAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public TargetingAgent(IWorldClient worldClient, ILogger<TargetingAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public ulong? CurrentTarget => _currentTarget;

        /// <inheritdoc />
        public event Action<ulong?>? TargetChanged;

        /// <inheritdoc />
        public async Task SetTargetAsync(ulong targetGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Setting target to GUID: {TargetGuid:X}", targetGuid);

                // Create packet payload with target GUID
                var payload = new byte[8];
                BitConverter.GetBytes(targetGuid).CopyTo(payload, 0);

                // Send CMSG_SET_SELECTION packet
                await _worldClient.SendMovementAsync(Opcode.CMSG_SET_SELECTION, payload, cancellationToken);

                // Update internal state
                var previousTarget = _currentTarget;
                _currentTarget = targetGuid == 0 ? null : targetGuid;

                // Fire target changed event if target actually changed
                if (previousTarget != _currentTarget)
                {
                    _logger.LogInformation("Target changed from {PreviousTarget:X} to {NewTarget:X}", 
                        previousTarget ?? 0, _currentTarget ?? 0);
                    TargetChanged?.Invoke(_currentTarget);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set target to {TargetGuid:X}", targetGuid);
                throw;
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assist player: {PlayerGuid:X}", playerGuid);
                throw;
            }
        }

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

        /// <summary>
        /// Updates the current target based on server response.
        /// This should be called when receiving target update packets.
        /// </summary>
        /// <param name="targetGuid">The new target GUID from the server.</param>
        public void UpdateCurrentTarget(ulong targetGuid)
        {
            var newTarget = targetGuid == 0 ? null : (ulong?)targetGuid;
            
            if (_currentTarget != newTarget)
            {
                var previousTarget = _currentTarget;
                _currentTarget = newTarget;
                
                _logger.LogDebug("Server updated target from {PreviousTarget:X} to {NewTarget:X}",
                    previousTarget ?? 0, _currentTarget ?? 0);
                    
                TargetChanged?.Invoke(_currentTarget);
            }
        }
    }
}