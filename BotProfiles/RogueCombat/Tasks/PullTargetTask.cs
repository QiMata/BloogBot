using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Enums;
using GameData.Core.Models;
using static BotRunner.Constants.Spellbook;

namespace RogueCombat.Tasks
{
    public class PullTargetTask : BotTask, IBotTask
    {

        internal PullTargetTask(IBotContext botContext) : base(botContext) { }

        public void Update()
        {
            if (ObjectManager.GetTarget(ObjectManager.Player).TappedByOther)
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                return;
            }

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position);
            if (distanceToTarget < 25 && !ObjectManager.Player.HasBuff(Stealth) && ObjectManager.IsSpellReady(Garrote) && !ObjectManager.Player.IsInCombat)
            {
                ObjectManager.CastSpell(Stealth);
            }

            if (distanceToTarget < 15 && ObjectManager.IsSpellReady(Distract) && ObjectManager.IsSpellReady(Distract) && ObjectManager.GetTarget(ObjectManager.Player).CreatureType != CreatureType.Totem)
            {
                //var delta = ObjectManager.GetTarget(ObjectManager.Player).Position - ObjectManager.Player.Position;
                //var normalizedVector = delta.GetNormalizedVector();
                //var scaledVector = normalizedVector * 5;
                //var targetPosition = ObjectManager.GetTarget(ObjectManager.Player).Position + scaledVector;

                //ObjectManager.Player.CastSpellAtPosition(Distract, targetPosition);
            }

            if (distanceToTarget < 3.5 && ObjectManager.Player.HasBuff(Stealth) && !ObjectManager.Player.IsInCombat && ObjectManager.GetTarget(ObjectManager.Player).CreatureType != CreatureType.Totem)
            {
                if (ObjectManager.IsSpellReady(Garrote) && ObjectManager.GetTarget(ObjectManager.Player).CreatureType != CreatureType.Elemental && ObjectManager.Player.IsBehind(ObjectManager.GetTarget(ObjectManager.Player)))
                {
                    ObjectManager.CastSpell(Garrote);
                    return;
                }
                else if (ObjectManager.IsSpellReady(CheapShot) && ObjectManager.Player.IsBehind(ObjectManager.GetTarget(ObjectManager.Player)))
                {
                    ObjectManager.CastSpell(CheapShot);
                    return;
                }
            } 

            if (distanceToTarget < 3)
            {
                ObjectManager.StopAllMovement();
                BotTasks.Pop();
                BotTasks.Push(new PvERotationTask(BotContext));
                return;
            }

            NavigateToward(ObjectManager.GetTarget(ObjectManager.Player).Position);
        }
    }
}
