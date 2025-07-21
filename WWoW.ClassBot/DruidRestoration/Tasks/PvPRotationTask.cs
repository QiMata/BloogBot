using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidRestoration.Tasks
{
    /// <summary>
    /// Basic PvP rotation for a restoration druid. Uses simple heals and damage spells.
    /// </summary>
    internal class PvPRotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
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
            ObjectManager.Player.StopAllMovement();
            ObjectManager.Player.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            TryCastSpell(Rejuvenation, 0, int.MaxValue, ObjectManager.Player.HealthPercent < 80 && !ObjectManager.Player.HasBuff(Rejuvenation), castOnSelf: true);
            TryCastSpell(HealingTouch, 0, int.MaxValue, ObjectManager.Player.HealthPercent < 60, castOnSelf: true);

            TryCastSpell(Moonfire, 0, 30, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Moonfire));
            TryCastSpell(Wrath, 0, 30);
        }
    }
}
