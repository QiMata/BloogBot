using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using static BotRunner.Constants.Spellbook;

namespace MageFrost.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
        // Vanilla 1.12.1 mage base spell ranges
        private const float FrostboltBaseRange = 30f;   // +3 per Arctic Reach talent rank
        private const float FireballBaseRange = 35f;
        private const float FireBlastBaseRange = 20f;
        private const float CounterspellBaseRange = 30f;
        private const float FlamestrikeBaseRange = 30f;
        private const float IceLanceBaseRange = 30f;
        private const float DeepFreezeBaseRange = 30f;

        private readonly string nuke;
        private readonly float nukeBaseRange;

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

            // Arctic Reach talent (Frost tree, row 11) adds 3 yd per rank to Frostbolt base range
            nukeBaseRange = FrostboltBaseRange + (ObjectManager.GetTalentRank(3, 11) * 3);
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

            var nukeRange = GetSpellRange(nukeBaseRange);
            if (Update(nukeRange))
                return;

            TryCastSpell(Evocation, 0f, float.MaxValue, (ObjectManager.Player.HealthPercent > 50 || ObjectManager.Player.HasBuff(IceBarrier)) && ObjectManager.Player.ManaPercent < 8 && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 15);

            IWoWItem wand = ObjectManager.GetEquippedItem(EquipSlot.Ranged);
            if (wand != null && ObjectManager.Player.ManaPercent <= 10 && !ObjectManager.Player.IsCasting && ObjectManager.Player.ChannelingId == 0)
                ObjectManager.CastSpell("Shoot");
            else
            {
                TryCastSpell(SummonWaterElemental, !ObjectManager.Units.Any(u => u.Name == "Water Elemental" && u.SummonedByGuid == ObjectManager.Player.Guid));

                TryCastSpell(ColdSnap, !ObjectManager.IsSpellReady(SummonWaterElemental));

                TryCastSpell(IcyVeins, ObjectManager.Aggressors.Count() > 1);

                TryCastSpell(FireWard, 0f, float.MaxValue, FireWardTargets.Any(c => ObjectManager.GetTarget(ObjectManager.Player).Name.Contains(c)) && (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 || ObjectManager.Player.HealthPercent < 10));

                TryCastSpell(FrostWard, 0f, float.MaxValue, FrostWardTargets.Any(c => ObjectManager.GetTarget(ObjectManager.Player).Name.Contains(c)) && (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 || ObjectManager.Player.HealthPercent < 10));

                TryCastSpell(Counterspell, 0f, GetSpellRange(CounterspellBaseRange), ObjectManager.GetTarget(ObjectManager.Player).Mana > 0 && ObjectManager.GetTarget(ObjectManager.Player).IsCasting);

                TryCastSpell(IceBarrier, condition: !ObjectManager.Player.HasBuff(IceBarrier) && (ObjectManager.Aggressors.Count() >= 2 || (!ObjectManager.IsSpellReady(FrostNova) && ObjectManager.Player.HealthPercent < 95 && ObjectManager.Player.ManaPercent > 40 && (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 || ObjectManager.Player.HealthPercent < 10))), castOnSelf: true);

                TryCastSpell(FrostNova, 0f, 9f, ObjectManager.GetTarget(ObjectManager.Player).TargetGuid == ObjectManager.Player.Guid && (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 || ObjectManager.Player.HealthPercent < 30) && !IsTargetFrozen && !ObjectManager.Units.Any(u => u.Guid != ObjectManager.GetTarget(ObjectManager.Player).Guid && u.HealthPercent > 0 && u.Guid != ObjectManager.Player.Guid && u.Position.DistanceTo(ObjectManager.Player.Position) <= 12), callback: FrostNovaCallback);

                TryCastSpell(ArcaneExplosion, 0f, 10f, ObjectManager.Aggressors.Count() > 2);

                TryCastSpell(Flamestrike, 0f, GetSpellRange(FlamestrikeBaseRange), ObjectManager.Aggressors.Count() > 2);

                TryCastSpell(ConeOfCold, 0f, 8f, ObjectManager.Player.Level >= 30 && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 && IsTargetFrozen);


                TryCastSpell(IceLance, 0f, GetSpellRange(IceLanceBaseRange), ObjectManager.Player.HasBuff(FingersOfFrost));

                TryCastSpell(DeepFreeze, 0f, GetSpellRange(DeepFreezeBaseRange), ObjectManager.Player.HasBuff(FingersOfFrost));

                TryCastSpell(FireBlast, 0f, GetSpellRange(FireBlastBaseRange), !IsTargetFrozen);

                TryCastSpell(Fireball, 0f, nukeRange, ObjectManager.Player.HasBuff(BrainFreeze));

                // Either Frostbolt or Fireball depending on what is stronger. Will always use Frostbolt at level 8+.
                TryCastSpell(nuke, 0f, nukeRange);
            }
        }

        public override void PerformCombatRotation() { }

        private Action FrostNovaCallback => () => StartKite(1500);

        private bool IsTargetFrozen => ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Frostbite) || ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(FrostNova);
    }
}
