using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace WarlockAffliction.Tasks
{
    internal class PvPRotationTask : CombatRotationTask, IBotTask
    {
        internal PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            AssignDPSTarget();

            if (ObjectManager.GetTarget(ObjectManager.Player) == null) return;
        }

        public override void PerformCombatRotation()
        {
            ObjectManager.Player.StopAllMovement();
            ObjectManager.Player.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);
            ObjectManager.Pet?.Attack();

            // crowd control / interrupt abilities
            TryCastSpell(DeathCoil, 0, 20, ObjectManager.GetTarget(ObjectManager.Player).IsCasting);
            TryCastSpell(Fear, 0, 20,
                (ObjectManager.GetTarget(ObjectManager.Player).IsCasting ||
                 ObjectManager.GetTarget(ObjectManager.Player).IsChanneling) &&
                !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Fear));

            TryCastSpell(LifeTap, 0, int.MaxValue,
                ObjectManager.Player.HealthPercent > 85 && ObjectManager.Player.ManaPercent < 80);

            if (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 20)
            {
                ObjectManager.Player.StopWand();
                TryCastSpell(DrainSoul, 0, 29);
            }
            else
            {
                TryCastSpell(CurseOfAgony, 0, 28,
                    !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(CurseOfAgony) &&
                    ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 90);

                TryCastSpell(Immolate, 0, 28,
                    !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Immolate) &&
                    ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 30);

                TryCastSpell(Corruption, 0, 28,
                    !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Corruption) &&
                    ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 30);

                TryCastSpell(SiphonLife, 0, 28,
                    !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(SiphonLife) &&
                    ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 50);

                TryCastSpell(ShadowBolt, 0, 28,
                    ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 40);
            }
        }
    }
}
