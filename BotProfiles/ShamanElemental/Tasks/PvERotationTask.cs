using BotRunner.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace ShamanElemental.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        // Vanilla 1.12.1 shaman base spell ranges
        private const float LightningBoltBaseRange = 30f;
        private const float EarthShockBaseRange = 20f;
        private const float FlameShockBaseRange = 20f;

        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        ~PvERotationTask()
        {

        }

        public override void PerformCombatRotation()
        {
            if (!EnsureTarget()) return;

            ExecuteRotation();
        }

        public void Update()
        {
            if (!EnsureTarget())
                return;

            if (ObjectManager.Player.HealthPercent < 30 && ObjectManager.Player.Mana >= ObjectManager.GetManaCost(HealingWave))
            {
                BotTasks.Push(new HealTask(BotContext));
                return;
            }

            // Stay close for totem effectiveness and melee fallback
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (Update(target != null ? GetMeleeRange(target) * 2f : 12f))
            {
                return;
            }

            ExecuteRotation();
        }

        private void ExecuteRotation()
        {
            TryCastSpell(GroundingTotem, condition: ObjectManager.Aggressors.Any(a => a.IsCasting && ObjectManager.GetTarget(ObjectManager.Player).Mana > 0), castOnSelf: true);

            TryCastSpell(EarthShock, 0f, GetSpellRange(EarthShockBaseRange),
                !NatureImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) &&
                (ObjectManager.GetTarget(ObjectManager.Player).IsCasting || ObjectManager.GetTarget(ObjectManager.Player).IsChanneling));

            TryCastSpell(FlameShock, 0f, GetSpellRange(FlameShockBaseRange),
                !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(FlameShock) &&
                !FireImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) &&
                ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 50);

            TryCastSpell(ElementalMastery, condition: true, castOnSelf: true);

            TryCastSpell(LightningBolt, 0f, GetSpellRange(LightningBoltBaseRange),
                !NatureImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) &&
                (ObjectManager.Player.ManaPercent > 20 || ObjectManager.Player.HasBuff(ElementalMastery)));

            TryCastSpell(TremorTotem, condition: FearingCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) && !ObjectManager.Units.Any(u => u.Position.DistanceTo(ObjectManager.Player.Position) < 29 && u.HealthPercent > 0 && u.Name.Contains(TremorTotem)), castOnSelf: true);

            TryCastSpell(SearingTotem, condition: ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 70 && !FireImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) && ObjectManager.GetTarget(ObjectManager.Player).Position.DistanceTo(ObjectManager.Player.Position) < 20 && !ObjectManager.Units.Any(u => u.Position.DistanceTo(ObjectManager.Player.Position) < 19 && u.HealthPercent > 0 && u.Name.Contains(SearingTotem)), castOnSelf: true);

            TryCastSpell(ManaSpringTotem, condition: !ObjectManager.Units.Any(u => u.Position.DistanceTo(ObjectManager.Player.Position) < 19 && u.HealthPercent > 0 && u.Name.Contains(ManaSpringTotem)), castOnSelf: true);


            TryCastSpell(LightningShield, condition: !NatureImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) && !ObjectManager.Player.HasBuff(LightningShield), castOnSelf: true);

            TryCastSpell(RockbiterWeapon, condition: ObjectManager.IsSpellReady(RockbiterWeapon) && (FireImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name) || !ObjectManager.Player.MainhandIsEnchanted && !ObjectManager.IsSpellReady(FlametongueWeapon)), castOnSelf: true);

            TryCastSpell(FlametongueWeapon, condition: ObjectManager.IsSpellReady(FlametongueWeapon) && !ObjectManager.Player.MainhandIsEnchanted && !FireImmuneCreatures.Contains(ObjectManager.GetTarget(ObjectManager.Player).Name), castOnSelf: true);

            if (ObjectManager.Player.ManaPercent < 5)
                ObjectManager.StartMeleeAttack();
        }
    }
}
