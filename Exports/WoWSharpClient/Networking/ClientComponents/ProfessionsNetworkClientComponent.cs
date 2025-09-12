using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of professions network agent that handles profession operations in World of Warcraft.
    /// Manages profession skill training, crafting, and gathering interactions using the Mangos protocol.
    /// </summary>
    public class ProfessionsNetworkClientComponent : IProfessionsNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<ProfessionsNetworkClientComponent> _logger;
        private readonly List<ProfessionService> _availableServices = [];

        /// <summary>
        /// Initializes a new instance of the ProfessionsNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public ProfessionsNetworkClientComponent(IWorldClient worldClient, ILogger<ProfessionsNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsTrainerWindowOpen { get; private set; }

        /// <inheritdoc />
        public bool IsCraftingWindowOpen { get; private set; }

        /// <inheritdoc />
        public ulong? CurrentTrainerGuid { get; private set; }

        /// <inheritdoc />
        public ProfessionType? CurrentProfession { get; private set; }

        /// <inheritdoc />
        public event Action<ulong, ProfessionType>? TrainerWindowOpened;

        /// <inheritdoc />
        public event Action? TrainerWindowClosed;

        /// <inheritdoc />
        public event Action<ProfessionType>? CraftingWindowOpened;

        /// <inheritdoc />
        public event Action? CraftingWindowClosed;

        /// <inheritdoc />
        public event Action<uint, uint>? SkillLearned;

        /// <inheritdoc />
        public event Action<uint, uint>? ItemCrafted;

        /// <inheritdoc />
        public event Action<uint, string>? CraftingFailed;

        /// <inheritdoc />
        public event Action<ProfessionService[]>? ProfessionServicesReceived;

        /// <inheritdoc />
        public event Action<string>? ProfessionError;

        /// <inheritdoc />
        public event Action<ulong, uint, uint>? ResourceGathered;

        /// <inheritdoc />
        public event Action<ulong, string>? GatheringFailed;

        /// <inheritdoc />
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

                TrainerWindowOpened?.Invoke(trainerGuid, professionType);

                _logger.LogInformation("Profession trainer opened: {TrainerGuid:X} for {ProfessionType}", trainerGuid, professionType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open profession trainer: {TrainerGuid:X}", trainerGuid);
                ProfessionError?.Invoke($"Failed to open profession trainer: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
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
                ProfessionError?.Invoke($"Failed to request profession services: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
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
                ProfessionError?.Invoke($"Failed to learn profession skill {spellId}: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LearnMultipleProfessionSkillsAsync(ulong trainerGuid, uint[] spellIds, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Learning multiple profession skills from trainer: {TrainerGuid:X}, Skills: [{SpellIds}]", 
                    trainerGuid, string.Join(", ", spellIds));

                foreach (var spellId in spellIds)
                {
                    await LearnProfessionSkillAsync(trainerGuid, spellId, cancellationToken);
                    
                    // Small delay between skill learning to respect rate limits
                    await Task.Delay(100, cancellationToken);
                }

                _logger.LogInformation("Multiple profession skills learning completed for trainer: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to learn multiple profession skills from trainer: {TrainerGuid:X}", trainerGuid);
                ProfessionError?.Invoke($"Failed to learn multiple profession skills: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
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

                TrainerWindowClosed?.Invoke();

                _logger.LogInformation("Profession trainer closed: {TrainerGuid:X}", previousTrainerGuid ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close profession trainer");
                ProfessionError?.Invoke($"Failed to close profession trainer: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task OpenCraftingWindowAsync(ProfessionType professionType, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening crafting window for profession: {ProfessionType}", professionType);

                // For crafting, we would typically send a spell cast or use an action
                // This is a simplified implementation - actual crafting window opening
                // might require different opcodes based on the profession
                var spellId = GetCraftingSpellId(professionType);
                
                var payload = new byte[4];
                BitConverter.GetBytes(spellId).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, payload, cancellationToken);

                CurrentProfession = professionType;
                IsCraftingWindowOpen = true;

                CraftingWindowOpened?.Invoke(professionType);

                _logger.LogInformation("Crafting window opened for profession: {ProfessionType}", professionType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open crafting window for profession: {ProfessionType}", professionType);
                ProfessionError?.Invoke($"Failed to open crafting window: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CraftItemAsync(uint recipeId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Crafting item with recipe: {RecipeId}, Quantity: {Quantity}", recipeId, quantity);

                for (uint i = 0; i < quantity; i++)
                {
                    var payload = new byte[4];
                    BitConverter.GetBytes(recipeId).CopyTo(payload, 0);

                    // Use spell cast for crafting the recipe
                    await _worldClient.SendOpcodeAsync(Opcode.CMSG_CAST_SPELL, payload, cancellationToken);

                    if (quantity > 1 && i < quantity - 1)
                    {
                        // Small delay between crafting attempts
                        await Task.Delay(500, cancellationToken);
                    }
                }

                _logger.LogInformation("Item crafting initiated: Recipe {RecipeId}, Quantity: {Quantity}", recipeId, quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to craft item with recipe: {RecipeId}", recipeId);
                CraftingFailed?.Invoke(recipeId, ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CraftMultipleItemsAsync(CraftingRequest[] craftingQueue, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Crafting multiple items from queue with {Count} recipes", craftingQueue.Length);

                var sortedQueue = craftingQueue.OrderBy(r => r.Priority).ToArray();

                foreach (var request in sortedQueue)
                {
                    await CraftItemAsync(request.RecipeId, request.Quantity, cancellationToken);
                    
                    // Delay between different recipes
                    await Task.Delay(200, cancellationToken);
                }

                _logger.LogInformation("Multiple items crafting completed from queue");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to craft multiple items from queue");
                ProfessionError?.Invoke($"Failed to craft multiple items: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseCraftingWindowAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing crafting window for profession: {ProfessionType}", CurrentProfession);

                // Simply clear the state - crafting windows typically close automatically
                // or require UI interaction which isn't available through network packets
                CurrentProfession = null;
                IsCraftingWindowOpen = false;

                CraftingWindowClosed?.Invoke();

                _logger.LogInformation("Crafting window closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close crafting window");
                ProfessionError?.Invoke($"Failed to close crafting window: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task GatherResourceAsync(ulong nodeGuid, GatheringType gatheringType, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Gathering resource from node: {NodeGuid:X}, Type: {GatheringType}", nodeGuid, gatheringType);

                var payload = new byte[8];
                BitConverter.GetBytes(nodeGuid).CopyTo(payload, 0);

                // Use game object interaction for gathering
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GAMEOBJ_USE, payload, cancellationToken);

                _logger.LogInformation("Resource gathering initiated from node: {NodeGuid:X}, Type: {GatheringType}", nodeGuid, gatheringType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to gather resource from node: {NodeGuid:X}", nodeGuid);
                GatheringFailed?.Invoke(nodeGuid, ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickTrainProfessionSkillsAsync(ulong trainerGuid, ProfessionType professionType, uint[] skillIds, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Quick training profession skills from trainer: {TrainerGuid:X}, Profession: {ProfessionType}", 
                    trainerGuid, professionType);

                await OpenProfessionTrainerAsync(trainerGuid, professionType, cancellationToken);
                
                // Small delay to allow trainer window to open
                await Task.Delay(200, cancellationToken);
                
                await RequestProfessionServicesAsync(trainerGuid, cancellationToken);
                
                // Small delay to allow services to load
                await Task.Delay(200, cancellationToken);
                
                await LearnMultipleProfessionSkillsAsync(trainerGuid, skillIds, cancellationToken);
                
                await CloseProfessionTrainerAsync(cancellationToken);

                _logger.LogInformation("Quick profession training completed for trainer: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to quick train profession skills from trainer: {TrainerGuid:X}", trainerGuid);
                ProfessionError?.Invoke($"Failed to quick train profession skills: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public ProfessionService[] GetAvailableProfessionServices()
        {
            return _availableServices.Where(s => s.IsAvailable).ToArray();
        }

        /// <inheritdoc />
        public ProfessionService[] GetAffordableProfessionServices(uint availableMoney)
        {
            return _availableServices.Where(s => s.IsAvailable && s.Cost <= availableMoney).ToArray();
        }

        /// <inheritdoc />
        public bool IsProfessionSkillAvailable(uint spellId)
        {
            return _availableServices.Any(s => s.SpellId == spellId && s.IsAvailable);
        }

        /// <inheritdoc />
        public uint GetProfessionSkillCost(uint spellId)
        {
            var service = _availableServices.FirstOrDefault(s => s.SpellId == spellId);
            return service?.Cost ?? 0;
        }

        /// <inheritdoc />
        public bool CanCraftRecipe(uint recipeId)
        {
            // This would require checking inventory for materials
            // For now, return true as a simplified implementation
            // In a real implementation, this would check the player's inventory
            // against the recipe requirements
            return true;
        }

        /// <inheritdoc />
        public RecipeMaterial[] GetRecipeMaterials(uint recipeId)
        {
            // This would require recipe data and inventory checking
            // For now, return empty array as a simplified implementation
            // In a real implementation, this would return the actual materials needed
            return [];
        }

        /// <summary>
        /// Handles server response for profession trainer services.
        /// This method should be called by the packet handler when SMSG_TRAINER_LIST is received.
        /// </summary>
        /// <param name="services">The list of profession services from the server.</param>
        public void HandleProfessionServicesResponse(ProfessionService[] services)
        {
            try
            {
                _logger.LogDebug("Received profession services response with {Count} services", services.Length);

                _availableServices.Clear();
                _availableServices.AddRange(services);

                ProfessionServicesReceived?.Invoke(services);

                _logger.LogInformation("Profession services updated: {Count} services available", services.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle profession services response");
                ProfessionError?.Invoke($"Failed to process profession services: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles server response for profession skill learning.
        /// This method should be called by the packet handler when SMSG_TRAINER_BUY_SUCCEEDED is received.
        /// </summary>
        /// <param name="spellId">The spell ID that was learned.</param>
        /// <param name="cost">The cost that was paid.</param>
        public void HandleSkillLearnedResponse(uint spellId, uint cost)
        {
            try
            {
                _logger.LogDebug("Profession skill learned: {SpellId}, Cost: {Cost}", spellId, cost);

                SkillLearned?.Invoke(spellId, cost);

                _logger.LogInformation("Profession skill learned successfully: {SpellId} for {Cost} copper", spellId, cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle skill learned response for spell: {SpellId}", spellId);
                ProfessionError?.Invoke($"Failed to process skill learned response: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles server response for crafting operations.
        /// This method should be called by the packet handler when crafting succeeds.
        /// </summary>
        /// <param name="itemId">The item ID that was crafted.</param>
        /// <param name="quantity">The quantity that was crafted.</param>
        public void HandleItemCraftedResponse(uint itemId, uint quantity)
        {
            try
            {
                _logger.LogDebug("Item crafted: {ItemId}, Quantity: {Quantity}", itemId, quantity);

                ItemCrafted?.Invoke(itemId, quantity);

                _logger.LogInformation("Item crafted successfully: {ItemId} x{Quantity}", itemId, quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle item crafted response for item: {ItemId}", itemId);
                ProfessionError?.Invoke($"Failed to process item crafted response: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles server response for gathering operations.
        /// This method should be called by the packet handler when gathering succeeds.
        /// </summary>
        /// <param name="nodeGuid">The node GUID that was gathered from.</param>
        /// <param name="itemId">The item ID that was gathered.</param>
        /// <param name="quantity">The quantity that was gathered.</param>
        public void HandleResourceGatheredResponse(ulong nodeGuid, uint itemId, uint quantity)
        {
            try
            {
                _logger.LogDebug("Resource gathered from node: {NodeGuid:X}, Item: {ItemId}, Quantity: {Quantity}", 
                    nodeGuid, itemId, quantity);

                ResourceGathered?.Invoke(nodeGuid, itemId, quantity);

                _logger.LogInformation("Resource gathered successfully from node: {NodeGuid:X}, Item: {ItemId} x{Quantity}", 
                    nodeGuid, itemId, quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle resource gathered response for node: {NodeGuid:X}", nodeGuid);
                ProfessionError?.Invoke($"Failed to process resource gathered response: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the crafting spell ID for a profession type.
        /// This is used to open the crafting window for the specified profession.
        /// </summary>
        /// <param name="professionType">The profession type.</param>
        /// <returns>The spell ID used to open crafting for the profession.</returns>
        private static uint GetCraftingSpellId(ProfessionType professionType)
        {
            return professionType switch
            {
                ProfessionType.Alchemy => 2259,        // Alchemy
                ProfessionType.Blacksmithing => 2018,  // Blacksmithing
                ProfessionType.Enchanting => 7411,     // Enchanting
                ProfessionType.Engineering => 4036,    // Engineering
                ProfessionType.Leatherworking => 2108, // Leatherworking
                ProfessionType.Tailoring => 3908,      // Tailoring
                ProfessionType.Cooking => 2550,        // Cooking
                ProfessionType.FirstAid => 3273,       // First Aid
                _ => 0
            };
        }
    }
}