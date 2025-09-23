using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using System.Reactive;
using System.Reactive.Linq;
using System.Buffers.Binary;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of trainer network agent that handles trainer operations in World of Warcraft.
    /// Manages learning spells, abilities, and skills from NPC trainers using the Mangos protocol.
    /// </summary>
    public class TrainerNetworkClientComponent : NetworkClientComponent, ITrainerNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<TrainerNetworkClientComponent> _logger;

        private bool _isTrainerWindowOpen;
        private ulong? _currentTrainerGuid;
        private readonly List<TrainerServiceData> _availableServices;
        private bool _disposed;

        // Opcode-backed reactive streams
        private readonly IObservable<(ulong TrainerGuid, TrainerServiceData[] Services)> _trainerWindowsOpened;
        private readonly IObservable<Unit> _trainerWindowsClosed;
        private readonly IObservable<(uint SpellId, uint Cost)> _spellsLearned;
        private readonly IObservable<TrainerServiceData[]> _trainerServicesUpdated;
        private readonly IObservable<string> _trainerErrors;

        /// <summary>
        /// Initializes a new instance of the TrainerNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public TrainerNetworkClientComponent(IWorldClient worldClient, ILogger<TrainerNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _availableServices = [];

            // Wire opcode streams -> observables
            _trainerWindowsOpened = SafeOpcodeStream(Opcode.SMSG_TRAINER_LIST)
                .Select(ParseTrainerList)
                .Do(tuple =>
                {
                    _isTrainerWindowOpen = true;
                    _currentTrainerGuid = tuple.TrainerGuid;
                    _availableServices.Clear();
                    _availableServices.AddRange(tuple.Services);

                    TrainerWindowOpened?.Invoke(tuple.TrainerGuid);
                    TrainerServicesReceived?.Invoke(tuple.Services);

                    _logger.LogDebug("Trainer window opened for: {TrainerGuid:X} with {ServiceCount} services",
                        tuple.TrainerGuid, tuple.Services.Length);
                })
                .Publish()
                .RefCount();

            // Close stream derived from server-side close indicators (best-effort)
            _trainerWindowsClosed = Observable.Merge(
                    SafeOpcodeStream(Opcode.SMSG_GOSSIP_COMPLETE),
                    Observable.Empty<ReadOnlyMemory<byte>>()
                )
                .Select(_ => Unit.Default)
                .Do(_ =>
                {
                    _isTrainerWindowOpen = false;
                    _currentTrainerGuid = null;
                    _availableServices.Clear();
                    TrainerWindowClosed?.Invoke();
                    _logger.LogDebug("Trainer window closed by server");
                })
                .Publish()
                .RefCount();

            _spellsLearned = SafeOpcodeStream(Opcode.SMSG_TRAINER_BUY_SUCCEEDED)
                .Select(ParseTrainerBuySucceeded)
                .Do(tuple =>
                {
                    SpellLearned?.Invoke(tuple.SpellId, tuple.Cost);
                    _logger.LogDebug("Spell learned: {SpellId} (cost: {Cost})", tuple.SpellId, tuple.Cost);
                })
                .Publish()
                .RefCount();

            _trainerServicesUpdated = _trainerWindowsOpened
                .Select(t => t.Services)
                .Do(services =>
                {
                    TrainerServicesReceived?.Invoke(services);
                    _logger.LogDebug("Trainer services updated: {ServiceCount} services", services.Length);
                })
                .Publish()
                .RefCount();

            _trainerErrors = SafeOpcodeStream(Opcode.SMSG_TRAINER_BUY_FAILED)
                .Select(ParseTrainerBuyFailed)
                .Do(err =>
                {
                    TrainerError?.Invoke(err);
                    _logger.LogWarning("Trainer operation failed: {Error}", err);
                })
                .Publish()
                .RefCount();
        }

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        private static uint ReadUInt32(ReadOnlySpan<byte> span, int offset)
            => (uint)(span.Length >= offset + 4 ? BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)) : 0u);

        private static ulong ReadUInt64(ReadOnlySpan<byte> span, int offset)
            => span.Length >= offset + 8 ? BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)) : 0UL;

        private (ulong TrainerGuid, TrainerServiceData[] Services) ParseTrainerList(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            ulong trainerGuid = ReadUInt64(span, 0);
            var services = new List<TrainerServiceData>();

            // Best-effort parsing: if a count exists at offset 8 (common), read minimal entries
            int offset = 8;
            uint count = span.Length >= offset + 4 ? ReadUInt32(span, offset) : 0u;
            offset += span.Length >= 12 ? 4 : 0; // advance if count read

            int recordMinSize = 12; // index(4) + spellId(4) + cost(4)
            for (int i = 0; i < count && span.Length >= offset + recordMinSize; i++)
            {
                var service = new TrainerServiceData
                {
                    ServiceIndex = ReadUInt32(span, offset + 0),
                    SpellId = ReadUInt32(span, offset + 4),
                    Cost = ReadUInt32(span, offset + 8),
                    RequiredLevel = span.Length >= offset + 16 ? ReadUInt32(span, offset + 12) : 0,
                    RequiredSkill = span.Length >= offset + 20 ? ReadUInt32(span, offset + 16) : 0,
                    RequiredSkillLevel = span.Length >= offset + 24 ? ReadUInt32(span, offset + 20) : 0,
                    CanLearn = true,
                    Name = string.Empty
                };

                services.Add(service);
                offset += recordMinSize; // conservative advance
            }

            return (trainerGuid, services.ToArray());
        }

        private (uint SpellId, uint Cost) ParseTrainerBuySucceeded(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint spellId = ReadUInt32(span, 0);
            uint cost = span.Length >= 8 ? ReadUInt32(span, 4) : 0u;
            return (spellId, cost);
        }

        private string ParseTrainerBuyFailed(ReadOnlyMemory<byte> payload)
        {
            // Best-effort: if an error code exists, map to message; else generic
            var span = payload.Span;
            uint error = ReadUInt32(span, 0);
            return $"Trainer buy failed (code {error})";
        }

        #region ITrainerNetworkClientComponent Implementation

        /// <inheritdoc />
        public bool IsTrainerWindowOpen => _isTrainerWindowOpen;

        /// <inheritdoc />
        public ulong? CurrentTrainerGuid => _currentTrainerGuid;

        /// <inheritdoc />
        public event Action<ulong>? TrainerWindowOpened;

        /// <inheritdoc />
        public event Action? TrainerWindowClosed;

        /// <inheritdoc />
        public event Action<uint, uint>? SpellLearned;

        /// <inheritdoc />
        public event Action<TrainerServiceData[]>? TrainerServicesReceived;

        /// <inheritdoc />
        public event Action<string>? TrainerError;

        // Reactive observable properties
        public IObservable<(ulong TrainerGuid, TrainerServiceData[] Services)> TrainerWindowsOpened => _trainerWindowsOpened;
        public IObservable<Unit> TrainerWindowsClosed => _trainerWindowsClosed;
        public IObservable<(uint SpellId, uint Cost)> SpellsLearned => _spellsLearned;
        public IObservable<TrainerServiceData[]> TrainerServicesUpdated => _trainerServicesUpdated;
        public IObservable<string> TrainerErrors => _trainerErrors;

        /// <inheritdoc />
        public async Task OpenTrainerAsync(ulong trainerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Opening trainer interaction with: {TrainerGuid:X}", trainerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(trainerGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);

                // Optimistically set current trainer context so subsequent calls can proceed
                _currentTrainerGuid = trainerGuid;
                _isTrainerWindowOpen = true;

                _logger.LogInformation("Trainer interaction initiated with: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open trainer interaction with: {TrainerGuid:X}", trainerGuid);
                TrainerError?.Invoke($"Failed to open trainer: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task GetTrainerServicesAsync(CancellationToken cancellationToken = default)
        {
            if (!_currentTrainerGuid.HasValue)
            {
                var error = "No trainer is currently open";
                _logger.LogWarning(error);
                TrainerError?.Invoke(error);
                return;
            }

            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Requesting trainer services from: {TrainerGuid:X}", _currentTrainerGuid.Value);

                var payload = new byte[8];
                BitConverter.GetBytes(_currentTrainerGuid.Value).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TRAINER_LIST, payload, cancellationToken);

                _logger.LogInformation("Trainer services request sent to: {TrainerGuid:X}", _currentTrainerGuid.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request trainer services from: {TrainerGuid:X}", _currentTrainerGuid);
                TrainerError?.Invoke($"Failed to request trainer services: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task RequestTrainerServicesAsync(ulong trainerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Requesting trainer services from: {TrainerGuid:X}", trainerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(trainerGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TRAINER_LIST, payload, cancellationToken);

                _logger.LogInformation("Trainer services request sent to: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request trainer services from: {TrainerGuid:X}", trainerGuid);
                TrainerError?.Invoke($"Failed to request trainer services: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task LearnSpellAsync(uint spellId, CancellationToken cancellationToken = default)
        {
            if (!_currentTrainerGuid.HasValue)
            {
                var error = "No trainer is currently open";
                _logger.LogWarning(error);
                TrainerError?.Invoke(error);
                return;
            }

            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Learning spell {SpellId} from trainer: {TrainerGuid:X}", spellId, _currentTrainerGuid.Value);

                var payload = new byte[12];
                BitConverter.GetBytes(_currentTrainerGuid.Value).CopyTo(payload, 0);
                BitConverter.GetBytes(spellId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TRAINER_BUY_SPELL, payload, cancellationToken);

                _logger.LogInformation("Learn spell request sent for spell {SpellId} from trainer: {TrainerGuid:X}", spellId, _currentTrainerGuid.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to learn spell {SpellId} from trainer: {TrainerGuid:X}", spellId, _currentTrainerGuid);
                TrainerError?.Invoke($"Failed to learn spell: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task LearnSpellAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Learning spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(trainerGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(spellId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TRAINER_BUY_SPELL, payload, cancellationToken);

                _logger.LogInformation("Learn spell request sent for spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to learn spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);
                TrainerError?.Invoke($"Failed to learn spell: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task CloseTrainerAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Closing trainer window");

                // Trainer windows typically close automatically when moving away
                // But we can update our internal state
                _isTrainerWindowOpen = false;
                _currentTrainerGuid = null;
                _availableServices.Clear();
                TrainerWindowClosed?.Invoke();

                _logger.LogInformation("Trainer window closed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close trainer window");
                TrainerError?.Invoke($"Failed to close trainer: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public TrainerServiceData[] GetAvailableServices()
        {
            return _availableServices.Where(s => s.CanLearn).ToArray();
        }

        /// <inheritdoc />
        public TrainerServiceData[] GetAffordableServices(uint currentMoney)
        {
            return _availableServices.Where(s => s.CanLearn && s.Cost <= currentMoney).ToArray();
        }

        /// <inheritdoc />
        public bool IsSpellAvailable(uint spellId)
        {
            return _availableServices.Any(s => s.SpellId == spellId && s.CanLearn);
        }

        /// <inheritdoc />
        public uint? GetSpellCost(uint spellId)
        {
            var service = _availableServices.FirstOrDefault(s => s.SpellId == spellId);
            return service?.Cost;
        }

        /// <inheritdoc />
        public bool IsTrainerOpen(ulong trainerGuid)
        {
            return _isTrainerWindowOpen && _currentTrainerGuid == trainerGuid;
        }

        /// <inheritdoc />
        public async Task LearnSpellByIndexAsync(ulong trainerGuid, uint serviceIndex, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Learning spell by index {ServiceIndex} from trainer: {TrainerGuid:X}", serviceIndex, trainerGuid);

                // Find the spell ID for this index
                var service = _availableServices.FirstOrDefault(s => s.ServiceIndex == serviceIndex);
                if (service == null)
                {
                    var errorMsg = $"Service index {serviceIndex} not found in available services";
                    _logger.LogWarning(errorMsg);
                    TrainerError?.Invoke(errorMsg);
                    return;
                }

                await LearnSpellAsync(trainerGuid, service.SpellId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to learn spell by index {ServiceIndex} from trainer: {TrainerGuid:X}", serviceIndex, trainerGuid);
                TrainerError?.Invoke($"Failed to learn spell by index: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task QuickLearnSpellAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Performing quick learn of spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);

                await OpenTrainerAsync(trainerGuid, cancellationToken);
                await RequestTrainerServicesAsync(trainerGuid, cancellationToken);
                
                // Small delay to allow trainer window to open and services to load
                await Task.Delay(200, cancellationToken);
                
                await LearnSpellAsync(spellId, cancellationToken);
                
                // Small delay to allow learning to complete
                await Task.Delay(100, cancellationToken);
                
                await CloseTrainerAsync(cancellationToken);

                _logger.LogInformation("Quick learn completed for spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick learn failed for spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);
                TrainerError?.Invoke($"Quick learn failed: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task LearnMultipleSpellsAsync(ulong trainerGuid, uint[] spellIds, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Learning multiple spells from trainer: {TrainerGuid:X}. Spells: [{SpellIds}]", 
                    trainerGuid, string.Join(", ", spellIds));

                await OpenTrainerAsync(trainerGuid, cancellationToken);
                await RequestTrainerServicesAsync(trainerGuid, cancellationToken);
                
                // Small delay to allow trainer window to open and services to load
                await Task.Delay(200, cancellationToken);

                foreach (var spellId in spellIds)
                {
                    try
                    {
                        await LearnSpellAsync(spellId, cancellationToken);
                        
                        // Small delay between spell learning attempts
                        await Task.Delay(100, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to learn spell {SpellId}, continuing with next spell", spellId);
                        // Continue with other spells even if one fails
                    }
                }
                
                await CloseTrainerAsync(cancellationToken);

                _logger.LogInformation("Multiple spell learning completed from trainer: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multiple spell learning failed from trainer: {TrainerGuid:X}", trainerGuid);
                TrainerError?.Invoke($"Multiple spell learning failed: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public void UpdateTrainerServices(TrainerServiceData[] services)
        {
            _availableServices.Clear();
            _availableServices.AddRange(services);
            TrainerServicesReceived?.Invoke(services);
            _logger.LogDebug("Trainer services updated: {ServiceCount} services", services.Length);
        }

        #endregion

        #region Server Response Handlers

        /// <summary>
        /// Handles server responses for trainer window opening.
        /// This method should be called when SMSG_TRAINER_LIST is received.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer.</param>
        /// <param name="services">The available trainer services.</param>
        public void HandleTrainerWindowOpened(ulong trainerGuid, TrainerServiceData[] services)
        {
            _isTrainerWindowOpen = true;
            _currentTrainerGuid = trainerGuid;
            _availableServices.Clear();
            _availableServices.AddRange(services);
            
            TrainerWindowOpened?.Invoke(trainerGuid);
            TrainerServicesReceived?.Invoke(services);
            
            _logger.LogDebug("Trainer window opened for: {TrainerGuid:X} with {ServiceCount} services", 
                trainerGuid, services.Length);
        }

        /// <summary>
        /// Handles server responses for successful spell learning.
        /// This method should be called when SMSG_TRAINER_BUY_SUCCEEDED is received.
        /// </summary>
        /// <param name="spellId">The ID of the learned spell.</param>
        /// <param name="cost">The cost in copper.</param>
        public void HandleSpellLearned(uint spellId, uint cost)
        {
            SpellLearned?.Invoke(spellId, cost);
            _logger.LogDebug("Spell learned: {SpellId} (cost: {Cost})", spellId, cost);
        }

        /// <summary>
        /// Handles server responses for trainer operation failures.
        /// This method should be called when SMSG_TRAINER_BUY_FAILED or similar error responses are received.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        public void HandleTrainerError(string errorMessage)
        {
            TrainerError?.Invoke(errorMessage);
            _logger.LogWarning("Trainer operation failed: {Error}", errorMessage);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the trainer network client component and cleans up resources.
        /// </summary>
        public override void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing TrainerNetworkClientComponent");

            // Clear events to prevent memory leaks
            TrainerWindowOpened = null;
            TrainerWindowClosed = null;
            SpellLearned = null;
            TrainerServicesReceived = null;
            TrainerError = null;

            _disposed = true;
            _logger.LogDebug("TrainerNetworkClientComponent disposed");
            base.Dispose();
        }

        #endregion
    }
}