using BotRunner.Interfaces;
using static BotRunner.Constants.Spellbook;
using GameData.Core.Models;
using BotRunner.Tasks;

namespace PriestHoly.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }


        public void Update()
        {
            BotTasks.Pop();
        }
        // Vanilla 1.12.1 priest base spell ranges
        private const float HolyFireBaseRange = 30f;
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
                if (TryCastHeal(Renew, 80, 40)) return;
                if (TryCastHeal(Heal, 60, 40)) return;
            }
            else if (ObjectManager.Player.HealthPercent < 50)
            {
                TryCastSpell(Renew, 0, int.MaxValue, castOnSelf: true);
            }

            TryCastSpell(HolyFire, 0, 29,
                         !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(HolyFire));
            TryCastSpell(Smite, 0, 29);
=======
                if (TryCastHeal(Renew, 80, GetSpellRange(HealBaseRange))) return;
                if (TryCastHeal(Heal, 60, GetSpellRange(HealBaseRange))) return;
            }
            else if (ObjectManager.Player.HealthPercent < 50)
            {
                TryCastSpell(Renew, condition: true, castOnSelf: true);
            }

            TryCastSpell(HolyFire, 0f, GetSpellRange(HolyFireBaseRange),
                         !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(HolyFire));
            TryCastSpell(Smite, 0f, GetSpellRange(SmiteBaseRange));
>>>>>>> cpp_physics_system
        }
    }
}
