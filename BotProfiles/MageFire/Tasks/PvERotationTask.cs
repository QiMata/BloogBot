using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace MageFire.Tasks
{
    public class PvERotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
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
            if (Update(30))
                return;

            bool multiple = ObjectManager.Aggressors.Count() > 1;

            TryCastSpell(Combustion, 0, int.MaxValue, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 80);

            TryCastSpell(FrostNova, 0, 10, multiple, callback: FrostNovaCallback);

            TryCastSpell(Flamestrike, 0, 30, multiple);

            TryCastSpell(ArcaneExplosion, 0, 10, multiple);

            TryCastSpell(Pyroblast, 0, 35, !ObjectManager.Player.IsInCombat);

            TryCastSpell(Scorch, 0, 29, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Scorch));

            TryCastSpell(Fireball, 0, 34);
        }

        private Action FrostNovaCallback => () => StartKite(1500);
    }
}
