using GameData.Core.Interfaces;
using WoWSharpClient.Networking.ClientComponents.I;

namespace BotRunner.Combat
{
    public interface ITargetEngagementService
    {
        ulong? CurrentTargetGuid { get; }
        Task EngageAsync(IWoWUnit target, CancellationToken cancellationToken);
    }

    public class TargetEngagementService : ITargetEngagementService
    {
        private readonly IAgentFactory _agentFactory;
        private readonly BotCombatState _combatState;

        public TargetEngagementService(IAgentFactory agentFactory, BotCombatState combatState)
        {
            _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
            _combatState = combatState ?? throw new ArgumentNullException(nameof(combatState));
        }

        public ulong? CurrentTargetGuid => _combatState.CurrentTargetGuid;

        public async Task EngageAsync(IWoWUnit target, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(target);

            var targetGuid = target.Guid;

            if (!_agentFactory.TargetingAgent.IsTargeted(targetGuid))
            {
                await _agentFactory.AttackAgent.AttackTargetAsync(targetGuid, _agentFactory.TargetingAgent, cancellationToken);
            }
            else if (!_agentFactory.AttackAgent.IsAttacking)
            {
                await _agentFactory.AttackAgent.StartAttackAsync(cancellationToken);
            }

            _combatState.SetCurrentTarget(targetGuid);
        }
    }
}
