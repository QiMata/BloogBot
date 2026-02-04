using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidBalance.Tasks
{
    /// <summary>
    /// PvP combat rotation for a balance druid. Maintains Moonkin Form and
    /// uses typical balance damage spells.
    /// </summary>
    internal class PvPRotationTask : CombatRotationTask, IBotTask
    {
        private const string Starfire = "Starfire";
        private const string EclipseSolar = "Eclipse (Solar)";
        private const string EclipseLunar = "Eclipse (Lunar)";
        private static readonly string[] ImmuneToNatureDamage = ["Vortex", "Whirlwind", "Whirling", "Dust", "Cyclone"];

        internal PvPRotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            AssignDPSTarget();

            if (ObjectManager.GetTarget(ObjectManager.Player) == null)
                return;

            if (Update(30))
                return;

            PerformCombatRotation();
        }

        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            TryCastSpell(MoonkinForm, !ObjectManager.Player.HasBuff(MoonkinForm));
            TryCastSpell(Innervate, ObjectManager.Player.ManaPercent < 15, castOnSelf: true);
            TryCastSpell(RemoveCurse, 0, int.MaxValue, ObjectManager.Player.IsCursed && !ObjectManager.Player.HasBuff(MoonkinForm), castOnSelf: true);
            TryCastSpell(AbolishPoison, 0, int.MaxValue, ObjectManager.Player.IsPoisoned && !ObjectManager.Player.HasBuff(MoonkinForm), castOnSelf: true);

            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null) return;

            TryCastSpell(InsectSwarm, 0, 30,
                !target.HasDebuff(InsectSwarm) &&
                target.HealthPercent > 20 &&
                !ImmuneToNatureDamage.Any(s => target.Name.Contains(s)));

            TryCastSpell(Moonfire, 0, 30, !target.HasDebuff(Moonfire));

            bool lunarEclipse = ObjectManager.Player.HasBuff(EclipseLunar);
            bool solarEclipse = ObjectManager.Player.HasBuff(EclipseSolar);
            bool hasClearcasting = ObjectManager.Player.HasBuff(Clearcasting);

            TryCastSpell(Starfire, 0, 30,
                (lunarEclipse || hasClearcasting) &&
                !ImmuneToNatureDamage.Any(s => target.Name.Contains(s)));

            TryCastSpell(Wrath, 0, 30,
                (solarEclipse || (!lunarEclipse && !hasClearcasting)) &&
                !ImmuneToNatureDamage.Any(s => target.Name.Contains(s)));
        }
    }
}
