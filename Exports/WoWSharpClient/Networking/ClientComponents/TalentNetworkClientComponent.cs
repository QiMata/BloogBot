using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of talent network agent that handles talent allocation and respec operations in World of Warcraft.
    /// Manages allocating talent points when leveling up and when respecing using the Mangos protocol.
    /// </summary>
    public class TalentNetworkClientComponent : ITalentNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<TalentNetworkClientComponent> _logger;

        private bool _isTalentWindowOpen;
        private uint _availableTalentPoints;
        private uint _totalTalentPointsSpent;
        private uint _respecCost;
        private readonly List<TalentTreeInfo> _talentTrees;
        private readonly Dictionary<uint, TalentInfo> _talentCache;
        private bool _awaitingRespecConfirmation;

        /// <summary>
        /// Initializes a new instance of the TalentNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public TalentNetworkClientComponent(IWorldClient worldClient, ILogger<TalentNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _talentTrees = [];
            _talentCache = new Dictionary<uint, TalentInfo>();
        }

        /// <inheritdoc />
        public bool IsTalentWindowOpen => _isTalentWindowOpen;

        /// <inheritdoc />
        public uint AvailableTalentPoints => _availableTalentPoints;

        /// <inheritdoc />
        public uint TotalTalentPointsSpent => _totalTalentPointsSpent;

        /// <inheritdoc />
        public uint RespecCost => _respecCost;

        /// <inheritdoc />
        public event Action? TalentWindowOpened;

        /// <inheritdoc />
        public event Action? TalentWindowClosed;

        /// <inheritdoc />
        public event Action<uint, uint, uint>? TalentLearned;

        /// <inheritdoc />
        public event Action<uint, uint>? TalentsUnlearned;

        /// <inheritdoc />
        public event Action<TalentTreeInfo[]>? TalentInfoReceived;

        /// <inheritdoc />
        public event Action<string>? TalentError;

        /// <inheritdoc />
        public async Task OpenTalentWindowAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening talent window");

                // Send packet to open talent frame (this might be handled by UI or through specific interaction)
                // For now, we'll mark as opened and request talent info
                _isTalentWindowOpen = true;
                TalentWindowOpened?.Invoke();

                _logger.LogInformation("Talent window opened");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open talent window");
                TalentError?.Invoke($"Failed to open talent window: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseTalentWindowAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing talent window");

                _isTalentWindowOpen = false;
                TalentWindowClosed?.Invoke();

                _logger.LogInformation("Talent window closed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close talent window");
                TalentError?.Invoke($"Failed to close talent window: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LearnTalentAsync(uint talentId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Learning talent: {TalentId}", talentId);

                if (_availableTalentPoints == 0)
                {
                    var errorMsg = "No talent points available";
                    _logger.LogWarning(errorMsg);
                    TalentError?.Invoke(errorMsg);
                    return;
                }

                if (!CanLearnTalent(talentId))
                {
                    var errorMsg = $"Cannot learn talent {talentId} - prerequisites not met";
                    _logger.LogWarning(errorMsg);
                    TalentError?.Invoke(errorMsg);
                    return;
                }

                var payload = new byte[4];
                BitConverter.GetBytes(talentId).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LEARN_TALENT, payload, cancellationToken);

                _logger.LogInformation("Learn talent request sent for talent: {TalentId}", talentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to learn talent: {TalentId}", talentId);
                TalentError?.Invoke($"Failed to learn talent: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LearnTalentByPositionAsync(uint tabIndex, uint talentIndex, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Learning talent by position: tab {TabIndex}, index {TalentIndex}", tabIndex, talentIndex);

                // Find the talent ID for this position
                var tree = _talentTrees.FirstOrDefault(t => t.TabIndex == tabIndex);
                if (tree == null)
                {
                    var errorMsg = $"Talent tree not found for tab {tabIndex}";
                    _logger.LogWarning(errorMsg);
                    TalentError?.Invoke(errorMsg);
                    return;
                }

                var talent = tree.Talents.FirstOrDefault(t => t.TalentIndex == talentIndex);
                if (talent == null)
                {
                    var errorMsg = $"Talent not found at position tab {tabIndex}, index {talentIndex}";
                    _logger.LogWarning(errorMsg);
                    TalentError?.Invoke(errorMsg);
                    return;
                }

                await LearnTalentAsync(talent.TalentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to learn talent by position: tab {TabIndex}, index {TalentIndex}", tabIndex, talentIndex);
                TalentError?.Invoke($"Failed to learn talent by position: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RequestTalentRespecAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting talent respec");

                _awaitingRespecConfirmation = true;

                // Send CMSG_UNLEARN_TALENTS - no payload needed
                var payload = Array.Empty<byte>();
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_UNLEARN_TALENTS, payload, cancellationToken);

                _logger.LogInformation("Talent respec request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request talent respec");
                TalentError?.Invoke($"Failed to request talent respec: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ConfirmTalentRespecAsync(bool confirm, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Confirming talent respec: {Confirm}", confirm);

                if (!_awaitingRespecConfirmation)
                {
                    var errorMsg = "No respec confirmation pending";
                    _logger.LogWarning(errorMsg);
                    TalentError?.Invoke(errorMsg);
                    return;
                }

                // Send MSG_TALENT_WIPE_CONFIRM with confirmation
                var payload = new byte[1];
                payload[0] = confirm ? (byte)1 : (byte)0;

                await _worldClient.SendOpcodeAsync(Opcode.MSG_TALENT_WIPE_CONFIRM, payload, cancellationToken);

                _awaitingRespecConfirmation = false;
                _logger.LogInformation("Talent respec confirmation sent: {Confirm}", confirm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm talent respec");
                TalentError?.Invoke($"Failed to confirm talent respec: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ApplyTalentBuildAsync(TalentBuild talentBuild, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Applying talent build: {BuildName}", talentBuild.Name);

                var validation = ValidateTalentBuild(talentBuild);
                if (!validation.IsValid)
                {
                    var errorMsg = $"Talent build validation failed: {string.Join(", ", validation.Errors)}";
                    _logger.LogWarning(errorMsg);
                    TalentError?.Invoke(errorMsg);
                    return;
                }

                // Sort talents by priority and learning order
                var sortedAllocations = talentBuild.Allocations
                    .OrderByDescending(a => a.Priority)
                    .ThenBy(a => Array.IndexOf(talentBuild.LearningOrder, a.TalentId))
                    .ToList();

                uint pointsUsed = 0;
                foreach (var allocation in sortedAllocations)
                {
                    if (pointsUsed >= _availableTalentPoints)
                    {
                        _logger.LogWarning("Ran out of talent points while applying build");
                        break;
                    }

                    var currentRank = GetTalentRank(allocation.TalentId);
                    var pointsNeeded = allocation.TargetRank - currentRank;

                    for (uint i = 0; i < pointsNeeded && pointsUsed < _availableTalentPoints; i++)
                    {
                        if (CanLearnTalent(allocation.TalentId))
                        {
                            await LearnTalentAsync(allocation.TalentId, cancellationToken);
                            pointsUsed++;
                            
                            // Small delay between talent learning
                            await Task.Delay(100, cancellationToken);
                        }
                        else
                        {
                            _logger.LogWarning("Cannot learn talent {TalentId} - prerequisites not met", allocation.TalentId);
                            break;
                        }
                    }
                }

                _logger.LogInformation("Talent build application completed: {BuildName}. Used {PointsUsed} points", 
                    talentBuild.Name, pointsUsed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply talent build: {BuildName}", talentBuild.Name);
                TalentError?.Invoke($"Failed to apply talent build: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public TalentTreeInfo? GetTalentTreeInfo(uint tabIndex)
        {
            return _talentTrees.FirstOrDefault(t => t.TabIndex == tabIndex);
        }

        /// <inheritdoc />
        public uint GetTalentRank(uint talentId)
        {
            return _talentCache.TryGetValue(talentId, out var talent) ? talent.CurrentRank : 0;
        }

        /// <inheritdoc />
        public bool CanLearnTalent(uint talentId)
        {
            if (_availableTalentPoints == 0)
                return false;

            if (!_talentCache.TryGetValue(talentId, out var talent))
                return false;

            if (talent.CurrentRank >= talent.MaxRank)
                return false;

            // Check tree point requirements
            var tree = _talentTrees.FirstOrDefault(t => t.TabIndex == talent.TabIndex);
            if (tree != null && tree.PointsSpent < talent.RequiredTreePoints)
                return false;

            // Check prerequisites
            foreach (var prerequisite in talent.Prerequisites)
            {
                var prereqRank = GetTalentRank(prerequisite.TalentId);
                if (prereqRank < prerequisite.RequiredRank)
                    return false;
            }

            return true;
        }

        /// <inheritdoc />
        public uint GetPointsInTree(uint tabIndex)
        {
            var tree = _talentTrees.FirstOrDefault(t => t.TabIndex == tabIndex);
            return tree?.PointsSpent ?? 0;
        }

        /// <inheritdoc />
        public TalentTreeInfo[] GetAllTalentTrees()
        {
            return _talentTrees.ToArray();
        }

        /// <inheritdoc />
        public TalentBuildValidationResult ValidateTalentBuild(TalentBuild talentBuild)
        {
            var result = new TalentBuildValidationResult
            {
                IsValid = true,
                Errors = [],
                Warnings = []
            };

            if (talentBuild.Allocations == null || talentBuild.Allocations.Length == 0)
            {
                result.IsValid = false;
                result.Errors.Add("Talent build has no allocations");
                return result;
            }

            uint totalPointsNeeded = 0;
            var treePointCounts = new Dictionary<uint, uint>();

            // Calculate total points needed and tree distributions
            foreach (var allocation in talentBuild.Allocations)
            {
                if (!_talentCache.TryGetValue(allocation.TalentId, out var talent))
                {
                    result.Warnings.Add($"Talent {allocation.TalentId} not found in cache");
                    continue;
                }

                var currentRank = GetTalentRank(allocation.TalentId);
                var pointsNeeded = allocation.TargetRank - currentRank;
                
                if (pointsNeeded < 0)
                {
                    result.Warnings.Add($"Talent {allocation.TalentId} already exceeds target rank");
                    continue;
                }

                totalPointsNeeded += pointsNeeded;
                
                if (!treePointCounts.ContainsKey(talent.TabIndex))
                    treePointCounts[talent.TabIndex] = 0;
                treePointCounts[talent.TabIndex] += pointsNeeded;
            }

            result.RequiredPoints = totalPointsNeeded;
            result.ApplicablePoints = Math.Min(totalPointsNeeded, _availableTalentPoints);

            if (totalPointsNeeded > _availableTalentPoints)
            {
                result.Warnings.Add($"Build requires {totalPointsNeeded} points but only {_availableTalentPoints} are available");
            }

            // Validate prerequisites and tree requirements
            foreach (var allocation in talentBuild.Allocations)
            {
                if (_talentCache.TryGetValue(allocation.TalentId, out var talent))
                {
                    // Check tree point requirements
                    var futureTreePoints = treePointCounts.GetValueOrDefault(talent.TabIndex, (uint)0);
                    var currentTreePoints = GetPointsInTree(talent.TabIndex);
                    
                    if (currentTreePoints + futureTreePoints < talent.RequiredTreePoints)
                    {
                        result.Errors.Add($"Talent {allocation.TalentId} requires {talent.RequiredTreePoints} points in tree {talent.TabIndex}");
                        result.IsValid = false;
                    }

                    // Check prerequisites
                    foreach (var prerequisite in talent.Prerequisites)
                    {
                        var prereqAllocation = talentBuild.Allocations.FirstOrDefault(a => a.TalentId == prerequisite.TalentId);
                        var futurePrereqRank = prereqAllocation?.TargetRank ?? GetTalentRank(prerequisite.TalentId);
                        
                        if (futurePrereqRank < prerequisite.RequiredRank)
                        {
                            result.Errors.Add($"Talent {allocation.TalentId} requires talent {prerequisite.TalentId} at rank {prerequisite.RequiredRank}");
                            result.IsValid = false;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Handles server responses for talent window opening.
        /// This method should be called when talent frame is opened or talent info is received.
        /// </summary>
        /// <param name="availablePoints">The number of available talent points.</param>
        /// <param name="spentPoints">The total number of spent talent points.</param>
        /// <param name="talentTrees">The talent tree information.</param>
        public void HandleTalentInfoReceived(uint availablePoints, uint spentPoints, TalentTreeInfo[] talentTrees)
        {
            _availableTalentPoints = availablePoints;
            _totalTalentPointsSpent = spentPoints;
            
            _talentTrees.Clear();
            _talentTrees.AddRange(talentTrees);
            
            _talentCache.Clear();
            foreach (var tree in talentTrees)
            {
                foreach (var talent in tree.Talents)
                {
                    _talentCache[talent.TalentId] = talent;
                }
            }

            TalentInfoReceived?.Invoke(talentTrees);
            
            _logger.LogDebug("Talent info received: {AvailablePoints} available, {SpentPoints} spent, {TreeCount} trees", 
                availablePoints, spentPoints, talentTrees.Length);
        }

        /// <summary>
        /// Handles server responses for successful talent learning.
        /// This method should be called when a talent is successfully learned.
        /// </summary>
        /// <param name="talentId">The ID of the learned talent.</param>
        /// <param name="currentRank">The current rank after learning.</param>
        public void HandleTalentLearned(uint talentId, uint currentRank)
        {
            if (_talentCache.TryGetValue(talentId, out var talent))
            {
                talent.CurrentRank = currentRank;
                
                // Update tree points spent
                var tree = _talentTrees.FirstOrDefault(t => t.TabIndex == talent.TabIndex);
                if (tree != null)
                {
                    tree.PointsSpent++;
                }
            }

            if (_availableTalentPoints > 0)
            {
                _availableTalentPoints--;
                _totalTalentPointsSpent++;
            }

            TalentLearned?.Invoke(talentId, currentRank, _availableTalentPoints);
            _logger.LogDebug("Talent learned: {TalentId} (rank: {CurrentRank}), {AvailablePoints} points remaining", 
                talentId, currentRank, _availableTalentPoints);
        }

        /// <summary>
        /// Handles server responses for talent respec confirmation.
        /// This method should be called when MSG_TALENT_WIPE_CONFIRM is received.
        /// </summary>
        /// <param name="cost">The cost of the respec in copper.</param>
        public void HandleRespecConfirmationRequest(uint cost)
        {
            _respecCost = cost;
            _awaitingRespecConfirmation = true;
            
            _logger.LogDebug("Talent respec confirmation requested: {Cost} copper", cost);
        }

        /// <summary>
        /// Handles server responses for successful talent respec.
        /// This method should be called when talents are successfully unlearned.
        /// </summary>
        /// <param name="cost">The cost of the respec in copper.</param>
        /// <param name="pointsRefunded">The number of talent points refunded.</param>
        public void HandleTalentsUnlearned(uint cost, uint pointsRefunded)
        {
            _availableTalentPoints += pointsRefunded;
            _totalTalentPointsSpent = 0;
            _awaitingRespecConfirmation = false;

            // Reset all talent ranks in cache
            foreach (var talent in _talentCache.Values)
            {
                talent.CurrentRank = 0;
            }

            // Reset tree points spent
            foreach (var tree in _talentTrees)
            {
                tree.PointsSpent = 0;
            }

            TalentsUnlearned?.Invoke(cost, pointsRefunded);
            _logger.LogDebug("Talents unlearned: {Cost} copper cost, {PointsRefunded} points refunded", cost, pointsRefunded);
        }

        /// <summary>
        /// Handles server responses for talent operation failures.
        /// This method should be called when talent operations fail.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        public void HandleTalentError(string errorMessage)
        {
            TalentError?.Invoke(errorMessage);
            _logger.LogWarning("Talent operation failed: {Error}", errorMessage);
        }

        /// <summary>
        /// Updates the available talent points.
        /// This can be called when the character gains or loses talent points.
        /// </summary>
        /// <param name="availablePoints">The new number of available talent points.</param>
        public void UpdateAvailableTalentPoints(uint availablePoints)
        {
            _availableTalentPoints = availablePoints;
            _logger.LogDebug("Available talent points updated: {AvailablePoints}", availablePoints);
        }
    }
}