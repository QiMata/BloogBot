using GameData.Core.Enums;
using WoWSharpClient.Client;
using Microsoft.Extensions.Logging;

namespace WoWSharpClient.Agent
{
    /// <summary>
    /// Implementation of attack agent that handles combat operations in World of Warcraft.
    /// Manages auto-attack functionality using the Mangos protocol.
    /// Works in coordination with the targeting agent for target selection.
    /// </summary>
    public class AttackAgent : IAttackAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<AttackAgent> _logger;
        private bool _isAttacking;

        /// <summary>
        /// Initializes a new instance of the AttackAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public AttackAgent(IWorldClient worldClient, ILogger<AttackAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsAttacking => _isAttacking;

        /// <inheritdoc />
        public event Action<ulong>? AttackStarted;

        /// <inheritdoc />
        public event Action? AttackStopped;

        /// <inheritdoc />
        public event Action<string>? AttackError;

        /// <inheritdoc />
        public async Task StartAttackAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting auto-attack");

                // CMSG_ATTACKSWING has no payload for auto-attack
                await _worldClient.SendMovementAsync(Opcode.CMSG_ATTACKSWING, Array.Empty<byte>(), cancellationToken);

                // Note: We don't immediately set _isAttacking = true here because we want to wait
                // for server confirmation via SMSG_ATTACKSTART. The UpdateAttackingState method
                // will be called when the server responds.
                
                _logger.LogInformation("Auto-attack command sent to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start attack");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task StopAttackAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Stopping auto-attack");

                // CMSG_ATTACKSTOP has no payload
                await _worldClient.SendMovementAsync(Opcode.CMSG_ATTACKSTOP, Array.Empty<byte>(), cancellationToken);

                // Note: We don't immediately set _isAttacking = false here because we want to wait
                // for server confirmation via SMSG_ATTACKSTOP. The UpdateAttackingState method
                // will be called when the server responds.
                
                _logger.LogInformation("Stop attack command sent to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop attack");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task AttackTargetAsync(ulong targetGuid, ITargetingAgent targetingAgent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(targetingAgent);

            try
            {
                _logger.LogDebug("Setting target and starting attack on: {TargetGuid:X}", targetGuid);

                // Set target first using the targeting agent
                await targetingAgent.SetTargetAsync(targetGuid, cancellationToken);

                // Small delay to ensure target is set before attacking
                await Task.Delay(50, cancellationToken);

                // Start auto-attack
                await StartAttackAsync(cancellationToken);

                _logger.LogInformation("Successfully set target and started attack on: {TargetGuid:X}", targetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to attack target: {TargetGuid:X}", targetGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ToggleAttackAsync(CancellationToken cancellationToken = default)
        {
            if (_isAttacking)
            {
                await StopAttackAsync(cancellationToken);
            }
            else
            {
                await StartAttackAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Updates the attacking state based on server response.
        /// This should be called when receiving SMSG_ATTACKSTART or SMSG_ATTACKSTOP.
        /// </summary>
        /// <param name="isAttacking">Whether the character is now attacking.</param>
        /// <param name="attackerGuid">The attacker's GUID (optional).</param>
        /// <param name="victimGuid">The victim's GUID (optional).</param>
        public void UpdateAttackingState(bool isAttacking, ulong? attackerGuid = null, ulong? victimGuid = null)
        {
            if (_isAttacking != isAttacking)
            {
                _isAttacking = isAttacking;
                
                if (isAttacking && victimGuid.HasValue)
                {
                    _logger.LogDebug("Server confirmed attack started on: {VictimGuid:X}", victimGuid.Value);
                    AttackStarted?.Invoke(victimGuid.Value);
                }
                else if (!isAttacking)
                {
                    _logger.LogDebug("Server confirmed attack stopped");
                    AttackStopped?.Invoke();
                }
            }
        }

        /// <summary>
        /// Reports an attack error based on server response.
        /// This should be called when receiving attack error packets.
        /// </summary>
        /// <param name="errorMessage">The error message describing why the attack failed.</param>
        public void ReportAttackError(string errorMessage)
        {
            _logger.LogWarning("Attack error: {ErrorMessage}", errorMessage);
            AttackError?.Invoke(errorMessage);
        }
    }
}