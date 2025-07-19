using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace ShamanElemental.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {

        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        ~PvERotationTask()
        {

        }

        public override void PerformCombatRotation()
        {
            if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                if (ObjectManager.Aggressors.Any())
                    ObjectManager.Player.SetTarget(ObjectManager.Aggressors.First().Guid);
                else
                    return;
            }

            ExecuteRotation();
        }

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                ObjectManager.Player.SetTarget(ObjectManager.Aggressors.First().Guid);
            }

            if (ObjectManager.Player.HealthPercent < 30 && ObjectManager.Player.Mana >= ObjectManager.Player.GetManaCost(HealingWave))
            {
                BotTasks.Push(new HealTask(BotContext));
                return;
            }

            if (Update(12))
            {
                return;
            }

            ExecuteRotation();
        }

        private void ExecuteRotation()
        {
            TryCastSpell(GroundingTotem, 0, int.MaxValue, ObjectManager.Aggressors.Any(a => a.IsCasting && ObjectManager.GetTarget(ObjectManager.Player).Mana > 0));

            TryCastSpell(EarthShock, 0, 20,
                !NatureImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) &&
                (ObjectManager.GetTarget(ObjectManager.Player).IsCasting || ObjectManager.GetTarget(ObjectManager.Player).IsChanneling));

            TryCastSpell(FlameShock, 0, 20,
                !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(FlameShock) &&
                !FireImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) &&
                ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 50);

            TryCastSpell(ElementalMastery, 0, int.MaxValue);

            TryCastSpell(LightningBolt, 0, 30,
                !NatureImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) &&
                (ObjectManager.Player.ManaPercent > 20 || ObjectManager.Player.HasBuff(ElementalMastery)));

            TryCastSpell(TremorTotem, 0, int.MaxValue, FearingCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) && !ObjectManager.Units.Any(u => u.Position.DistanceTo(ObjectManager.Player.Position) < 29 && u.HealthPercent > 0 && u.Name.Contains(TremorTotem)));

            //TryCastSpell(StoneclawTotem, 0, int.MaxValue, ObjectManager.Aggressors.Count() > 1);

            //TryCastSpell(StoneskinTotem, 0, int.MaxValue, ObjectManager.GetTarget(ObjectManager.Player).Mana == 0 && !ObjectManager.Units.Any(u => u.Position.GetDistanceTo(ObjectManager.Player.Position) < 19 && u.HealthPercent > 0 && (u.Name.Contains(StoneskinTotem) || u.Name.Contains(TremorTotem))));

            TryCastSpell(SearingTotem, 0, int.MaxValue, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 70 && !FireImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) && ObjectManager.GetTarget(ObjectManager.Player).Position.DistanceTo(ObjectManager.Player.Position) < 20 && !ObjectManager.Units.Any(u => u.Position.DistanceTo(ObjectManager.Player.Position) < 19 && u.HealthPercent > 0 && u.Name.Contains(SearingTotem)));

            TryCastSpell(ManaSpringTotem, 0, int.MaxValue, !ObjectManager.Units.Any(u => u.Position.DistanceTo(ObjectManager.Player.Position) < 19 && u.HealthPercent > 0 && u.Name.Contains(ManaSpringTotem)));


            TryCastSpell(LightningShield, 0, int.MaxValue, !NatureImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) && !ObjectManager.Player.HasBuff(LightningShield));

            TryCastSpell(RockbiterWeapon, 0, int.MaxValue, ObjectManager.Player.IsSpellReady(RockbiterWeapon) && (FireImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) || !ObjectManager.Player.MainhandIsEnchanted && !ObjectManager.Player.IsSpellReady(FlametongueWeapon)));

            TryCastSpell(FlametongueWeapon, 0, int.MaxValue, ObjectManager.Player.IsSpellReady(FlametongueWeapon) && !ObjectManager.Player.MainhandIsEnchanted && !FireImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name));

            if (ObjectManager.Player.ManaPercent < 5)
                ObjectManager.Player.StartMeleeAttack();
        }
    }
}
