using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace ShamanEnhancement.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
        public PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (Update(5))
                return;

            PerformCombatRotation();
        }
        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);
            ObjectManager.StartMeleeAttack();

            TryCastSpell(GroundingTotem, 0, int.MaxValue, ObjectManager.Aggressors.Any(a => a.IsCasting));
            TryCastSpell(EarthShock, 0, 20, ObjectManager.GetTarget(ObjectManager.Player).IsCasting);
            TryCastSpell(Stormstrike, 0, 5);
        }
    }
}
