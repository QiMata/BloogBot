using Microsoft.Extensions.Logging;
using WoWSharpClient.Agent;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using GameData.Core.Models;
using GameData.Core.Enums;

namespace WoWSharpClient.Agent
{
    /// <summary>
    /// Example implementation showing how to integrate the separated TargetingAgent and AttackAgent.
    /// This class demonstrates how to wire both agents into your BackgroundBotRunner.
    /// </summary>
    public class CombatIntegrationExample
    {
        private readonly ITargetingAgent _targetingAgent;
        private readonly IAttackAgent _attackAgent;
        private readonly ILogger<CombatIntegrationExample> _logger;

        public CombatIntegrationExample(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CombatIntegrationExample>();
            
            // Create both agents using the factory
            var (targetingAgent, attackAgent) = WoWClientFactory.CreateCombatAgents(worldClient, loggerFactory);
            _targetingAgent = targetingAgent;
            _attackAgent = attackAgent;
            
            // Subscribe to targeting events
            _targetingAgent.TargetChanged += OnTargetChanged;
            
            // Subscribe to attack events
            _attackAgent.AttackStarted += OnAttackStarted;
            _attackAgent.AttackStopped += OnAttackStopped;
            _attackAgent.AttackError += OnAttackError;
        }

        /// <summary>
        /// Example method showing how to use separated targeting and attacking.
        /// This could be called from your BackgroundBotRunner's ExecuteAsync method.
        /// </summary>
        public async Task DemonstrateCombatAsync()
        {
            try
            {
                // Example: Target a specific enemy (you would get this GUID from WoWSharpObjectManager)
                ulong enemyGuid = 0x12345678;
                
                _logger.LogInformation("Starting combat demonstration...");
                
                // Step 1: Set target using targeting agent
                await _targetingAgent.SetTargetAsync(enemyGuid);
                
                // Step 2: Start attacking using attack agent
                await _attackAgent.StartAttackAsync();
                
                // Wait for some combat
                await Task.Delay(5000);
                
                // Step 3: Stop attacking
                await _attackAgent.StopAttackAsync();
                
                // Step 4: Clear target
                await _targetingAgent.ClearTargetAsync();
                
                _logger.LogInformation("Combat demonstration completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during combat demonstration");
            }
        }

        /// <summary>
        /// Convenience method that combines targeting and attacking in one call.
        /// Uses the AttackAgent's built-in coordination with the targeting agent.
        /// </summary>
        /// <param name="targetGuid">The GUID of the target to attack.</param>
        public async Task AttackTargetAsync(ulong targetGuid)
        {
            try
            {
                await _attackAgent.AttackTargetAsync(targetGuid, _targetingAgent);
                _logger.LogInformation($"Started attacking target: {targetGuid:X}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error attacking target {targetGuid:X}");
            }
        }

        /// <summary>
        /// Assists another player by targeting what they are targeting, then attacking.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to assist.</param>
        public async Task AssistPlayerAsync(ulong playerGuid)
        {
            try
            {
                // Use targeting agent to assist the player
                await _targetingAgent.AssistAsync(playerGuid);
                
                // Small delay to let the server update our target
                await Task.Delay(200);
                
                // If we now have a target (from the assist), start attacking
                if (_targetingAgent.HasTarget())
                {
                    await _attackAgent.StartAttackAsync();
                    _logger.LogInformation($"Assisting player {playerGuid:X} and attacking their target");
                }
                else
                {
                    _logger.LogWarning($"Assist failed - player {playerGuid:X} has no target");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assisting player {playerGuid:X}");
            }
        }

        /// <summary>
        /// Toggles attack state. If attacking, stops. If not attacking, starts.
        /// </summary>
        public async Task ToggleAttackAsync()
        {
            try
            {
                await _attackAgent.ToggleAttackAsync();
                _logger.LogInformation($"Toggled attack state. Now attacking: {_attackAgent.IsAttacking}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling attack state");
            }
        }

        /// <summary>
        /// Example of how to find targets using WoWSharpObjectManager and attack them.
        /// This would be integrated with your bot's AI logic.
        /// </summary>
        public async Task FindAndAttackNearestEnemyAsync()
        {
            try
            {
                var player = WoWSharpObjectManager.Instance.Player;
                if (player?.Position == null)
                {
                    _logger.LogWarning("Player position not available");
                    return;
                }

                // Get nearby hostile units
                var nearbyEnemies = WoWSharpObjectManager.Instance.Objects
                    .OfType<WoWUnit>()
                    .Where(u => IsHostileUnit(u) && u.Position.DistanceTo(player.Position) < 30)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .ToList();

                if (nearbyEnemies.Any())
                {
                    var target = nearbyEnemies.First();
                    _logger.LogInformation($"Found nearest enemy: {target.Guid:X} at distance {target.Position.DistanceTo(player.Position):F2}");
                    
                    // Use the convenience method to attack the target
                    await _attackAgent.AttackTargetAsync(target.Guid, _targetingAgent);
                }
                else
                {
                    _logger.LogInformation("No nearby enemies found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding and attacking nearest enemy");
            }
        }

        /// <summary>
        /// Demonstrates pure targeting without attacking.
        /// </summary>
        /// <param name="targetGuid">The GUID to target.</param>
        public async Task JustTargetAsync(ulong targetGuid)
        {
            try
            {
                await _targetingAgent.SetTargetAsync(targetGuid);
                _logger.LogInformation($"Set target to: {targetGuid:X}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting target {targetGuid:X}");
            }
        }

        /// <summary>
        /// Demonstrates attacking current target without changing target.
        /// </summary>
        public async Task AttackCurrentTargetAsync()
        {
            try
            {
                if (!_targetingAgent.HasTarget())
                {
                    _logger.LogWarning("No target selected");
                    return;
                }

                await _attackAgent.StartAttackAsync();
                _logger.LogInformation($"Started attacking current target: {_targetingAgent.CurrentTarget:X}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attacking current target");
            }
        }

        /// <summary>
        /// Determines if a unit is hostile based on UnitReaction.
        /// </summary>
        /// <param name="unit">The unit to check.</param>
        /// <returns>True if the unit is hostile, false otherwise.</returns>
        private static bool IsHostileUnit(WoWUnit unit)
        {
            return unit.UnitReaction == UnitReaction.Hostile ||
                   unit.UnitReaction == UnitReaction.Unfriendly;
        }

        private void OnTargetChanged(ulong? newTarget)
        {
            if (newTarget.HasValue)
            {
                _logger.LogInformation($"Target changed to: {newTarget.Value:X}");
            }
            else
            {
                _logger.LogInformation("Target cleared");
                
                // If target is cleared while attacking, stop attacking
                if (_attackAgent.IsAttacking)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _attackAgent.StopAttackAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error stopping attack when target was cleared");
                        }
                    });
                }
            }
        }

        private void OnAttackStarted(ulong targetGuid)
        {
            _logger.LogInformation($"Started attacking target: {targetGuid:X}");
        }

        private void OnAttackStopped()
        {
            _logger.LogInformation("Stopped attacking");
        }

        private void OnAttackError(string errorMessage)
        {
            _logger.LogWarning($"Attack error: {errorMessage}");
        }

        /// <summary>
        /// Gets the current target information.
        /// </summary>
        /// <returns>The current target GUID, or null if no target.</returns>
        public ulong? GetCurrentTarget() => _targetingAgent.CurrentTarget;

        /// <summary>
        /// Gets whether the character is currently attacking.
        /// </summary>
        /// <returns>True if attacking, false otherwise.</returns>
        public bool IsAttacking() => _attackAgent.IsAttacking;

        /// <summary>
        /// Gets whether a target is currently selected.
        /// </summary>
        /// <returns>True if a target is selected, false otherwise.</returns>
        public bool HasTarget() => _targetingAgent.HasTarget();
    }
}