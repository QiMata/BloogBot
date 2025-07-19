using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace PriestDiscipline.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }


        public void Update()
        {
            BotTasks.Pop();
        }
        public override void PerformCombatRotation()
        {
            ObjectManager.Player.StopAllMovement();
            ObjectManager.Player.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            TryCastSpell(PowerWordShield, 0, int.MaxValue,
                         !ObjectManager.Player.HasDebuff(WeakenedSoul) &&
                         !ObjectManager.Player.HasBuff(PowerWordShield), castOnSelf: true);

            TryCastSpell(InnerFire, 0, int.MaxValue,
                         !ObjectManager.Player.HasBuff(InnerFire));

            TryCastSpell(ShadowWordPain, 0, 29,
                         !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(ShadowWordPain) &&
                         ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 10);

            TryCastSpell(Smite, 0, 29);

            if (ObjectManager.Player.HealthPercent < 60)
                TryCastSpell(Heal, 0, int.MaxValue, castOnSelf: true);
        }
    }
}
