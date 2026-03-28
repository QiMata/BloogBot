using Communication;
using WoWStateManager.Settings;
using Microsoft.Extensions.Logging;

namespace WoWStateManager.Progression
{
    public class ProgressionPlanner
    {
        private readonly ILogger<ProgressionPlanner> _logger;

        public ProgressionPlanner(ILogger<ProgressionPlanner> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Evaluate all goals and return the highest-priority action, or null if bot should self-direct.
        /// Priority order:
        ///   1. Survival (dead/ghost) — deferred to BotRunner's autonomous death recovery
        ///   2. Level-up training — visit trainer when new spells are available
        ///   3. Gear — farm BiS upgrades for high-priority slots
        ///   4. Mount — gold farm or mount purchase at level 40+
        ///   5. Gold — grind if below savings target
        ///   6. Profession — level gathering/crafting to configured targets
        ///   7. Default — null (bot self-directs)
        /// </summary>
        public ActionMessage? GetNextAction(WoWActivitySnapshot snapshot, CharacterBuildConfig? config)
        {
            if (config == null) return null;
            if (snapshot.ConnectionState != BotConnectionState.BotInWorld) return null;
            if (!snapshot.IsObjectManagerValid) return null;
            if (snapshot.IsMapTransition) return null;

            var player = snapshot.Player;
            if (player?.Unit?.GameObject == null) return null;

            // P1: Survival — handled by BotRunner's autonomous death recovery, skip here

            // P2: Level-up training
            // Future: check player level vs last-trained level, return trainer visit action
            // Requires trainer location data and spell availability tracking

            // P3: Gear — BiS gap analysis
            // Future: compare equipped items vs BiS list from BuildConfig, return farm activity

            // P4: Mount goal
            if (config.GoldTargetCopper > 0)
            {
                var currentGold = player.Coinage;
                if (currentGold < (uint)config.GoldTargetCopper)
                {
                    _logger.LogDebug("Bot {Account} needs gold: {Current}/{Target} copper",
                        snapshot.AccountName, currentGold, config.GoldTargetCopper);
                    // Don't override active actions — just log for now
                    // Future: inject grinding activity when bot is idle
                }
            }

            // P5: Gold — same check as mount for now (gold target covers both)

            // P6: Profession training
            foreach (var skillTarget in config.SkillPriorities)
            {
                var parts = skillTarget.Split(':');
                if (parts.Length != 2) continue;
                var profName = parts[0];
                if (!int.TryParse(parts[1], out var targetLevel)) continue;

                // Check if skill is below target
                // skillInfo map is keyed by skill line ID — we'd need a profession name -> skill ID mapping
                // For now, log the gap
                _logger.LogDebug("Bot {Account} has profession goal: {Prof} to {Level}",
                    snapshot.AccountName, profName, targetLevel);
            }

            return null; // No override — bot self-directs
        }
    }
}
