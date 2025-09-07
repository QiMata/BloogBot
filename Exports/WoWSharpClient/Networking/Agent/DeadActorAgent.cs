using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent.I;

namespace WoWSharpClient.Networking.Agent
{
    /// <summary>
    /// Implementation of dead actor agent that handles death and resurrection operations in World of Warcraft.
    /// Manages spirit release, corpse resurrection, and spirit healer interactions using the Mangos protocol.
    /// </summary>
    public class DeadActorAgent : IDeadActorAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<DeadActorAgent> _logger;

        private bool _isDead;
        private bool _isGhost;
        private bool _hasResurrectionRequest;
        private (float X, float Y, float Z)? _corpseLocation;
        private DateTime? _spiritHealerResurrectionTime;

        /// <summary>
        /// Initializes a new instance of the DeadActorAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public DeadActorAgent(IWorldClient worldClient, ILogger<DeadActorAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsDead => _isDead;

        /// <inheritdoc />
        public bool IsGhost => _isGhost;

        /// <inheritdoc />
        public bool HasResurrectionRequest => _hasResurrectionRequest;

        /// <inheritdoc />
        public (float X, float Y, float Z)? CorpseLocation => _corpseLocation;

        /// <inheritdoc />
        public event Action? OnDeath;

        /// <inheritdoc />
        public event Action? OnSpiritReleased;

        /// <inheritdoc />
        public event Action? OnResurrected;

        /// <inheritdoc />
        public event Action<ulong, string>? OnResurrectionRequest;

        /// <inheritdoc />
        public event Action<float, float, float>? OnCorpseLocationUpdated;

        /// <inheritdoc />
        public event Action<string>? OnDeathError;

        /// <inheritdoc />
        public async Task ReleaseSpiritAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Releasing spirit");

                await _worldClient.SendMovementAsync(Opcode.CMSG_REPOP_REQUEST, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Spirit release request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release spirit");
                OnDeathError?.Invoke($"Failed to release spirit: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ResurrectAtCorpseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Attempting to resurrect at corpse");

                await _worldClient.SendMovementAsync(Opcode.CMSG_RECLAIM_CORPSE, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Corpse resurrection request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resurrect at corpse");
                OnDeathError?.Invoke($"Failed to resurrect at corpse: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task AcceptResurrectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Accepting resurrection request");

                var payload = new byte[1];
                payload[0] = 1; // Accept flag

                await _worldClient.SendMovementAsync(Opcode.CMSG_RESURRECT_RESPONSE, payload, cancellationToken);

                _hasResurrectionRequest = false;
                _logger.LogInformation("Resurrection request accepted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept resurrection");
                OnDeathError?.Invoke($"Failed to accept resurrection: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DeclineResurrectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Declining resurrection request");

                var payload = new byte[1];
                payload[0] = 0; // Decline flag

                await _worldClient.SendMovementAsync(Opcode.CMSG_RESURRECT_RESPONSE, payload, cancellationToken);

                _hasResurrectionRequest = false;
                _logger.LogInformation("Resurrection request declined");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decline resurrection");
                OnDeathError?.Invoke($"Failed to decline resurrection: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ResurrectWithSpiritHealerAsync(ulong spiritHealerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Resurrecting with spirit healer: {SpiritHealerGuid:X}", spiritHealerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(spiritHealerGuid).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_SPIRIT_HEALER_ACTIVATE, payload, cancellationToken);

                _logger.LogInformation("Spirit healer resurrection request sent to: {SpiritHealerGuid:X}", spiritHealerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resurrect with spirit healer: {SpiritHealerGuid:X}", spiritHealerGuid);
                OnDeathError?.Invoke($"Failed to resurrect with spirit healer: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QueryCorpseLocationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Querying corpse location");

                await _worldClient.SendMovementAsync(Opcode.MSG_CORPSE_QUERY, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Corpse location query sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query corpse location");
                OnDeathError?.Invoke($"Failed to query corpse location: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QueryAreaSpiritHealersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Querying area spirit healers");

                await _worldClient.SendMovementAsync(Opcode.CMSG_AREA_SPIRIT_HEALER_QUERY, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Area spirit healer query sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query area spirit healers");
                OnDeathError?.Invoke($"Failed to query area spirit healers: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QueueForSpiritHealerAsync(ulong spiritHealerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Queueing for spirit healer: {SpiritHealerGuid:X}", spiritHealerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(spiritHealerGuid).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_AREA_SPIRIT_HEALER_QUEUE, payload, cancellationToken);

                _logger.LogInformation("Spirit healer queue request sent to: {SpiritHealerGuid:X}", spiritHealerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue for spirit healer: {SpiritHealerGuid:X}", spiritHealerGuid);
                OnDeathError?.Invoke($"Failed to queue for spirit healer: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SelfResurrectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Attempting self-resurrection");

                await _worldClient.SendMovementAsync(Opcode.CMSG_SELF_RES, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Self-resurrection request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to self-resurrect");
                OnDeathError?.Invoke($"Failed to self-resurrect: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public float? GetDistanceToCorpse(float currentX, float currentY, float currentZ)
        {
            if (!_corpseLocation.HasValue)
                return null;

            var corpse = _corpseLocation.Value;
            var dx = corpse.X - currentX;
            var dy = corpse.Y - currentY;
            var dz = corpse.Z - currentZ;

            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <inheritdoc />
        public bool IsCloseToCorpse(float currentX, float currentY, float currentZ, float maxDistance = 39.0f)
        {
            var distance = GetDistanceToCorpse(currentX, currentY, currentZ);
            return distance.HasValue && distance.Value <= maxDistance;
        }

        /// <inheritdoc />
        public TimeSpan? GetSpiritHealerResurrectionTime()
        {
            if (!_spiritHealerResurrectionTime.HasValue)
                return null;

            var timeLeft = _spiritHealerResurrectionTime.Value - DateTime.UtcNow;
            return timeLeft.TotalSeconds > 0 ? timeLeft : TimeSpan.Zero;
        }

        /// <inheritdoc />
        public async Task AutoHandleDeathAsync(bool allowSpiritHealer = false, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting automatic death handling (allowSpiritHealer: {AllowSpiritHealer})", allowSpiritHealer);

                if (!_isDead)
                {
                    _logger.LogWarning("Auto handle death called but character is not dead");
                    return;
                }

                // Step 1: Release spirit if not already done
                if (!_isGhost)
                {
                    _logger.LogDebug("Releasing spirit as part of auto death handling");
                    await ReleaseSpiritAsync(cancellationToken);
                    
                    // Wait for spirit release to complete
                    await Task.Delay(2000, cancellationToken);
                }

                // Step 2: Query corpse location
                _logger.LogDebug("Querying corpse location");
                await QueryCorpseLocationAsync(cancellationToken);
                
                // Wait for corpse location response
                await Task.Delay(1000, cancellationToken);

                // Step 3: Try to resurrect at corpse
                if (_corpseLocation.HasValue)
                {
                    _logger.LogDebug("Attempting resurrection at corpse");
                    await ResurrectAtCorpseAsync(cancellationToken);
                    
                    // Wait to see if resurrection succeeds
                    await Task.Delay(2000, cancellationToken);
                }

                // Step 4: If still dead and spirit healer is allowed, use spirit healer
                if (_isDead && allowSpiritHealer)
                {
                    _logger.LogDebug("Corpse resurrection failed, querying for spirit healers");
                    await QueryAreaSpiritHealersAsync(cancellationToken);
                    
                    // Wait for spirit healer response
                    await Task.Delay(1000, cancellationToken);
                    
                    // Note: In a real implementation, you'd need to get the actual spirit healer GUID
                    // This is just a placeholder - the actual GUID would come from the server response
                    // await ResurrectWithSpiritHealerAsync(spiritHealerGuid, cancellationToken);
                }

                _logger.LogInformation("Automatic death handling completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto death handling failed");
                OnDeathError?.Invoke($"Auto death handling failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public void UpdateDeathState(bool isDead, bool isGhost)
        {
            var wasAlive = !_isDead;
            var becameGhost = !_isGhost && isGhost;
            var wasResurrected = _isDead && !isDead;

            _isDead = isDead;
            _isGhost = isGhost;

            if (wasAlive && isDead)
            {
                _logger.LogInformation("Character died");
                OnDeath?.Invoke();
            }

            if (becameGhost)
            {
                _logger.LogInformation("Character became a ghost (spirit released)");
                OnSpiritReleased?.Invoke();
            }

            if (wasResurrected)
            {
                _logger.LogInformation("Character was resurrected");
                _corpseLocation = null; // Clear corpse location on resurrection
                OnResurrected?.Invoke();
            }

            _logger.LogDebug("Death state updated: IsDead={IsDead}, IsGhost={IsGhost}", isDead, isGhost);
        }

        /// <inheritdoc />
        public void UpdateCorpseLocation(float x, float y, float z)
        {
            _corpseLocation = (x, y, z);
            OnCorpseLocationUpdated?.Invoke(x, y, z);
            _logger.LogDebug("Corpse location updated: ({X:F2}, {Y:F2}, {Z:F2})", x, y, z);
        }

        /// <summary>
        /// Handles server responses for resurrection requests.
        /// This method should be called when SMSG_RESURRECT_REQUEST is received.
        /// </summary>
        /// <param name="resurrectorGuid">The GUID of the player or NPC offering resurrection.</param>
        /// <param name="resurrectorName">The name of the resurrector.</param>
        public void HandleResurrectionRequest(ulong resurrectorGuid, string resurrectorName)
        {
            _hasResurrectionRequest = true;
            OnResurrectionRequest?.Invoke(resurrectorGuid, resurrectorName);
            _logger.LogDebug("Resurrection request received from: {ResurrectorName} ({ResurrectorGuid:X})", resurrectorName, resurrectorGuid);
        }

        /// <summary>
        /// Handles server responses for spirit healer resurrection time.
        /// This method should be called when SMSG_AREA_SPIRIT_HEALER_TIME is received.
        /// </summary>
        /// <param name="timeUntilResurrection">Time until spirit healer resurrection becomes available.</param>
        public void HandleSpiritHealerTime(TimeSpan timeUntilResurrection)
        {
            _spiritHealerResurrectionTime = DateTime.UtcNow.Add(timeUntilResurrection);
            _logger.LogDebug("Spirit healer resurrection available in: {Time}", timeUntilResurrection);
        }

        /// <summary>
        /// Handles server responses for resurrection failures.
        /// This method should be called when SMSG_RESURRECT_FAILED is received.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        public void HandleResurrectionFailed(string errorMessage)
        {
            OnDeathError?.Invoke($"Resurrection failed: {errorMessage}");
            _logger.LogWarning("Resurrection failed: {Error}", errorMessage);
        }

        /// <summary>
        /// Handles server responses for spirit healer confirmation.
        /// This method should be called when SMSG_SPIRIT_HEALER_CONFIRM is received.
        /// </summary>
        /// <param name="spiritHealerGuid">The GUID of the spirit healer.</param>
        public void HandleSpiritHealerConfirm(ulong spiritHealerGuid)
        {
            _logger.LogDebug("Spirit healer confirmation received from: {SpiritHealerGuid:X}", spiritHealerGuid);
        }

        /// <summary>
        /// Handles general death/resurrection errors.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        public void HandleDeathError(string errorMessage)
        {
            OnDeathError?.Invoke(errorMessage);
            _logger.LogWarning("Death operation failed: {Error}", errorMessage);
        }
    }
}