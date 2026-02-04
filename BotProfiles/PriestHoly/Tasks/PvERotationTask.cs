using BotRunner.Interfaces;
using BotRunner.Tasks;

namespace PriestHoly.Tasks
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
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            TryCastSpell(PowerWordShield, 0, int.MaxValue,
                         !ObjectManager.Player.HasDebuff(WeakenedSoul) &&
                         !ObjectManager.Player.HasBuff(PowerWordShield), castOnSelf: true);

            TryCastSpell(InnerFire, 0, int.MaxValue,
                         !ObjectManager.Player.HasBuff(InnerFire));

            TryCastSpell(HolyFire, 0, 29,
                         !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(HolyFire));
            TryCastSpell(Smite, 0, 29);

            if (ObjectManager.Player.HealthPercent < 50)
                TryCastSpell(Renew, 0, int.MaxValue, castOnSelf: true);
        }
    }
}
