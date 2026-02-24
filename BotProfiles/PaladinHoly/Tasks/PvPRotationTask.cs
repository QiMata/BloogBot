using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PaladinHoly.Tasks
{
    public class PvPRotationTask : CombatRotationTask, IBotTask
    {
        public PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!EnsureTarget())
                return;

            if (Update(5))
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null)
                return;

            ObjectManager.StopAllMovement();
            ObjectManager.Face(target.Position);
            ObjectManager.StartMeleeAttack();

            var player = ObjectManager.Player;

            // Devotion Aura
            TryCastSpell(DevotionAura, condition: !player.HasBuff(DevotionAura), castOnSelf: true);

            // Hammer of Justice stun (interrupt or emergency)
            TryCastSpell(HammerOfJustice, 0, 10, target.IsCasting || player.HealthPercent < 30);

            // Divine Protection when low HP
            TryCastSpell(DivineProtection, condition: player.HealthPercent < 25, castOnSelf: true);

            // Lay on Hands emergency
            TryCastSpell(LayOnHands, condition: player.HealthPercent < 15, castOnSelf: true);

            // Holy Light heal
            TryCastSpell(HolyLight, condition: player.HealthPercent < 50, castOnSelf: true);

            // Purify self (remove poison/disease)
            TryCastSpell(Purify, condition: player.IsPoisoned || player.IsDiseased, castOnSelf: true);

            // Seal of Righteousness
            TryCastSpell(SealOfRighteousness, condition: !player.HasBuff(SealOfRighteousness) && !player.HasBuff(SealOfTheCrusader), castOnSelf: true);

            // Judgement
            TryCastSpell(Judgement, 0, 10);

            // Consecration for AoE
            TryCastSpell(Consecration, 0, 8, ObjectManager.Aggressors.Count() > 1);

            // Exorcism (ranged, works vs undead/demon)
            TryCastSpell(Exorcism, 0, 29);
        }
    }
}
