using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;

namespace WoWSharpClient.Agent
{
    /// <summary>
    /// Example implementation showing how to integrate the separated TargetingNetworkAgent and AttackNetworkAgent.
    /// This class demonstrates how to wire both agents into your BackgroundBotRunner.
    /// </summary>
    public class CombatIntegrationExample
    {
        private readonly ITargetingNetworkAgent _targetingAgent;
        private readonly IAttackNetworkAgent _attackAgent;
        private readonly ILogger<CombatIntegrationExample> _logger;

        public CombatIntegrationExample(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CombatIntegrationExample>();
            
            // Create both agents using the factory
            var (targetingAgent, attackAgent) = WoWClientFactory.CreateCombatNetworkAgents(worldClient, loggerFactory);
            _targetingAgent = targetingAgent;
            _attackAgent = attackAgent;

            // Wire up event handlers for coordination
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            // When target changes, log the change
            _targetingAgent.TargetChanged += OnTargetChanged;
            
            // When attack starts/stops, log the changes
            _attackAgent.AttackStarted += OnAttackStarted;
            _attackAgent.AttackStopped += OnAttackStopped;
            _attackAgent.AttackError += OnAttackError;
        }

        /// <summary>
        /// Example method showing how to engage a target in combat
        /// </summary>
        public async Task EngageCombatAsync(ulong enemyGuid)
        {
            try
            {
                _logger.LogInformation("Engaging combat with enemy: {EnemyGuid:X}", enemyGuid);

                // Use the coordinated attack method that handles both targeting and attacking
                await _attackAgent.AttackTargetAsync(enemyGuid, _targetingAgent);

                _logger.LogInformation("Combat engagement initiated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to engage combat with enemy: {EnemyGuid:X}", enemyGuid);
                throw;
            }
        }

        /// <summary>
        /// Example method showing how to stop combat
        /// </summary>
        public async Task StopCombatAsync()
        {
            try
            {
                _logger.LogInformation("Stopping combat");

                // Stop attacking first
                await _attackAgent.StopAttackAsync();

                // Then clear target
                await _targetingAgent.ClearTargetAsync();

                _logger.LogInformation("Combat stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop combat");
                throw;
            }
        }

        /// <summary>
        /// Example method showing how to assist another player
        /// </summary>
        public async Task AssistPlayerAsync(ulong playerGuid)
        {
            try
            {
                _logger.LogInformation("Assisting player: {PlayerGuid:X}", playerGuid);

                // Assist the player (this will target whatever they're targeting)
                await _targetingAgent.AssistAsync(playerGuid);

                // Small delay to ensure target is set
                await Task.Delay(100);

                // If we now have a target, start attacking
                if (_targetingAgent.HasTarget())
                {
                    await _attackAgent.StartAttackAsync();
                    _logger.LogInformation("Started attacking assist target");
                }
                else
                {
                    _logger.LogWarning("Player {PlayerGuid:X} doesn't appear to have a target", playerGuid);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assist player: {PlayerGuid:X}", playerGuid);
                throw;
            }
        }

        /// <summary>
        /// Example method showing how to toggle attack state
        /// </summary>
        public async Task ToggleCombatAsync()
        {
            try
            {
                if (_attackAgent.IsAttacking)
                {
                    _logger.LogInformation("Toggling attack off");
                    await _attackAgent.StopAttackAsync();
                }
                else
                {
                    if (_targetingAgent.HasTarget())
                    {
                        _logger.LogInformation("Toggling attack on");
                        await _attackAgent.StartAttackAsync();
                    }
                    else
                    {
                        _logger.LogWarning("Cannot start attack - no target selected");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle combat");
                throw;
            }
        }

        private void OnTargetChanged(ulong? newTarget)
        {
            if (newTarget.HasValue)
            {
                _logger.LogDebug("Target changed to: {NewTarget:X}", newTarget.Value);
            }
            else
            {
                _logger.LogDebug("Target cleared");
                
                // Optionally stop attacking when target is cleared
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
                            _logger.LogError(ex, "Failed to stop attack when target was cleared");
                        }
                    });
                }
            }
        }

        private void OnAttackStarted(ulong victimGuid)
        {
            _logger.LogInformation("Attack started on: {VictimGuid:X}", victimGuid);
        }

        private void OnAttackStopped()
        {
            _logger.LogInformation("Attack stopped");
        }

        private void OnAttackError(string error)
        {
            _logger.LogWarning("Attack error: {Error}", error);
        }

        /// <summary>
        /// Example of a more complex combat behavior
        /// </summary>
        public async Task AutoCombatAsync(ulong[] enemyGuids, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting auto-combat against {EnemyCount} enemies", enemyGuids.Length);

            foreach (var enemyGuid in enemyGuids)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    _logger.LogDebug("Engaging enemy: {EnemyGuid:X}", enemyGuid);

                    // Target and attack the enemy
                    await _attackAgent.AttackTargetAsync(enemyGuid, _targetingAgent);

                    // Simulate combat duration (in real implementation, you'd wait for combat to end)
                    await Task.Delay(2000, cancellationToken);

                    // Stop combat with this enemy
                    await _attackAgent.StopAttackAsync();

                    _logger.LogDebug("Finished combat with enemy: {EnemyGuid:X}", enemyGuid);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Auto-combat cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during auto-combat with enemy: {EnemyGuid:X}", enemyGuid);
                    // Continue with next enemy
                }
            }

            // Ensure we're not attacking anything at the end
            await StopCombatAsync();
            _logger.LogInformation("Auto-combat completed");
        }
    }
}