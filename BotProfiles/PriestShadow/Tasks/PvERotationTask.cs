using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace PriestShadow.Tasks
{
    public class PvERotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
    {
        // Vanilla 1.12.1 priest base spell ranges
        private const float ShadowWordPainBaseRange = 30f;
        private const float MindBlastBaseRange = 30f;
        private const float MindFlayBaseRange = 20f;
        private const float SmiteBaseRange = 30f;
        private const float VampiricEmbraceBaseRange = 30f;
        private const float HealBaseRange = 40f;

        public void Update()
        {
            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            IWoWUnit woWUnit = ObjectManager.Aggressors.FirstOrDefault(x => x.TargetGuid == ObjectManager.Player.Guid);
            if (ObjectManager.PartyMembers.Any(x => x.HealthPercent < 70) && ObjectManager.Player.Mana >= ObjectManager.GetManaCost(LesserHeal))
            {
                List<IWoWPlayer> unhealthyMembers = [.. ObjectManager.PartyMembers.Where(x => x.HealthPercent < 70).OrderBy(x => x.Health)];

                if (unhealthyMembers.Count > 0 && ObjectManager.Player.Mana >= ObjectManager.GetManaCost(LesserHeal))
                {
                    if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).Guid != unhealthyMembers[0].Guid)
                    {
                        ObjectManager.SetTarget(unhealthyMembers[0].Guid);
                        return;
                    }
                }

                if (ObjectManager.Player.IsCasting || ObjectManager.GetTarget(ObjectManager.Player) == null)
                    return;

                if (ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) < GetSpellRange(HealBaseRange) && ObjectManager.Player.InLosWith(ObjectManager.GetTarget(ObjectManager.Player)))
                {
                    ObjectManager.StopAllMovement();
                    ObjectManager.StopWandAttack();

                    ObjectManager.StopAllMovement();

                    if (!ObjectManager.GetTarget(ObjectManager.Player).HasBuff(Renew))
                        ObjectManager.CastSpell(Renew);
                    if (ObjectManager.IsSpellReady(LesserHeal))
                        ObjectManager.CastSpell(LesserHeal);

                    return;
                }
                else
                {
                    NavigateToward(ObjectManager.GetTarget(ObjectManager.Player).Position);
                    return;
                }
            }
            else if (woWUnit != null)
            {
                TryCastSpell(Fade, condition: true, castOnSelf: true);

                if (woWUnit.ManaPercent > 5)
                {
                    if (MoveBehindTankSpot(45))
                        return;
                    else
                        ObjectManager.StopAllMovement();
                }
                else if (MoveBehindTankSpot(3))
                    return;
                else
                    ObjectManager.StopAllMovement();
            }
            else
            {
                AssignDPSTarget();

                if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).UnitReaction == UnitReaction.Friendly) return;
            }
        }

        public override void PerformCombatRotation()
        {
            ObjectManager.StopAllMovement();
            ObjectManager.Face(ObjectManager.GetTarget(ObjectManager.Player).Position);

            if (ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(ShadowWordPain) || ObjectManager.Player.ManaPercent < 50)
                ObjectManager.StartWandAttack();
            else
            {
                TryCastSpell(ShadowForm, condition: !ObjectManager.Player.HasBuff(ShadowForm), castOnSelf: true);

                TryCastSpell(VampiricEmbrace, 0f, GetSpellRange(VampiricEmbraceBaseRange),
                             ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 50 &&
                             !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(VampiricEmbrace));

                TryCastSpell(DispelMagic, condition: ObjectManager.Player.HasMagicDebuff, castOnSelf: true);

                if (ObjectManager.IsSpellReady(AbolishDisease))
                    TryCastSpell(AbolishDisease, condition: ObjectManager.Player.IsDiseased && !ObjectManager.Player.HasBuff(ShadowForm), castOnSelf: true);
                else if (ObjectManager.IsSpellReady(CureDisease))
                    TryCastSpell(CureDisease, condition: ObjectManager.Player.IsDiseased && !ObjectManager.Player.HasBuff(ShadowForm), castOnSelf: true);

                TryCastSpell(InnerFire, condition: !ObjectManager.Player.HasBuff(InnerFire), castOnSelf: true);

                TryCastSpell(ShadowWordPain, 0f, GetSpellRange(ShadowWordPainBaseRange), ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 10 && !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(ShadowWordPain) && ObjectManager.Player.ManaPercent > 50);

                TryCastSpell(MindBlast, 0f, GetSpellRange(MindBlastBaseRange));

                var mindFlayRange = GetSpellRange(MindFlayBaseRange);
                if (ObjectManager.IsSpellReady(MindFlay) &&
                    ObjectManager.GetTarget(ObjectManager.Player).Position.DistanceTo(ObjectManager.Player.Position) <= mindFlayRange &&
                    (!ObjectManager.IsSpellReady(PowerWordShield) || ObjectManager.Player.HasBuff(PowerWordShield)))
                    TryCastSpell(MindFlay, 0f, mindFlayRange);
                else
                    TryCastSpell(Smite, 0f, GetSpellRange(SmiteBaseRange), !ObjectManager.Player.HasBuff(ShadowForm));
            }
        }
    }
}
