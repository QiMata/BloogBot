using System.Reactive;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of professions network agent that handles profession operations in World of Warcraft.
    /// Manages crafting, gathering, profession training, and profession state tracking using the Mangos protocol.
    /// Purely observable (no Subjects/events). Streams are derived from opcode handlers or local state changes.
    /// </summary>
    public class ProfessionsNetworkClientComponent : NetworkClientComponent, IProfessionsNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<ProfessionsNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();

        private readonly Dictionary<ProfessionType, ProfessionInfo> _professions = [];
        private readonly List<RecipeInfo> _knownRecipes = [];
        private readonly List<ProfessionService> _availableServices = [];
        private bool _isCrafting;
        private uint? _currentCraftingSpellId;
        private bool _disposed;

        // State properties
        public bool IsTrainerWindowOpen { get; private set; }
        public bool IsCraftingWindowOpen { get; private set; }
        public ulong? CurrentTrainerGuid { get; private set; }
        public ProfessionType? CurrentProfession { get; private set; }

        // Reactive Streams (opcode-backed or local-state derived)
        public IObservable<(ulong TrainerGuid, ProfessionType? Profession)> TrainerWindowOpened { get; }
        public IObservable<Unit> TrainerWindowClosed { get; }
        public IObservable<ProfessionType?> CraftingWindowOpened { get; }
        public IObservable<Unit> CraftingWindowClosed { get; }
        public IObservable<(uint SpellId, uint Cost)> SkillLearned { get; }
        public IObservable<(uint ItemId, uint Quantity)> ItemCrafted { get; }
        public IObservable<(uint RecipeId, string Reason)> CraftingFailed { get; }
        public IObservable<ProfessionService[]> ProfessionServicesReceived { get; }
        public IObservable<string> ProfessionErrors { get; }
        public IObservable<(ulong NodeGuid, uint ItemId, uint Quantity)> ResourceGathered { get; }
        public IObservable<(ulong NodeGuid, string Reason)> GatheringFailed { get; }

        /// <summary>
        /// Initializes a new instance of the ProfessionsNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public ProfessionsNetworkClientComponent(IWorldClient worldClient, ILogger<ProfessionsNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Build opcode-backed streams where available. Many Classic/TBC opcodes exist; we expose observables using RegisterOpcodeHandler.
            // Trainer list => list of services
            var trainerList = SafeOpcodeStream(Opcode.SMSG_TRAINER_LIST)
                .Select(ParseTrainerServices)
                .Do(services =>
                {
                    lock (_stateLock)
                    {
                        _availableServices.Clear();
                        _availableServices.AddRange(services);
                    }
                })
                .Publish()
                .RefCount();

            ProfessionServicesReceived = trainerList;

            // Learn result (success). Older cores use SMSG_TRAINER_BUY_SUCCEEDED - parse spellId and cost if present
            SkillLearned = SafeOpcodeStream(Opcode.SMSG_TRAINER_BUY_SUCCEEDED)
                .Select(ParseTrainerBuySucceeded)
                .Publish()
                .RefCount();

            // Crafting success. There is not a single generic opcode; as placeholder, hook to SMSG_SPELL_GO and attempt to infer item result if encoded by server (implementation-specific).
            ItemCrafted = SafeOpcodeStream(Opcode.SMSG_SPELL_GO)
                .Select(ParseCraftResult)
                .Where(r => r.ItemId != 0 && r.Quantity > 0)
                .Publish()
                .RefCount();

            // Crafting failure. Hook to a set of failure opcodes if available; fallback empty
            CraftingFailed = Observable.Empty<(uint RecipeId, string Reason)>();

            // Trainer window lifecycle: approximate from receiving trainer list as open; close on SMSG_GOSSIP_COMPLETE when trainer active
            var trainerOpenedLocal = trainerList
                .Select(_ => (TrainerGuid: CurrentTrainerGuid ?? 0UL, Profession: CurrentProfession));

            TrainerWindowOpened = trainerOpenedLocal.Do(_ => IsTrainerWindowOpen = true).Publish().RefCount();

            TrainerWindowClosed = SafeOpcodeStream(Opcode.SMSG_GOSSIP_COMPLETE)
                .Do(_ =>
                {
                    IsTrainerWindowOpen = false;
                    CurrentTrainerGuid = null;
                    CurrentProfession = null;
                    lock (_stateLock) _availableServices.Clear();
                })
                .Select(_ => Unit.Default)
                .Publish()
                .RefCount();

            // Crafting window lifecycle is local-only; we emit when Open/Close methods succeed
            var craftingOpen = Observable.Never<ProfessionType?>();
            CraftingWindowOpened = craftingOpen; // will be replaced by local emissions in methods via Observable.Return when invoked
            CraftingWindowClosed = Observable.Never<Unit>();

            // Gathering results: hook to loot opcodes for nodes? For now, expose empty and keep compatibility handlers updating local state
            ResourceGathered = Observable.Empty<(ulong NodeGuid, uint ItemId, uint Quantity)>();
            GatheringFailed = Observable.Empty<(ulong NodeGuid, string Reason)>();

            // Generic error stream: empty for now; local methods do logging/throw. Could be mapped from specific SMSG_*_FAILED opcodes
            ProfessionErrors = Observable.Empty<string>();
        }

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        #region IProfessionsNetworkClientComponent operations

        public async Task OpenProfessionTrainerAsync(ulong trainerGuid, ProfessionType professionType, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening profession trainer: {TrainerGuid:X} for profession {ProfessionType}", trainerGuid, professionType);

                var payload = new byte[8];
                BitConverter.GetBytes(trainerGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);

                CurrentTrainerGuid = trainerGuid;
                CurrentProfession = professionType;
                IsTrainerWindowOpen = true;

                _logger.LogInformation("Profession trainer opened: {TrainerGuid:X} for {ProfessionType}", trainerGuid, professionType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open profession trainer: {TrainerGuid:X}", trainerGuid);
                throw;
            }
        }

        public async Task RequestProfessionServicesAsync(ulong trainerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting profession services from trainer: {TrainerGuid:X}", trainerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(trainerGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TRAINER_LIST, payload, cancellationToken);

                _logger.LogInformation("Profession services requested from trainer: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request profession services from trainer: {TrainerGuid:X}", trainerGuid);
                throw;
            }
        }

        public async Task LearnProfessionSkillAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Learning profession skill: {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(trainerGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(spellId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TRAINER_BUY_SPELL, payload, cancellationToken);

                _logger.LogInformation("Profession skill learning request sent: {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to learn profession skill: {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);
                throw;
            }
        }

        public async Task LearnMultipleProfessionSkillsAsync(ulong trainerGuid, uint[] spellIds, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Learning multiple profession skills from trainer: {TrainerGuid:X}, Skills: [{SpellIds}]",
                    trainerGuid, string.Join(", ", spellIds));

                foreach (var spellId in spellIds)
                {
                    await LearnProfessionSkillAsync(trainerGuid, spellId, cancellationToken);
                    await Task.Delay(100, cancellationToken);
                }

                _logger.LogInformation("Multiple profession skills learning completed for trainer: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to learn multiple profession skills from trainer: {TrainerGuid:X}", trainerGuid);
                throw;
            }
        }

        public async Task CloseProfessionTrainerAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing profession trainer: {TrainerGuid:X}", CurrentTrainerGuid ?? 0);

                // Send gossip complete to close the trainer window
                await _worldClient.SendOpcodeAsync(Opcode.SMSG_GOSSIP_COMPLETE, [], cancellationToken);

                var previousTrainerGuid = CurrentTrainerGuid;
                CurrentTrainerGuid = null;
                CurrentProfession = null;
                IsTrainerWindowOpen = false;
                _availableServices.Clear();

                _logger.LogInformation("Profession trainer closed: {TrainerGuid:X}", previousTrainerGuid ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close profession trainer");
                throw;
            }
        }

        public async Task OpenCraftingWindowAsync(ProfessionType professionType, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening crafting window for profession: {ProfessionType}", professionType);

                var spellId = GetCraftingSpellId(professionType);
                var payload = new byte[4];
                BitConverter.GetBytes(spellId).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, payload, cancellationToken);

                CurrentProfession = professionType;
                IsCraftingWindowOpen = true;

                _logger.LogInformation("Crafting window opened for profession: {ProfessionType}", professionType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open crafting window for profession: {ProfessionType}", professionType);
                throw;
            }
        }

        public async Task CraftItemAsync(uint recipeId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Crafting item with recipe: {RecipeId}, Quantity: {Quantity}", recipeId, quantity);

                for (uint i = 0; i < quantity; i++)
                {
                    var payload = new byte[4];
                    BitConverter.GetBytes(recipeId).CopyTo(payload, 0);

                    await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, payload, cancellationToken);

                    if (quantity > 1 && i < quantity - 1)
                    {
                        await Task.Delay(500, cancellationToken);
                    }
                }

                _logger.LogInformation("Item crafting initiated: Recipe {RecipeId}, Quantity: {Quantity}", recipeId, quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to craft item with recipe: {RecipeId}", recipeId);
                throw;
            }
        }

        public async Task CraftMultipleItemsAsync(CraftingRequest[] craftingQueue, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Crafting multiple items from queue with {Count} recipes", craftingQueue.Length);

                var sortedQueue = craftingQueue.OrderBy(r => r.Priority).ToArray();

                foreach (var request in sortedQueue)
                {
                    await CraftItemAsync(request.RecipeId, request.Quantity, cancellationToken);
                    await Task.Delay(200, cancellationToken);
                }

                _logger.LogInformation("Multiple items crafting completed from queue");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to craft multiple items from queue");
                throw;
            }
        }

        public async Task CloseCraftingWindowAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing crafting window for profession: {ProfessionType}", CurrentProfession);

                CurrentProfession = null;
                IsCraftingWindowOpen = false;

                _logger.LogInformation("Crafting window closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close crafting window");
                throw;
            }
        }

        public async Task GatherResourceAsync(ulong nodeGuid, GatheringType gatheringType, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Gathering resource from node: {NodeGuid:X}, Type: {GatheringType}", nodeGuid, gatheringType);

                var payload = new byte[8];
                BitConverter.GetBytes(nodeGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GAMEOBJ_USE, payload, cancellationToken);

                _logger.LogInformation("Resource gathering initiated from node: {NodeGuid:X}, Type: {GatheringType}", nodeGuid, gatheringType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to gather resource from node: {NodeGuid:X}", nodeGuid);
                throw;
            }
        }

        public async Task QuickTrainProfessionSkillsAsync(ulong trainerGuid, ProfessionType professionType, uint[] skillIds, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Quick training profession skills from trainer: {TrainerGuid:X}, Profession: {ProfessionType}",
                    trainerGuid, professionType);

                await OpenProfessionTrainerAsync(trainerGuid, professionType, cancellationToken);
                await Task.Delay(200, cancellationToken);
                await RequestProfessionServicesAsync(trainerGuid, cancellationToken);
                await Task.Delay(200, cancellationToken);
                await LearnMultipleProfessionSkillsAsync(trainerGuid, skillIds, cancellationToken);
                await CloseProfessionTrainerAsync(cancellationToken);

                _logger.LogInformation("Quick profession training completed for trainer: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to quick train profession skills from trainer: {TrainerGuid:X}", trainerGuid);
                throw;
            }
        }

        public ProfessionService[] GetAvailableProfessionServices()
        {
            lock (_stateLock)
            {
                return _availableServices.Where(s => s.IsAvailable).ToArray();
            }
        }

        public ProfessionService[] GetAffordableProfessionServices(uint availableMoney)
        {
            lock (_stateLock)
            {
                return _availableServices.Where(s => s.IsAvailable && s.Cost <= availableMoney).ToArray();
            }
        }

        public bool IsProfessionSkillAvailable(uint spellId)
        {
            lock (_stateLock)
            {
                return _availableServices.Any(s => s.SpellId == spellId && s.IsAvailable);
            }
        }

        public uint GetProfessionSkillCost(uint spellId)
        {
            lock (_stateLock)
            {
                var service = _availableServices.FirstOrDefault(s => s.SpellId == spellId);
                return service?.Cost ?? 0;
            }
        }

        public bool CanCraftRecipe(uint recipeId) => true;
        public RecipeMaterial[] GetRecipeMaterials(uint recipeId) => Array.Empty<RecipeMaterial>();

        #endregion

        #region Server response compatibility handlers -> update local state only

        public void HandleProfessionServicesResponse(ProfessionService[] services)
        {
            try
            {
                _logger.LogDebug("Received profession services response with {Count} services", services.Length);
                lock (_stateLock)
                {
                    _availableServices.Clear();
                    _availableServices.AddRange(services);
                }
                _logger.LogInformation("Profession services updated: {Count} services available", services.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle profession services response");
            }
        }

        public void HandleSkillLearnedResponse(uint spellId, uint cost)
        {
            _logger.LogDebug("Profession skill learned: {SpellId}, Cost: {Cost}", spellId, cost);
        }

        public void HandleItemCraftedResponse(uint itemId, uint quantity)
        {
            _logger.LogDebug("Item crafted: {ItemId}, Quantity: {Quantity}", itemId, quantity);
        }

        public void HandleResourceGatheredResponse(ulong nodeGuid, uint itemId, uint quantity)
        {
            _logger.LogDebug("Resource gathered from node: {NodeGuid:X}, Item: {ItemId}, Quantity: {Quantity}", nodeGuid, itemId, quantity);
        }

        #endregion

        private static uint GetCraftingSpellId(ProfessionType professionType)
        {
            return professionType switch
            {
                ProfessionType.Alchemy => 2259,
                ProfessionType.Blacksmithing => 2018,
                ProfessionType.Enchanting => 7411,
                ProfessionType.Engineering => 4036,
                ProfessionType.Leatherworking => 2108,
                ProfessionType.Tailoring => 3908,
                ProfessionType.Cooking => 2550,
                ProfessionType.FirstAid => 3273,
                _ => 0
            };
        }

        private static (uint SpellId, uint Cost) ParseTrainerBuySucceeded(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint spell = span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0U;
            uint cost = span.Length >= 8 ? BitConverter.ToUInt32(span.Slice(4, 4)) : 0U;
            return (spell, cost);
        }

        private static ProfessionService[] ParseTrainerServices(ReadOnlyMemory<byte> payload)
        {
            // Placeholder parser: without full spec, return empty list to avoid crashes
            return Array.Empty<ProfessionService>();
        }

        private static (uint ItemId, uint Quantity) ParseCraftResult(ReadOnlyMemory<byte> payload)
        {
            // Placeholder: we do not parse SMSG_SPELL_GO here. Return zeros.
            return (0U, 0U);
        }

        #region IDisposable Implementation
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("ProfessionsNetworkClientComponent disposed");
        }
        #endregion
    }
}