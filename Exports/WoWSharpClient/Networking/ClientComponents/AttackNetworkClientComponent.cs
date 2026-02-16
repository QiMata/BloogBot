using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
using System.Reactive.Subjects; // Added for Subject
using System.Reactive.Disposables;
using System;
using System.Threading.Tasks;
using System.Threading; // Added for CompositeDisposable

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of attack network agent that handles combat operations in World of Warcraft.
    /// Manages auto-attack functionality using the Mangos protocol.
    /// Works in coordination with the targeting agent for target selection.
    /// Uses reactive observables for better composability and filtering.
    /// </summary>
    public class AttackNetworkClientComponent : NetworkClientComponent, IAttackNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<AttackNetworkClientComponent> _logger;
        private bool _isAttacking;
        private ulong? _currentVictim;
        private ulong _pendingAttackTarget;
        private bool _disposed;

        // Reactive observable pipelines (wired to world client streams/opcodes)
        private readonly IObservable<AttackStateData> _attackStateChanges;
        private readonly IObservable<WeaponSwingData> _weaponSwings;
        private readonly IObservable<AttackErrorData> _attackErrors;

        // Internal subjects & disposables for eager subscription so we don't miss early events
        private readonly Subject<AttackStateData> _attackStateSubject = new();
        private readonly CompositeDisposable _disposables = new();

        /// <summary>
        /// Initializes a new instance of the AttackNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public AttackNetworkClientComponent(IWorldClient worldClient, ILogger<AttackNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Eagerly subscribe to attack state changes so state transitions occurring before external subscriptions aren't lost
            bool lastState = _isAttacking; // initial false
            var attackStateSubscription = SafeStream(_worldClient.AttackStateChanged)
                .Subscribe(tuple =>
                {
                    var (isAttacking, attackerGuid, victimGuid) = tuple;

                    // Only emit when state actually changes
                    if (lastState != isAttacking)
                    {
                        _isAttacking = isAttacking;
                        _currentVictim = isAttacking ? victimGuid : null;
                        lastState = isAttacking;

                        if (isAttacking)
                        {
                            _logger.LogDebug("Server confirmed attack started on: {VictimGuid:X}", victimGuid);
                        }
                        else
                        {
                            _logger.LogDebug("Server confirmed attack stopped");
                        }

                        _attackStateSubject.OnNext(new AttackStateData(isAttacking, _currentVictim, DateTime.UtcNow));
                    }
                }, ex => _logger.LogError(ex, "Attack state stream faulted"));
            _disposables.Add(attackStateSubscription);

            _attackStateChanges = _attackStateSubject.AsObservable();

            // Attack errors from the world client stream
            _attackErrors = SafeStream(_worldClient.AttackErrors)
                .Select(errMsg => new AttackErrorData(errMsg, _currentVictim, DateTime.UtcNow))
                .Do(err => _logger.LogWarning("Attack error: {ErrorMessage} (Target: {TargetGuid:X})", err.ErrorMessage, err.TargetGuid ?? 0));
            _disposables.Add(_attackErrors.Subscribe(_ => { }, ex => _logger.LogError(ex, "Attack error stream faulted"))); // ensure side-effects, keep hot

            // Weapon swings via opcode stream (best-effort parsing)
            _weaponSwings = SafeOpcodeStream(Opcode.SMSG_ATTACKERSTATEUPDATE)
                .Select(ParseWeaponSwing);
        }

        #region Properties

        /// <inheritdoc />
        public bool IsAttacking => _isAttacking;

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

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Starting auto-attack on target {TargetGuid:X}", _pendingAttackTarget);

                // CMSG_ATTACKSWING requires the target's full 8-byte GUID
                var payload = BitConverter.GetBytes(_pendingAttackTarget);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ATTACKSWING, payload, cancellationToken);

                _logger.LogInformation("Auto-attack command sent to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start attack");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task StopAttackAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AttackNetworkClientComponent));

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Stopping auto-attack");

                // CMSG_ATTACKSTOP has no payload
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ATTACKSTOP, [], cancellationToken);

                _logger.LogInformation("Stop attack command sent to server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop attack");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task AttackTargetAsync(ulong targetGuid, ITargetingNetworkClientComponent targetingAgent, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AttackNetworkClientComponent));

            ArgumentNullException.ThrowIfNull(targetingAgent);

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Setting target and starting attack on: {TargetGuid:X}", targetGuid);
                _pendingAttackTarget = targetGuid;

                var targetCompletionSource = new TaskCompletionSource<bool>();
                var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

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
                    subscription?.Dispose();
                    timeoutCts.Dispose();
                }

                await StartAttackAsync(cancellationToken);

                _logger.LogInformation("Successfully set target and started attack on: {TargetGuid:X}", targetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to attack target: {TargetGuid:X}", targetGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
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

        #region Helpers
        // Provides a non-null observable (fallback to empty) for different source types
        private static IObservable<T> SafeStream<T>(IObservable<T>? source)
            => source ?? Observable.Empty<T>();

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        private WeaponSwingData ParseWeaponSwing(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;

            ulong attacker = span.Length >= 8 ? BitConverter.ToUInt64(span[..8]) : 0UL;
            ulong victim = span.Length >= 16 ? BitConverter.ToUInt64(span.Slice(8, 8)) : 0UL;
            uint damage = span.Length >= 20 ? BitConverter.ToUInt32(span.Slice(16, 4)) : 0u;
            bool isCritical = span.Length >= 21 && span[20] != 0;

            _logger.LogDebug("Weapon swing: {AttackerGuid:X} -> {VictimGuid:X}, Damage: {Damage}, Critical: {IsCritical}", attacker, victim, damage, isCritical);
            return new WeaponSwingData(attacker, victim, damage, isCritical, DateTime.UtcNow);
        }
        #endregion

        #region IDisposable
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _disposables.Dispose();
            _attackStateSubject.Dispose();
            base.Dispose();
        }
        #endregion
    }
}