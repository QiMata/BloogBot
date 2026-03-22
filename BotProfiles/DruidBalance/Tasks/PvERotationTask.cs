using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using static BotRunner.Constants.Spellbook;

namespace DruidBalance.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        private const string Starfire = "Starfire";
        private const string EclipseSolar = "Eclipse (Solar)";
        private const string EclipseLunar = "Eclipse (Lunar)";
        private static readonly string[] ImmuneToNatureDamage = ["Vortex", "Whirlwind", "Whirling", "Dust", "Cyclone"];
        private IWoWUnit secondaryTarget;
        private bool castingEntanglingRoots;
<<<<<<< HEAD
=======

        // Vanilla 1.12.1 druid base spell ranges
        private const float WrathBaseRange = 30f;
        private const float MoonfireBaseRange = 30f;
        private const float StarfireBaseRange = 30f;
        private const float InsectSwarmBaseRange = 30f;
        private const float EntanglingRootsBaseRange = 30f;
>>>>>>> cpp_physics_system

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
                    StartKite(1500);

                ObjectManager.SetTarget(ObjectManager.GetTarget(ObjectManager.Player).Guid);
                castingEntanglingRoots = false;
            }

            if (IsKiting)
                return;

            // heal self if we're injured
            if (ObjectManager.Player.HealthPercent < 30 && (ObjectManager.Player.Mana >= ObjectManager.GetManaCost(HealingTouch) || ObjectManager.Player.Mana >= ObjectManager.GetManaCost(Rejuvenation)))
            {
                Wait.RemoveAll();
                BotTasks.Push(new HealTask(BotContext));
                return;
            }

            if (!EnsureTarget())
                return;

<<<<<<< HEAD
            if (Update(30))
=======
            if (Update(GetSpellRange(WrathBaseRange)))
>>>>>>> cpp_physics_system
                return;

            // if we get an add, root it with Entangling Roots
            if (ObjectManager.Aggressors.Count() == 2 && secondaryTarget == null)
                secondaryTarget = ObjectManager.Aggressors.Single(u => u.Guid != ObjectManager.GetTarget(ObjectManager.Player).Guid);

            if (secondaryTarget != null && !secondaryTarget.HasDebuff(EntanglingRoots))
            {
                ObjectManager.SetTarget(secondaryTarget.Guid);
<<<<<<< HEAD
                TryCastSpell(EntanglingRoots, 0, 30, !secondaryTarget.HasDebuff(EntanglingRoots), callback: EntanglingRootsCallback);
=======
                TryCastSpell(EntanglingRoots, 0f, GetSpellRange(EntanglingRootsBaseRange), !secondaryTarget.HasDebuff(EntanglingRoots), callback: EntanglingRootsCallback);
>>>>>>> cpp_physics_system
            }

            TryCastSpell(MoonkinForm, !ObjectManager.Player.HasBuff(MoonkinForm));

            TryCastSpell(Innervate, ObjectManager.Player.ManaPercent < 10, castOnSelf: true);

            TryCastSpell(RemoveCurse, condition: ObjectManager.Player.IsCursed && !ObjectManager.Player.HasBuff(MoonkinForm), castOnSelf: true);

            TryCastSpell(AbolishPoison, condition: ObjectManager.Player.IsPoisoned && !ObjectManager.Player.HasBuff(MoonkinForm), castOnSelf: true);

            var target = ObjectManager.GetTarget(ObjectManager.Player);

            TryCastSpell(InsectSwarm, 0f, GetSpellRange(InsectSwarmBaseRange),
                target != null &&
                !target.HasDebuff(InsectSwarm) &&
                target.HealthPercent > 20 &&
                !ImmuneToNatureDamage.Any(s => target.Name.Contains(s)));

            TryCastSpell(Moonfire, 0f, GetSpellRange(MoonfireBaseRange), target != null && !target.HasDebuff(Moonfire));

            bool lunarEclipse = ObjectManager.Player.HasBuff(EclipseLunar);
            bool solarEclipse = ObjectManager.Player.HasBuff(EclipseSolar);
            bool hasClearcasting = ObjectManager.Player.HasBuff(Clearcasting);

            TryCastSpell(Starfire, 0f, GetSpellRange(StarfireBaseRange),
                target != null &&
                (lunarEclipse || hasClearcasting) &&
                !ImmuneToNatureDamage.Any(s => target.Name.Contains(s)));

            TryCastSpell(Wrath, 0f, GetSpellRange(WrathBaseRange),
                target != null &&
                (solarEclipse || (!lunarEclipse && !hasClearcasting)) &&
                !ImmuneToNatureDamage.Any(s => target.Name.Contains(s)));
        }

        public override void PerformCombatRotation()
        {

        }
    }
}
