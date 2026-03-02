using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Models;
using static BotRunner.Constants.Spellbook;

namespace MageFire.Tasks
{
    public class PvERotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        // Vanilla 1.12.1 mage base spell ranges
        private const float FireballBaseRange = 35f;
        private const float ScorchBaseRange = 30f;
        private const float PyroblastBaseRange = 35f;
        private const float FlamestrikeBaseRange = 30f;

        public void Update()
        {
            if (IsKiting)
                return;

            if (!EnsureTarget())
                return;

            ExecuteRotation();
        }

        public override void PerformCombatRotation()
        {
            if (IsKiting)
                return;

            if (!EnsureTarget()) return;

            ExecuteRotation();
        }

        private void ExecuteRotation()
        {
            if (Update(GetSpellRange(FireballBaseRange)))
                return;

            bool multiple = ObjectManager.Aggressors.Count() > 1;

            TryCastSpell(Combustion, condition: ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 80, castOnSelf: true);

            TryCastSpell(FrostNova, 0f, 10f, multiple, callback: FrostNovaCallback);

            TryCastSpell(Flamestrike, 0f, GetSpellRange(FlamestrikeBaseRange), multiple);

            TryCastSpell(ArcaneExplosion, 0f, 10f, multiple);

            TryCastSpell(Pyroblast, 0f, GetSpellRange(PyroblastBaseRange), !ObjectManager.Player.IsInCombat);

            TryCastSpell(Scorch, 0f, GetSpellRange(ScorchBaseRange), !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Scorch));

            TryCastSpell(Fireball, 0f, GetSpellRange(FireballBaseRange));
        }

        private Action FrostNovaCallback => () => StartKite(1500);
    }
}
