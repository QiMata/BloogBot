using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
<<<<<<< HEAD
=======
using GameData.Core.Models;
>>>>>>> cpp_physics_system
using static BotRunner.Constants.Spellbook;

namespace MageArcane.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
<<<<<<< HEAD
=======
        // Vanilla 1.12.1 mage base spell ranges
        private const float ArcaneMissilesBaseRange = 30f;
        private const float ArcaneBarrageBaseRange = 30f;
        private const float FireballBaseRange = 35f;
        private const float FireBlastBaseRange = 20f;
        private const float CounterspellBaseRange = 30f;
        private const float FlamestrikeBaseRange = 30f;

>>>>>>> cpp_physics_system
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (IsKiting)
                return;

            if (!EnsureTarget())
                return;

            ExecuteRotation();
        }

        public override void PerformCombatRotation()
        {
            if (IsKiting)
                return;

            if (!EnsureTarget()) return;

            ExecuteRotation();
        }

        private void ExecuteRotation()
        {
            if (Update(GetSpellRange(ArcaneMissilesBaseRange)))
                return;

            bool hasWand = ObjectManager.GetEquippedItem(EquipSlot.Ranged) != null;
            bool useWand = hasWand && ObjectManager.Player.ManaPercent <= 10 && !ObjectManager.Player.IsCasting && ObjectManager.Player.ChannelingId == 0;
            if (useWand)
                ObjectManager.CastSpell("Shoot");

            TryCastSpell(PresenceOfMind, condition: ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 80, castOnSelf: true);

            TryCastSpell(ArcanePower, condition: ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 80, castOnSelf: true);

            TryCastSpell(Counterspell, 0f, GetSpellRange(CounterspellBaseRange), ObjectManager.GetTarget(ObjectManager.Player).Mana > 0 && ObjectManager.GetTarget(ObjectManager.Player).IsCasting);

            TryCastSpell(ManaShield, condition: !ObjectManager.Player.HasBuff(ManaShield) && ObjectManager.Player.HealthPercent < 20, castOnSelf: true);

            TryCastSpell(FireBlast, 0f, GetSpellRange(FireBlastBaseRange), !ObjectManager.Player.HasBuff(Clearcasting));

            TryCastSpell(FrostNova, 0f, 10f, !ObjectManager.Units.Any(u => u.Guid != ObjectManager.GetTarget(ObjectManager.Player).Guid && u.Health > 0 && u.Position.DistanceTo(ObjectManager.Player.Position) < 15), callback: FrostNovaCallback);

            TryCastSpell(ArcaneExplosion, 0f, 10f, ObjectManager.Aggressors.Count() > 2);

            TryCastSpell(Flamestrike, 0f, GetSpellRange(FlamestrikeBaseRange), ObjectManager.Aggressors.Count() > 2);

            TryCastSpell(ArcaneBarrage, 0f, GetSpellRange(ArcaneBarrageBaseRange), ObjectManager.Player.Level >= 40);

            TryCastSpell(Fireball, 0f, GetSpellRange(FireballBaseRange), ObjectManager.Player.Level < 15 || ObjectManager.Player.HasBuff(PresenceOfMind));

            TryCastSpell(ArcaneMissiles, 0f, GetSpellRange(ArcaneMissilesBaseRange), ObjectManager.Player.Level >= 15);
        }

        private Action FrostNovaCallback => () => StartKite(1500);
    }
}
