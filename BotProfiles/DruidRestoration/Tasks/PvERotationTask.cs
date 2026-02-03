using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidRestoration.Tasks
{
    /// <summary>
    /// Basic PvE rotation for a restoration druid.
    /// Focuses on staying alive while dealing modest damage.
    /// </summary>
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            AssignDPSTarget();

            if (ObjectManager.GetTarget(ObjectManager.Player) == null)
                return;

            if (Update(30))
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            // keep ourselves alive
            TryCastSpell(Rejuvenation, 0, int.MaxValue, ObjectManager.Player.HealthPercent < 80 && !ObjectManager.Player.HasBuff(Rejuvenation), castOnSelf: true);
            TryCastSpell(HealingTouch, 0, int.MaxValue, ObjectManager.Player.HealthPercent < 60, castOnSelf: true);

            // offensive abilities
            TryCastSpell(Moonfire, 0, 30, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Moonfire));
            TryCastSpell(Wrath, 0, 30);
        }
    }
}
