using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace MageFire.Tasks
{
    internal class PvERotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        private bool frostNovaBackpedaling;
        private int frostNovaBackpedalStartTime;

        public void Update()
        {
            if (frostNovaBackpedaling && Environment.TickCount - frostNovaBackpedalStartTime > 1500)
            {
                ObjectManager.Player.StopMovement(ControlBits.Back);
                frostNovaBackpedaling = false;
            }
            if (frostNovaBackpedaling)
                return;

            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                ObjectManager.Player.SetTarget(ObjectManager.Aggressors.First().Guid);
            }

            ExecuteRotation();
        }

        public override void PerformCombatRotation()
        {
            if (frostNovaBackpedaling && Environment.TickCount - frostNovaBackpedalStartTime > 1500)
            {
                ObjectManager.Player.StopMovement(ControlBits.Back);
                frostNovaBackpedaling = false;
            }
            if (frostNovaBackpedaling)
                return;

            if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                if (ObjectManager.Aggressors.Any())
                    ObjectManager.Player.SetTarget(ObjectManager.Aggressors.First().Guid);
                else
                    return;
            }

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

        private Action FrostNovaCallback => () =>
        {
            frostNovaBackpedaling = true;
            frostNovaBackpedalStartTime = Environment.TickCount;
            ObjectManager.Player.StartMovement(ControlBits.Back);
        };
    }
}
