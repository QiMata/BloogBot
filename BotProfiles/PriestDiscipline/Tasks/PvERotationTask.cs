using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;
using GameData.Core.Models;
using BotRunner.Tasks;

namespace PriestDiscipline.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }


        public void Update()
        {
            BotTasks.Pop();
        }
        // Vanilla 1.12.1 priest base spell ranges
        private const float ShadowWordPainBaseRange = 30f;
        private const float SmiteBaseRange = 30f;
        private const float HealBaseRange = 40f;

        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            TryCastSpell(PowerWordShield, condition:
                         !ObjectManager.Player.HasDebuff(WeakenedSoul) &&
                         !ObjectManager.Player.HasBuff(PowerWordShield), castOnSelf: true);

            TryCastSpell(InnerFire, condition:
                         !ObjectManager.Player.HasBuff(InnerFire), castOnSelf: true);

            // Group healing: heal party members before DPS
            if (IsInGroup)
            {
<<<<<<< HEAD
                if (TryCastHeal(Heal, 65, 40)) return;
            }
            else if (ObjectManager.Player.HealthPercent < 60)
            {
                TryCastSpell(Heal, 0, int.MaxValue, castOnSelf: true);
            }

            TryCastSpell(ShadowWordPain, 0, 29,
                         !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(ShadowWordPain) &&
                         ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 10);

            TryCastSpell(Smite, 0, 29);
=======
                if (TryCastHeal(Heal, 65, GetSpellRange(HealBaseRange))) return;
            }
            else if (ObjectManager.Player.HealthPercent < 60)
            {
                TryCastSpell(Heal, condition: true, castOnSelf: true);
            }

            TryCastSpell(ShadowWordPain, 0f, GetSpellRange(ShadowWordPainBaseRange),
                         !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(ShadowWordPain) &&
                         ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 10);

            TryCastSpell(Smite, 0f, GetSpellRange(SmiteBaseRange));
>>>>>>> cpp_physics_system
        }
    }
}
