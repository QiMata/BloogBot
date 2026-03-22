using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using static BotRunner.Constants.Spellbook;

namespace BotProfiles.Common
{
    public abstract class WarlockBaseRotationTask : CombatRotationTask, IBotTask
    {
        // Vanilla 1.12.1 warlock base spell ranges
        protected const float ShadowBoltBaseRange = 30f;
        protected const float CurseBaseRange = 30f;      // Curse of Agony, etc.
        protected const float ImmolateBaseRange = 30f;
        protected const float CorruptionBaseRange = 30f;
        protected const float SiphonLifeBaseRange = 30f;
        protected const float DrainSoulBaseRange = 30f;
        protected const float FearBaseRange = 20f;
        protected const float DeathCoilBaseRange = 30f;
        protected const float ConflagrateBaseRange = 30f;
        protected const float HauntBaseRange = 30f;

        protected WarlockBaseRotationTask(IBotContext botContext) : base(botContext) { }

        protected virtual IEnumerable<string> DotSpells =>
            new[] { CurseOfAgony, Immolate, Corruption, SiphonLife };

        public virtual void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            AssignDPSTarget();

            if (ObjectManager.GetTarget(ObjectManager.Player) == null)
                return;
        }

        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);
            ObjectManager.Pet?.Attack();

            UseCooldowns();

            if (ObjectManager.Pet != null)
            {
                if (ObjectManager.Player.HealthPercent < 40 && ObjectManager.Pet.CanUse(Sacrifice))
                    ObjectManager.Pet.Cast(Sacrifice);

                if (ObjectManager.Pet.CanUse(Torment))
                    ObjectManager.Pet.Cast(Torment);
            }

            TryCastSpell(LifeTap, condition:
                ObjectManager.Player.HealthPercent > 85 && ObjectManager.Player.ManaPercent < 80, castOnSelf: true);

            BeforeRotation();

            var target = ObjectManager.GetTarget(ObjectManager.Player);

            if (target.HealthPercent <= 20)
            {
                ObjectManager.StopWandAttack();
                TryCastSpell(DrainSoul, 0f, GetSpellRange(DrainSoulBaseRange));
                return;
            }

            TryCastSpell(CurseOfAgony, 0f, GetSpellRange(CurseBaseRange),
                !target.HasDebuff(CurseOfAgony) && target.HealthPercent > 90);

            TryCastSpell(Immolate, 0f, GetSpellRange(ImmolateBaseRange),
                !target.HasDebuff(Immolate) && target.HealthPercent > 30);

            AfterImmolate();

            TryCastSpell(Corruption, 0f, GetSpellRange(CorruptionBaseRange),
                !target.HasDebuff(Corruption) && target.HealthPercent > 30);

            TryCastSpell(SiphonLife, 0f, GetSpellRange(SiphonLifeBaseRange),
                !target.HasDebuff(SiphonLife) && target.HealthPercent > 50);

            AfterDots();

            if (AllDotsActive())
                TryCastSpell(ShadowBolt, 0f, GetSpellRange(ShadowBoltBaseRange), target.HealthPercent > 40);
        }

        protected virtual void BeforeRotation() { }
        protected virtual void AfterImmolate() { }
        protected virtual void AfterDots() { }

        protected bool ShouldReapply(string debuff)
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            return target != null && !target.HasDebuff(debuff);
        }

        private bool AllDotsActive()
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null) return false;
            return DotSpells.All(d => ObjectManager.IsSpellReady(d) && target.HasDebuff(d));
        }

        private void UseCooldowns()
        {
            if (ObjectManager.IsSpellReady(BloodFury))
                ObjectManager.CastSpell(BloodFury);

            if (ObjectManager.IsSpellReady(Berserking))
                ObjectManager.CastSpell(Berserking);
        }
    }
}
