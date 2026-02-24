using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace BotProfiles.Common
{
    public abstract class WarlockBaseRotationTask : CombatRotationTask, IBotTask
    {
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

            TryCastSpell(LifeTap, 0, int.MaxValue,
                ObjectManager.Player.HealthPercent > 85 && ObjectManager.Player.ManaPercent < 80);

            BeforeRotation();

            var target = ObjectManager.GetTarget(ObjectManager.Player);

            if (target.HealthPercent <= 20)
            {
                ObjectManager.StopWandAttack();
                TryCastSpell(DrainSoul, 0, 29);
                return;
            }

            TryCastSpell(CurseOfAgony, 0, 28,
                !target.HasDebuff(CurseOfAgony) && target.HealthPercent > 90);

            TryCastSpell(Immolate, 0, 28,
                !target.HasDebuff(Immolate) && target.HealthPercent > 30);

            AfterImmolate();

            TryCastSpell(Corruption, 0, 28,
                !target.HasDebuff(Corruption) && target.HealthPercent > 30);

            TryCastSpell(SiphonLife, 0, 28,
                !target.HasDebuff(SiphonLife) && target.HealthPercent > 50);

            AfterDots();

            if (AllDotsActive())
                TryCastSpell(ShadowBolt, 0, 28, target.HealthPercent > 40);
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
