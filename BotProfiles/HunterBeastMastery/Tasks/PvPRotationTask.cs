using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace HunterBeastMastery.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
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

            ObjectManager.StopAllMovement();
            ObjectManager.Face(target.Position);

            // Mend Pet when pet is low
            TryCastSpell(MendPet, condition: ObjectManager.Pet != null && ObjectManager.Pet.HealthPercent < 50, castOnSelf: true);

            if (distance > 5 && distance < 34)
            {
                // Ranged mode — slow target, debuff, and nuke

                // Concussive Shot to keep distance
                TryCastSpell(ConcussiveShot, 5, 34, !target.HasDebuff(ConcussiveShot));

                // Hunter's Mark
                TryCastSpell(HuntersMark, 5, 34, !target.HasDebuff(HuntersMark));

                // Serpent Sting
                TryCastSpell(SerpentSting, 5, 34, !target.HasDebuff(SerpentSting));

                // Rapid Fire burst
                TryCastSpell(RapidFire, condition: target.HealthPercent > 60, castOnSelf: true);

                // Multi-Shot for AoE
                TryCastSpell(MultiShot, 5, 34, ObjectManager.Aggressors.Count() > 1);

                // Arcane Shot
                TryCastSpell(ArcaneShot, 5, 34);
            }
            else if (distance <= 5)
            {
                // Melee mode — Wing Clip and kite away
                TryUseAbility(WingClip, 40, !target.HasDebuff(WingClip), () => StartKite(1500));

                TryUseAbility(MongooseBite, 0);
                TryUseAbility(RaptorStrike, 0);
            }
        }
    }
}
