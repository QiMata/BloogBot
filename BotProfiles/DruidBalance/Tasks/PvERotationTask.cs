using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace DruidBalance.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {
        private const string Starfire = "Starfire";
        private const string EclipseSolar = "Eclipse (Solar)";
        private const string EclipseLunar = "Eclipse (Lunar)";
        private static readonly string[] ImmuneToNatureDamage = ["Vortex", "Whirlwind", "Whirling", "Dust", "Cyclone"];
        private IWoWUnit secondaryTarget;
        private bool castingEntanglingRoots;
        private bool backpedaling;
        private int backpedalStartTime;

        private Action EntanglingRootsCallback => () =>
        {
            castingEntanglingRoots = true;
        };

        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (castingEntanglingRoots)
            {
                if (secondaryTarget.HasDebuff(EntanglingRoots))
                {
                    backpedaling = true;
                    backpedalStartTime = Environment.TickCount;
                    ObjectManager.Player.StartMovement(ControlBits.Back);
                }

                ObjectManager.SetTarget(ObjectManager.GetTarget(ObjectManager.Player).Guid);
                castingEntanglingRoots = false;
            }

            // handle backpedaling during entangling roots
            if (Environment.TickCount - backpedalStartTime > 1500)
            {
                ObjectManager.Player.StopMovement(ControlBits.Back);
                backpedaling = false;
            }
            if (backpedaling)
                return;

            // heal self if we're injured
            if (ObjectManager.Player.HealthPercent < 30 && (ObjectManager.Player.Mana >= ObjectManager.GetManaCost(HealingTouch) || ObjectManager.Player.Mana >= ObjectManager.GetManaCost(Rejuvenation)))
            {
                Wait.RemoveAll();
                BotTasks.Push(new HealTask(BotContext));
                return;
            }

            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                ObjectManager.SetTarget(ObjectManager.Aggressors.First().Guid);
            }

            if (Update(30))
                return;

            // if we get an add, root it with Entangling Roots
            if (ObjectManager.Aggressors.Count() == 2 && secondaryTarget == null)
                secondaryTarget = ObjectManager.Aggressors.Single(u => u.Guid != ObjectManager.GetTarget(ObjectManager.Player).Guid);

            if (secondaryTarget != null && !secondaryTarget.HasDebuff(EntanglingRoots))
            {
                ObjectManager.SetTarget(secondaryTarget.Guid);
                TryCastSpell(EntanglingRoots, 0, 30, !secondaryTarget.HasDebuff(EntanglingRoots), EntanglingRootsCallback);
            }

            TryCastSpell(MoonkinForm, !ObjectManager.Player.HasBuff(MoonkinForm));

            TryCastSpell(Innervate, ObjectManager.Player.ManaPercent < 10, castOnSelf: true);

            TryCastSpell(RemoveCurse, 0, int.MaxValue, ObjectManager.Player.IsCursed && !ObjectManager.Player.HasBuff(MoonkinForm), castOnSelf: true);

            TryCastSpell(AbolishPoison, 0, int.MaxValue, ObjectManager.Player.IsPoisoned && !ObjectManager.Player.HasBuff(MoonkinForm), castOnSelf: true);

            var target = ObjectManager.GetTarget(ObjectManager.Player);

            TryCastSpell(InsectSwarm, 0, 30,
                target != null &&
                !target.HasDebuff(InsectSwarm) &&
                target.HealthPercent > 20 &&
                !ImmuneToNatureDamage.Any(s => target.Name.Contains(s)));

            TryCastSpell(Moonfire, 0, 30, target != null && !target.HasDebuff(Moonfire));

            bool lunarEclipse = ObjectManager.Player.HasBuff(EclipseLunar);
            bool solarEclipse = ObjectManager.Player.HasBuff(EclipseSolar);
            bool hasClearcasting = ObjectManager.Player.HasBuff(Clearcasting);

            TryCastSpell(Starfire, 0, 30,
                target != null &&
                (lunarEclipse || hasClearcasting) &&
                !ImmuneToNatureDamage.Any(s => target.Name.Contains(s)));

            TryCastSpell(Wrath, 0, 30,
                target != null &&
                (solarEclipse || (!lunarEclipse && !hasClearcasting)) &&
                !ImmuneToNatureDamage.Any(s => target.Name.Contains(s)));
        }

        public override void PerformCombatRotation()
        {
            
        }
    }
}
