using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of attack network agent that handles combat operations in World of Warcraft.
    /// Manages auto-attack functionality using the Mangos protocol.
    /// Works in coordination with the targeting agent for target selection.
    /// Uses reactive observables for better composability and filtering.
    /// </summary>
    public class AttackNetworkClientComponent : IAttackNetworkAgent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<AttackNetworkClientComponent> _logger;
        private bool _isAttacking;
        private bool _isOperationInProgress;
        private DateTime? _lastOperationTime;
        private ulong? _currentVictim;

        // Reactive observables
        private readonly Subject<AttackStateData> _attackStateChanges = new();
        private readonly Subject<WeaponSwingData> _weaponSwings = new();
        private readonly Subject<AttackErrorData> _attackErrors = new();

        // Legacy callback fields for backwards compatibility
        private Action<ulong>? _attackStartedCallback;
        private Action? _attackStoppedCallback;
        private Action<string>? _attackErrorCallback;

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the AttackNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public AttackNetworkClientComponent(IWorldClient worldClient, ILogger<AttackNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Properties

        /// <inheritdoc />
        public bool IsAttacking => _isAttacking;

        /// <inheritdoc />
        public bool IsOperationInProgress => _isOperationInProgress;

        /// <inheritdoc />
        public DateTime? LastOperationTime => _lastOperationTime;

        /// <inheritdoc />
        public ulong? CurrentVictim => _currentVictim;

        #endregion

        #region Reactive Observables

        /// <inheritdoc />
        public IObservable<AttackStateData> AttackStateChanges => _attackStateChanges;

        /// <inheritdoc />
        public IObservable<WeaponSwingData> WeaponSwings => _weaponSwings;

        /// <inheritdoc />
        public IObservable<AttackErrorData> AttackErrors => _attackErrors;

        #endregion

        #region Operations

        /// <inheritdoc />
        public async Task StartAttackAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AttackNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Starting auto-attack");

                // CMSG_ATTACKSWING has no payload for auto-attack
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ATTACKSWING, [], cancellationToken);

                _lastOperationTime = DateTime.UtcNow;

                // Note: We don't immediately set _isAttacking = true here because we want to wait
                // for server confirmation via SMSG_ATTACKSTART. The HandleAttackStateChanged method
                // will be called when the server responds.
                
                _logger.LogInformation("Auto-attack command sent to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start attack");

                var errorData = new AttackErrorData(ex.Message, _currentVictim, DateTime.UtcNow);
                _attackErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task StopAttackAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AttackNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Stopping auto-attack");

                // CMSG_ATTACKSTOP has no payload
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ATTACKSTOP, [], cancellationToken);

                _lastOperationTime = DateTime.UtcNow;

                // Note: We don't immediately set _isAttacking = false here because we want to wait
                // for server confirmation via SMSG_ATTACKSTOP. The HandleAttackStateChanged method
                // will be called when the server responds.
                
                _logger.LogInformation("Stop attack command sent to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop attack");

                var errorData = new AttackErrorData(ex.Message, _currentVictim, DateTime.UtcNow);
                _attackErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task AttackTargetAsync(ulong targetGuid, ITargetingNetworkAgent targetingAgent, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AttackNetworkClientComponent));

            ArgumentNullException.ThrowIfNull(targetingAgent);

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Setting target and starting attack on: {TargetGuid:X}", targetGuid);

                // Set up a task completion source to wait for target confirmation
                var targetCompletionSource = new TaskCompletionSource<bool>();
                var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                // Use reactive observable to wait for target change
                IDisposable? subscription = null;
                if (targetingAgent.TargetChanges != null)
                {
                    subscription = targetingAgent.TargetChanges.Subscribe(targetingData =>
                    {
                        if (targetingData.CurrentTarget == targetGuid)
                        {
                            targetCompletionSource.TrySetResult(true);
                        }
                    });
                }

                try
                {
                    // Set target first using the targeting agent
                    await targetingAgent.SetTargetAsync(targetGuid, cancellationToken);

                    // Wait for target confirmation with timeout (max 1000ms)
                    timeoutCts.CancelAfter(1000);
                    var targetSetTask = targetCompletionSource.Task;
                    var timeoutTask = Task.Delay(-1, timeoutCts.Token);

                    var completedTask = await Task.WhenAny(targetSetTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        _logger.LogWarning("Target setting timed out, proceeding with attack anyway for: {TargetGuid:X}", targetGuid);
                    }
                    else if (targetSetTask.IsCompletedSuccessfully)
                    {
                        _logger.LogDebug("Target confirmed, proceeding with attack on: {TargetGuid:X}", targetGuid);
                    }
                }
                finally
                {
                    // Remove the subscription
                    subscription?.Dispose();
                    timeoutCts.Dispose();
                }

                // Start auto-attack
                await StartAttackAsync(cancellationToken);

                _logger.LogInformation("Successfully set target and started attack on: {TargetGuid:X}", targetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to attack target: {TargetGuid:X}", targetGuid);

                var errorData = new AttackErrorData(ex.Message, targetGuid, DateTime.UtcNow);
                _attackErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
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

        #endregion

        #region Server Response Handling

        /// <inheritdoc />
        public void HandleAttackStateChanged(bool isAttacking, ulong attackerGuid, ulong victimGuid)
        {
            if (_disposed) return;

            if (_isAttacking != isAttacking)
            {
                _isAttacking = isAttacking;
                _currentVictim = isAttacking ? victimGuid : null;
                
                if (isAttacking)
                {
                    _logger.LogDebug("Server confirmed attack started on: {VictimGuid:X}", victimGuid);
                }
                else
                {
                    _logger.LogDebug("Server confirmed attack stopped");
                }

                var attackStateData = new AttackStateData(isAttacking, _currentVictim, DateTime.UtcNow);
                _attackStateChanges.OnNext(attackStateData);

                // Legacy callback support
                if (isAttacking)
                {
                    _attackStartedCallback?.Invoke(victimGuid);
                }
                else
                {
                    _attackStoppedCallback?.Invoke();
                }
            }
        }

        /// <inheritdoc />
        public void HandleWeaponSwing(ulong attackerGuid, ulong victimGuid, uint damage, bool isCritical)
        {
            if (_disposed) return;

            _logger.LogDebug("Weapon swing: {AttackerGuid:X} -> {VictimGuid:X}, Damage: {Damage}, Critical: {IsCritical}", 
                attackerGuid, victimGuid, damage, isCritical);

            var swingData = new WeaponSwingData(attackerGuid, victimGuid, damage, isCritical, DateTime.UtcNow);
            _weaponSwings.OnNext(swingData);
        }

        /// <inheritdoc />
        public void HandleAttackError(string errorMessage, ulong? targetGuid = null)
        {
            if (_disposed) return;

            _logger.LogWarning("Attack error: {ErrorMessage} (Target: {TargetGuid:X})", errorMessage, targetGuid ?? 0);

            var errorData = new AttackErrorData(errorMessage, targetGuid, DateTime.UtcNow);
            _attackErrors.OnNext(errorData);

            // Legacy callback support
            _attackErrorCallback?.Invoke(errorMessage);
        }

        #endregion

        #region Legacy Callback Support

        /// <inheritdoc />
        [Obsolete("Use AttackStateChanges observable instead")]
        public void SetAttackStartedCallback(Action<ulong>? callback)
        {
            _attackStartedCallback = callback;
        }

        /// <inheritdoc />
        [Obsolete("Use AttackStateChanges observable instead")]
        public void SetAttackStoppedCallback(Action? callback)
        {
            _attackStoppedCallback = callback;
        }

        /// <inheritdoc />
        [Obsolete("Use AttackErrors observable instead")]
        public void SetAttackErrorCallback(Action<string>? callback)
        {
            _attackErrorCallback = callback;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the attack network agent and completes all observables.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _attackStateChanges.OnCompleted();
            _weaponSwings.OnCompleted();
            _attackErrors.OnCompleted();

            _attackStateChanges.Dispose();
            _weaponSwings.Dispose();
            _attackErrors.Dispose();

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Legacy Server Response Methods (for backwards compatibility)

        /// <summary>
        /// Updates the attacking state based on server response.
        /// This should be called when receiving SMSG_ATTACKSTART or SMSG_ATTACKSTOP.
        /// Use HandleAttackStateChanged instead.
        /// </summary>
        /// <param name="isAttacking">Whether the character is now attacking.</param>
        /// <param name="attackerGuid">The attacker's GUID (optional).</param>
        /// <param name="victimGuid">The victim's GUID (optional).</param>
        [Obsolete("Use HandleAttackStateChanged instead")]
        public void UpdateAttackingState(bool isAttacking, ulong? attackerGuid = null, ulong? victimGuid = null)
        {
            if (_disposed) return;

            if (_isAttacking != isAttacking)
            {
                _isAttacking = isAttacking;
                _currentVictim = isAttacking ? victimGuid : null;
                
                if (isAttacking && victimGuid.HasValue)
                {
                    _logger.LogDebug("Server confirmed attack started on: {VictimGuid:X}", victimGuid.Value);
                    
                    var attackStateData = new AttackStateData(isAttacking, _currentVictim, DateTime.UtcNow);
                    _attackStateChanges.OnNext(attackStateData);
                    
                    // Legacy callback support - only invoke if we have victim GUID
                    _attackStartedCallback?.Invoke(victimGuid.Value);
                }
                else if (isAttacking && !victimGuid.HasValue)
                {
                    _logger.LogDebug("Server confirmed attack started (no victim GUID provided)");
                    
                    var attackStateData = new AttackStateData(isAttacking, _currentVictim, DateTime.UtcNow);
                    _attackStateChanges.OnNext(attackStateData);
                    
                    // Don't invoke callback when no victim GUID is provided
                }
                else if (!isAttacking)
                {
                    _logger.LogDebug("Server confirmed attack stopped");
                    
                    var attackStateData = new AttackStateData(isAttacking, _currentVictim, DateTime.UtcNow);
                    _attackStateChanges.OnNext(attackStateData);
                    
                    // Legacy callback support
                    _attackStoppedCallback?.Invoke();
                }
            }
        }

        /// <summary>
        /// Reports an attack error based on server response.
        /// This should be called when receiving attack error packets.
        /// Use HandleAttackError instead.
        /// </summary>
        /// <param name="errorMessage">The error message describing why the attack failed.</param>
        [Obsolete("Use HandleAttackError instead")]
        public void ReportAttackError(string errorMessage)
        {
            HandleAttackError(errorMessage);
        }

        #endregion
    }
}