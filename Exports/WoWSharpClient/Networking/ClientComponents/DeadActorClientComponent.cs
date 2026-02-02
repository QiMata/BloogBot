using System;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of dead actor agent that handles death and resurrection operations in World of Warcraft.
    /// Manages spirit release, corpse resurrection, and spirit healer interactions using the Mangos protocol.
    /// Uses opcode-backed observables (no events/subjects).
    /// </summary>
    public class DeadActorClientComponent : NetworkClientComponent, IDeadActorNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<DeadActorClientComponent> _logger;

        private bool _isDead;
        private bool _isGhost;
        private bool _hasResurrectionRequest;
        private (float X, float Y, float Z)? _corpseLocation;
        private DateTime? _spiritHealerResurrectionTime;
        private bool _disposed;

        // Reactive opcode-backed streams (keep conservative defaults until protocol wiring is implemented)
        private readonly IObservable<DeathData> _deathEvents;
        private readonly IObservable<ResurrectionData> _resurrectionNotifications;
        private readonly IObservable<DeathErrorData> _deathErrors;

        /// <summary>
        /// Initializes a new instance of the DeadActorAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public DeadActorClientComponent(IWorldClient worldClient, ILogger<DeadActorClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Expose opcode-backed observables. If no server streams exist yet, expose never-ending streams.
            // Integrators can wire specific SMSG_* opcodes here once available (e.g., SMSG_RESURRECT_REQUEST, etc.).
            _deathEvents = Observable.Never<DeathData>();
            _resurrectionNotifications = Observable.Never<ResurrectionData>();
            _deathErrors = Observable.Never<DeathErrorData>();
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
        public IObservable<DeathData> DeathEvents => _deathEvents;

        /// <inheritdoc />
        public IObservable<ResurrectionData> ResurrectionNotifications => _resurrectionNotifications;

        /// <inheritdoc />
        public IObservable<DeathErrorData> DeathErrors => _deathErrors;

        /// <inheritdoc />
        public async Task ReleaseSpiritAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Releasing spirit");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_REPOP_REQUEST, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Spirit release request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release spirit");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task ResurrectAtCorpseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Attempting to resurrect at corpse");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_RECLAIM_CORPSE, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Corpse resurrection request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resurrect at corpse");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task AcceptResurrectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Accepting resurrection request");

                var payload = new byte[1];
                payload[0] = 1; // Accept flag

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_RESURRECT_RESPONSE, payload, cancellationToken);

                _hasResurrectionRequest = false;
                _logger.LogInformation("Resurrection request accepted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept resurrection");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task DeclineResurrectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Declining resurrection request");

                var payload = new byte[1];
                payload[0] = 0; // Decline flag

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_RESURRECT_RESPONSE, payload, cancellationToken);

                _hasResurrectionRequest = false;
                _logger.LogInformation("Resurrection request declined");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decline resurrection");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task ResurrectWithSpiritHealerAsync(ulong spiritHealerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Resurrecting with spirit healer: {SpiritHealerGuid:X}", spiritHealerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(spiritHealerGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SPIRIT_HEALER_ACTIVATE, payload, cancellationToken);

                _logger.LogInformation("Spirit healer resurrection request sent to: {SpiritHealerGuid:X}", spiritHealerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resurrect with spirit healer: {SpiritHealerGuid:X}", spiritHealerGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task QueryCorpseLocationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Querying corpse location");

                await _worldClient.SendOpcodeAsync(Opcode.MSG_CORPSE_QUERY, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Corpse location query sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query corpse location");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task QueryAreaSpiritHealersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Querying area spirit healers");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AREA_SPIRIT_HEALER_QUERY, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Area spirit healer query sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query area spirit healers");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task QueueForSpiritHealerAsync(ulong spiritHealerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Queueing for spirit healer: {SpiritHealerGuid:X}", spiritHealerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(spiritHealerGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AREA_SPIRIT_HEALER_QUEUE, payload, cancellationToken);

                _logger.LogInformation("Spirit healer queue request sent to: {SpiritHealerGuid:X}", spiritHealerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue for spirit healer: {SpiritHealerGuid:X}", spiritHealerGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task SelfResurrectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Attempting self-resurrection");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SELF_RES, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Self-resurrection request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to self-resurrect");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
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
                SetOperationInProgress(true);
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
                    await Task.Delay(2000, cancellationToken);
                }

                // Step 2: Query corpse location
                _logger.LogDebug("Querying corpse location");
                await QueryCorpseLocationAsync(cancellationToken);
                await Task.Delay(1000, cancellationToken);

                // Step 3: Try to resurrect at corpse
                if (_corpseLocation.HasValue)
                {
                    _logger.LogDebug("Attempting resurrection at corpse");
                    await ResurrectAtCorpseAsync(cancellationToken);
                    await Task.Delay(2000, cancellationToken);
                }

                // Step 4: If still dead and spirit healer is allowed, use spirit healer
                if (_isDead && allowSpiritHealer)
                {
                    _logger.LogDebug("Corpse resurrection failed, querying for spirit healers");
                    await QueryAreaSpiritHealersAsync(cancellationToken);
                    await Task.Delay(1000, cancellationToken);
                }

                _logger.LogInformation("Automatic death handling completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto death handling failed");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public void UpdateDeathState(bool isDead, bool isGhost)
        {
            _isDead = isDead;
            _isGhost = isGhost;
            if (!isDead)
            {
                _corpseLocation = null; // Clear corpse location on resurrection
            }
            _logger.LogDebug("Death state updated: IsDead={IsDead}, IsGhost={IsGhost}", isDead, isGhost);
        }

        /// <inheritdoc />
        public void UpdateCorpseLocation(float x, float y, float z)
        {
            _corpseLocation = (x, y, z);
            _logger.LogDebug("Corpse location updated: ({X:F2}, {Y:F2}, {Z:F2})", x, y, z);
        }

        /// <inheritdoc />
        public void HandleResurrectionRequest(ulong resurrectorGuid, string resurrectorName)
        {
            _hasResurrectionRequest = true;
            _logger.LogDebug("Resurrection request received from: {ResurrectorName} ({ResurrectorGuid:X})", resurrectorName, resurrectorGuid);
        }

        /// <inheritdoc />
        public void HandleSpiritHealerTime(TimeSpan timeSpan)
        {
            _spiritHealerResurrectionTime = DateTime.UtcNow.Add(timeSpan);
            _logger.LogDebug("Spirit healer resurrection time set: {TimeSpan} ({ResurrectionTime})", timeSpan, _spiritHealerResurrectionTime);
        }

        /// <inheritdoc />
        public void HandleDeathError(string errorMessage)
        {
            _logger.LogWarning("Death operation error: {Error}", errorMessage);
        }

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the dead actor agent and cleans up resources.
        /// </summary>
        public override void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing DeadActorClientComponent");

            _disposed = true;
            _logger.LogDebug("DeadActorClientComponent disposed");

            base.Dispose();
        }

        #endregion

        #region Helpers
        private IObservable<ReadOnlyMemory<byte>> SafeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();
        #endregion
    }
}