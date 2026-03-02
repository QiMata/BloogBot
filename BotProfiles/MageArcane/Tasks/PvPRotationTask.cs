using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Models;
using static BotRunner.Constants.Spellbook;

namespace MageArcane.Tasks
{
    public class PvPRotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        // Vanilla 1.12.1 mage base spell ranges
        private const float ArcaneMissilesBaseRange = 30f;
        private const float ArcaneBarrageBaseRange = 30f;
        private const float FireBlastBaseRange = 20f;
        private const float CounterspellBaseRange = 30f;

        public void Update()
        {
            if (!EnsureTarget())
                return;

            if (IsKiting)
                return;

            if (Update(GetSpellRange(ArcaneMissilesBaseRange)))
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null)
                return;

            ObjectManager.StopAllMovement();
            ObjectManager.Face(target.Position);

            var player = ObjectManager.Player;

            // Interrupt casters
            TryCastSpell(Counterspell, 0f, GetSpellRange(CounterspellBaseRange), target.IsCasting);

            // Emergency mana shield
            TryCastSpell(ManaShield, condition: player.HealthPercent < 25 && player.ManaPercent > 20, castOnSelf: true);

            // Frost Nova + kite when target is close
            var distance = player.Position.DistanceTo(target.Position);
            TryCastSpell(FrostNova, 0f, 10f, distance < 10, callback: () => StartKite(2000));

            // Burst cooldowns
            TryCastSpell(PresenceOfMind, condition: true, castOnSelf: true);
            TryCastSpell(ArcanePower, condition: target.HealthPercent > 40, castOnSelf: true);

            // AoE for multiple aggressors
            TryCastSpell(ArcaneExplosion, 0f, 10f, ObjectManager.Aggressors.Count() > 2);

            // Primary rotation
            TryCastSpell(ArcaneBarrage, 0f, GetSpellRange(ArcaneBarrageBaseRange));
            TryCastSpell(ArcaneMissiles, 0f, GetSpellRange(ArcaneMissilesBaseRange));
            TryCastSpell(FireBlast, 0f, GetSpellRange(FireBlastBaseRange));

            // Wand fallback at very low mana
            if (player.ManaPercent < 5)
                ObjectManager.StartWandAttack();
        }
    }
}
