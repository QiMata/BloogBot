using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace MageFrost.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        private readonly string nuke;
        private readonly int range;

        internal PvERotationTask(IBotContext botContext) : base(botContext)
        {
            if (!ObjectManager.IsSpellReady(Frostbolt))
                nuke = Fireball;
            else if (ObjectManager.Player.Level >= 8)
                nuke = Frostbolt;
            else if (ObjectManager.Player.Level >= 6)
                nuke = Fireball;
            else if (ObjectManager.Player.Level >= 4)
                nuke = Frostbolt;
            else
                nuke = Fireball;

            range = 29 + (ObjectManager.GetTalentRank(3, 11) * 3);
        }

        public void Update()
        {
            if (IsKiting)
            {
                TryCastSpell(FrostNova); // sometimes we try to cast too early and get into this state while FrostNova is still ready.
                return;
            }

            if (!EnsureTarget())
                return;

            if (Update(29 + (ObjectManager.GetTalentRank(3, 11) * 3)))
                return;

            TryCastSpell(Evocation, 0, int.MaxValue, (ObjectManager.Player.HealthPercent > 50 || ObjectManager.Player.HasBuff(IceBarrier)) && ObjectManager.Player.ManaPercent < 8 && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 15);

            IWoWItem wand = ObjectManager.GetEquippedItem(EquipSlot.Ranged);
            if (wand != null && ObjectManager.Player.ManaPercent <= 10 && !ObjectManager.Player.IsCasting && ObjectManager.Player.ChannelingId == 0)
                ObjectManager.CastSpell("Shoot");
            else
            {
                TryCastSpell(SummonWaterElemental, !ObjectManager.Units.Any(u => u.Name == "Water Elemental" && u.SummonedByGuid == ObjectManager.Player.Guid));

                TryCastSpell(ColdSnap, !ObjectManager.IsSpellReady(SummonWaterElemental));

                TryCastSpell(IcyVeins, ObjectManager.Aggressors.Count() > 1);

                TryCastSpell(FireWard, 0, int.MaxValue, FireWardTargets.Any(c => ObjectManager.GetTarget(ObjectManager.Player).Name.Contains(c)) && (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 || ObjectManager.Player.HealthPercent < 10));

                TryCastSpell(FrostWard, 0, int.MaxValue, FrostWardTargets.Any(c => ObjectManager.GetTarget(ObjectManager.Player).Name.Contains(c)) && (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 || ObjectManager.Player.HealthPercent < 10));

                TryCastSpell(Counterspell, 0, 30, ObjectManager.GetTarget(ObjectManager.Player).Mana > 0 && ObjectManager.GetTarget(ObjectManager.Player).IsCasting);

                TryCastSpell(IceBarrier, 0, 50, !ObjectManager.Player.HasBuff(IceBarrier) && (ObjectManager.Aggressors.Count() >= 2 || (!ObjectManager.IsSpellReady(FrostNova) && ObjectManager.Player.HealthPercent < 95 && ObjectManager.Player.ManaPercent > 40 && (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 || ObjectManager.Player.HealthPercent < 10))));

                TryCastSpell(FrostNova, 0, 9, ObjectManager.GetTarget(ObjectManager.Player).TargetGuid == ObjectManager.Player.Guid && (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 || ObjectManager.Player.HealthPercent < 30) && !IsTargetFrozen && !ObjectManager.Units.Any(u => u.Guid != ObjectManager.GetTarget(ObjectManager.Player).Guid && u.HealthPercent > 0 && u.Guid != ObjectManager.Player.Guid && u.Position.DistanceTo(ObjectManager.Player.Position) <= 12), callback: FrostNovaCallback);

                TryCastSpell(ArcaneExplosion, 0, 10, ObjectManager.Aggressors.Count() > 2);

                TryCastSpell(Flamestrike, 0, 30, ObjectManager.Aggressors.Count() > 2);

                TryCastSpell(ConeOfCold, 0, 8, ObjectManager.Player.Level >= 30 && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 && IsTargetFrozen);


                TryCastSpell(IceLance, 0, 30, ObjectManager.Player.HasBuff(FingersOfFrost));

                TryCastSpell(DeepFreeze, 0, 30, ObjectManager.Player.HasBuff(FingersOfFrost));

                TryCastSpell(FireBlast, 0, 20, !IsTargetFrozen);

                TryCastSpell(Fireball, 0, range, ObjectManager.Player.HasBuff(BrainFreeze));

                // Either Frostbolt or Fireball depending on what is stronger. Will always use Frostbolt at level 8+.
                TryCastSpell(nuke, 0, range);
            }
        }

        public override void PerformCombatRotation() { }

        private Action FrostNovaCallback => () => StartKite(1500);

        private bool IsTargetFrozen => ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Frostbite) || ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(FrostNova);
    }
}
