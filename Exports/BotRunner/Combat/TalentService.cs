using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WoWSharpClient.Networking.ClientComponents.I;

namespace BotRunner.Combat
{
    public interface ITalentService
    {
        /// <summary>
        /// Allocate all available talent points according to the configured build for the given class/spec.
        /// Returns the number of points allocated.
        /// </summary>
        Task<int> AllocateAvailablePointsAsync(string classSpecName, CancellationToken cancellationToken);
    }

    public class TalentService : ITalentService
    {
        private readonly IAgentFactory _agentFactory;

        public TalentService(IAgentFactory agentFactory)
        {
            _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        }

        public async Task<int> AllocateAvailablePointsAsync(string classSpecName, CancellationToken cancellationToken)
        {
            var talentAgent = _agentFactory.TalentAgent;
            uint available = talentAgent.AvailableTalentPoints;
            if (available == 0) return 0;

            // WWOW_TALENT_BUILD env var overrides the default class/spec build name.
            // Set by StateManager from CharacterBuildConfig.TalentBuildName.
            var effectiveBuildName = TalentBuildResolver.ResolveEffectiveBuildName(classSpecName);

            var buildOrder = TalentBuildDefinitions.GetBuild(effectiveBuildName);
            if (buildOrder == null)
            {
                Log.Warning("[TALENT] No build definition for '{BuildName}' (classSpec={ClassSpec})",
                    effectiveBuildName, classSpecName);
                return 0;
            }

            uint spent = talentAgent.TotalTalentPointsSpent;
            int allocated = 0;

            Log.Information("[TALENT] Allocating {Available} points for {BuildName} (already spent: {Spent})",
                available, effectiveBuildName, spent);

            for (int i = (int)spent; i < buildOrder.Length && allocated < (int)available; i++)
            {
                var (tab, pos) = buildOrder[i];
                try
                {
                    await talentAgent.LearnTalentByPositionAsync(tab, pos, cancellationToken);
                    allocated++;
                    await Task.Delay(200, cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Warning("[TALENT] Failed to learn talent at tab {Tab}, pos {Pos}: {Error}",
                        tab, pos, ex.Message);
                    break;
                }
            }

            Log.Information("[TALENT] Allocated {Count} talent points", allocated);
            return allocated;
        }
    }

    /// <summary>
    /// Lazy-resolved talent service for the background bot.
    /// Wraps a Func that resolves the IAgentFactory when available.
    /// </summary>
    public class DynamicTalentService : ITalentService
    {
        private readonly Func<IAgentFactory?> _agentFactoryAccessor;

        public DynamicTalentService(Func<IAgentFactory?> agentFactoryAccessor)
        {
            _agentFactoryAccessor = agentFactoryAccessor ?? throw new ArgumentNullException(nameof(agentFactoryAccessor));
        }

        public async Task<int> AllocateAvailablePointsAsync(string classSpecName, CancellationToken cancellationToken)
        {
            var factory = _agentFactoryAccessor();
            if (factory == null) return 0;

            var talentAgent = factory.TalentAgent;
            uint available = talentAgent.AvailableTalentPoints;
            if (available == 0) return 0;

            // WWOW_TALENT_BUILD env var overrides the default class/spec build name.
            var effectiveBuildName = TalentBuildResolver.ResolveEffectiveBuildName(classSpecName);

            var buildOrder = TalentBuildDefinitions.GetBuild(effectiveBuildName);
            if (buildOrder == null)
            {
                Log.Warning("[TALENT] No build definition for '{BuildName}' (classSpec={ClassSpec})",
                    effectiveBuildName, classSpecName);
                return 0;
            }

            uint spent = talentAgent.TotalTalentPointsSpent;
            int allocated = 0;

            Log.Information("[TALENT] Allocating {Available} points for {BuildName} (already spent: {Spent})",
                available, effectiveBuildName, spent);

            for (int i = (int)spent; i < buildOrder.Length && allocated < (int)available; i++)
            {
                var (tab, pos) = buildOrder[i];
                try
                {
                    await talentAgent.LearnTalentByPositionAsync(tab, pos, cancellationToken);
                    allocated++;
                    await Task.Delay(200, cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Warning("[TALENT] Failed to learn talent at tab {Tab}, pos {Pos}: {Error}",
                        tab, pos, ex.Message);
                    break;
                }
            }

            Log.Information("[TALENT] Allocated {Count} talent points", allocated);
            return allocated;
        }
    }

    internal static class TalentBuildResolver
    {
        /// <summary>
        /// Returns the WWOW_TALENT_BUILD env var value if set, otherwise falls back to the provided classSpecName.
        /// </summary>
        internal static string ResolveEffectiveBuildName(string classSpecName)
        {
            var envOverride = Environment.GetEnvironmentVariable("WWOW_TALENT_BUILD");
            if (!string.IsNullOrEmpty(envOverride))
            {
                Log.Information("[TALENT] Using WWOW_TALENT_BUILD override: '{BuildName}' (default was '{ClassSpec}')",
                    envOverride, classSpecName);
                return envOverride;
            }
            return classSpecName;
        }
    }
}
