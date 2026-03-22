using BotRunner.Interfaces;
using BotRunner.Tasks;
<<<<<<< HEAD
=======
using GameData.Core.Models;
>>>>>>> cpp_physics_system
using static BotRunner.Constants.Spellbook;

namespace HunterBeastMastery.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
<<<<<<< HEAD
=======
        // Vanilla 1.12.1 hunter base spell ranges
        private const float RangedAttackRange = 35f;  // Auto Shot, Arcane Shot, Multi-Shot, etc.
        private const float HunterDeadZone = 8f;      // Minimum range for ranged attacks

>>>>>>> cpp_physics_system
        public PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            // Send pet to attack
            ObjectManager.Pet?.Attack();

            if (!EnsureTarget())
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null)
                return;

            var player = ObjectManager.Player;
            var distance = player.Position.DistanceTo(target.Position);
<<<<<<< HEAD
=======
            var rangedRange = GetSpellRange(RangedAttackRange);
            var meleeRange = GetMeleeRange(target);
>>>>>>> cpp_physics_system

            ObjectManager.StopAllMovement();
            ObjectManager.Face(target.Position);

            // Mend Pet when pet is low
            TryCastSpell(MendPet, condition: ObjectManager.Pet != null && ObjectManager.Pet.HealthPercent < 50, castOnSelf: true);

<<<<<<< HEAD
            if (distance > 5 && distance < 34)
=======
            if (distance > HunterDeadZone && distance < rangedRange)
>>>>>>> cpp_physics_system
            {
                // Ranged mode — slow target, debuff, and nuke

                // Concussive Shot to keep distance
<<<<<<< HEAD
                TryCastSpell(ConcussiveShot, 5, 34, !target.HasDebuff(ConcussiveShot));

                // Hunter's Mark
                TryCastSpell(HuntersMark, 5, 34, !target.HasDebuff(HuntersMark));

                // Serpent Sting
                TryCastSpell(SerpentSting, 5, 34, !target.HasDebuff(SerpentSting));
=======
                TryCastSpell(ConcussiveShot, HunterDeadZone, rangedRange, !target.HasDebuff(ConcussiveShot));

                // Hunter's Mark
                TryCastSpell(HuntersMark, HunterDeadZone, rangedRange, !target.HasDebuff(HuntersMark));

                // Serpent Sting
                TryCastSpell(SerpentSting, HunterDeadZone, rangedRange, !target.HasDebuff(SerpentSting));
>>>>>>> cpp_physics_system

                // Rapid Fire burst
                TryCastSpell(RapidFire, condition: target.HealthPercent > 60, castOnSelf: true);

                // Multi-Shot for AoE
<<<<<<< HEAD
                TryCastSpell(MultiShot, 5, 34, ObjectManager.Aggressors.Count() > 1);

                // Arcane Shot
                TryCastSpell(ArcaneShot, 5, 34);
            }
            else if (distance <= 5)
=======
                TryCastSpell(MultiShot, HunterDeadZone, rangedRange, ObjectManager.Aggressors.Count() > 1);

                // Arcane Shot
                TryCastSpell(ArcaneShot, HunterDeadZone, rangedRange);
            }
            else if (distance <= meleeRange)
>>>>>>> cpp_physics_system
            {
                // Melee mode — Wing Clip and kite away
                TryUseAbility(WingClip, 40, !target.HasDebuff(WingClip), () => StartKite(1500));

                TryUseAbility(MongooseBite, 0);
                TryUseAbility(RaptorStrike, 0);
            }
        }
    }
}
