using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using static BotRunner.Constants.Spellbook;

namespace MageArcane.Tasks
{
    public class PvERotationTask : CombatRotationTask, IBotTask
    {
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
            if (Update(30))
                return;

            bool hasWand = ObjectManager.GetEquippedItem(EquipSlot.Ranged) != null;
            bool useWand = hasWand && ObjectManager.Player.ManaPercent <= 10 && !ObjectManager.Player.IsCasting && ObjectManager.Player.ChannelingId == 0;
            if (useWand)
                ObjectManager.CastSpell("Shoot");

            TryCastSpell(PresenceOfMind, 0, 50, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 80);

            TryCastSpell(ArcanePower, 0, 50, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 80);

            TryCastSpell(Counterspell, 0, 29, ObjectManager.GetTarget(ObjectManager.Player).Mana > 0 && ObjectManager.GetTarget(ObjectManager.Player).IsCasting);

            TryCastSpell(ManaShield, 0, 50, !ObjectManager.Player.HasBuff(ManaShield) && ObjectManager.Player.HealthPercent < 20);

            TryCastSpell(FireBlast, 0, 19, !ObjectManager.Player.HasBuff(Clearcasting));

            TryCastSpell(FrostNova, 0, 10, !ObjectManager.Units.Any(u => u.Guid != ObjectManager.GetTarget(ObjectManager.Player).Guid && u.Health > 0 && u.Position.DistanceTo(ObjectManager.Player.Position) < 15), callback: FrostNovaCallback);

            TryCastSpell(ArcaneExplosion, 0, 10, ObjectManager.Aggressors.Count() > 2);

            TryCastSpell(Flamestrike, 0, 30, ObjectManager.Aggressors.Count() > 2);

            TryCastSpell(ArcaneBarrage, 0, 30, ObjectManager.Player.Level >= 40);

            TryCastSpell(Fireball, 0, 34, ObjectManager.Player.Level < 15 || ObjectManager.Player.HasBuff(PresenceOfMind));

            TryCastSpell(ArcaneMissiles, 0, 29, ObjectManager.Player.Level >= 15);
        }

        private Action FrostNovaCallback => () => StartKite(1500);
    }
}
