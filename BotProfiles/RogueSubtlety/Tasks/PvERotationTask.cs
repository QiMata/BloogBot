using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;
using static BotRunner.Constants.Spellbook;

namespace RogueSubtlety.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {
        internal PvERotationTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.Aggressors.Any(x => x.TargetGuid == ObjectManager.Player.Guid))
            {
                Update(0);
                return;
            }

            if (ObjectManager.CasterAggressors.Any(x => x.TargetGuid == ObjectManager.Player.Guid))
            {
                if (MoveBehindTankSpot(15))
                    return;
            }

            AssignDPSTarget();

            if (!ObjectManager.PartyLeader.IsMoving && ObjectManager.GetTarget(ObjectManager.Player) != null
                && ObjectManager.GetTarget(ObjectManager.Player).Position.DistanceTo(ObjectManager.PartyLeader.Position) <= 5)
            {
                if (MoveBehindTarget(3))
                    return;
                else
                {
                    ObjectManager.StopAllMovement();
                    ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);
                    ObjectManager.StartMeleeAttack();

                    TryUseAbility(Evasion, 0, ObjectManager.Aggressors.Count() > 1);
                    TryUseAbility(SliceAndDice, 25, !ObjectManager.Player.HasBuff(SliceAndDice)
                        && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 70
                        && ObjectManager.Player.ComboPoints >= 2);

                    TryUseAbility(Kick, 25, ReadyToInterrupt(ObjectManager.GetTarget(ObjectManager.Player)));
                    TryUseAbility(Gouge, 45, ReadyToInterrupt(ObjectManager.GetTarget(ObjectManager.Player)) && !ObjectManager.IsSpellReady(Kick));
                    TryUseAbility(KidneyShot, 25, ReadyToInterrupt(ObjectManager.GetTarget(ObjectManager.Player)) && !ObjectManager.IsSpellReady(Kick)
                        && ObjectManager.Player.ComboPoints >= 1 && ObjectManager.Player.ComboPoints <= 2);

                    bool readyToEviscerate =
                        ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 20 && ObjectManager.Player.ComboPoints >= 2
                        || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 30 && ObjectManager.Player.ComboPoints >= 3
                        || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 40 && ObjectManager.Player.ComboPoints >= 4
                        || ObjectManager.Player.ComboPoints == 5;
                    TryUseAbility(Eviscerate, 35, readyToEviscerate);

                    TryUseAbility(SinisterStrike, 45, ObjectManager.Player.ComboPoints < 5);
                }
            }
            else
                ObjectManager.StopAllMovement();
        }

        public override void PerformCombatRotation()
        {
            // not used
        }

        private bool ReadyToInterrupt(IWoWUnit target) =>
            ObjectManager.GetTarget(ObjectManager.Player).Mana > 0 && (target.IsCasting || target.IsChanneling);
    }
}
