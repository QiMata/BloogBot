using System;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.ClientComponents.I;

namespace BotRunner.Combat
{
    public interface ILootingService
    {
        Task<bool> TryLootAsync(ulong targetGuid, CancellationToken cancellationToken);
    }

    public class LootingService : ILootingService
    {
        private readonly IAgentFactory _agentFactory;
        private readonly BotCombatState _combatState;

        public LootingService(IAgentFactory agentFactory, BotCombatState combatState)
        {
            _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
            _combatState = combatState ?? throw new ArgumentNullException(nameof(combatState));
        }

        public async Task<bool> TryLootAsync(ulong targetGuid, CancellationToken cancellationToken)
        {
            if (targetGuid == 0 || _combatState.HasLooted(targetGuid))
            {
                return false;
            }

            await _agentFactory.LootingAgent.QuickLootAsync(targetGuid, cancellationToken);

            if (_agentFactory.AttackAgent.IsAttacking)
            {
                await _agentFactory.AttackAgent.StopAttackAsync(cancellationToken);
            }

            if (_agentFactory.TargetingAgent.HasTarget() && _agentFactory.TargetingAgent.IsTargeted(targetGuid))
            {
                await _agentFactory.TargetingAgent.ClearTargetAsync(cancellationToken);
            }

            return _combatState.TryMarkLooted(targetGuid);
        }
    }
}
