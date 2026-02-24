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

                if (ObjectManager.Player.Position.DistanceTo(ObjectManager.GetTarget(ObjectManager.Player).Position) < 40 && ObjectManager.Player.InLosWith(ObjectManager.GetTarget(ObjectManager.Player)))
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
                TryCastSpell(Fade, 0, int.MaxValue);

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

                //if (Container.State.TankInPosition)
                //{
                //    if (MoveTowardsTarget())
                //        return;

                //    PerformCombatRotation();
                //}
                //else if (MoveBehindTankSpot(15))
                //    return;
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
                TryCastSpell(ShadowForm, 0, int.MaxValue, !ObjectManager.Player.HasBuff(ShadowForm));

                TryCastSpell(VampiricEmbrace, 0, 29,
                             ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 50 &&
                             !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(VampiricEmbrace));

                //bool noNeutralsNearby = !ObjectManager.Units.Any(u => u.Guid != ObjectManager.GetTarget(ObjectManager.Player).Guid && u.UnitReaction == UnitReaction.Neutral && u.Position.DistanceTo(ObjectManager.Player.Position) <= 10);
                //TryCastSpell(PsychicScream, 0, 7, (ObjectManager.GetTarget(ObjectManager.Player).Position.DistanceTo(ObjectManager.Player.Position) < 8 && !ObjectManager.Player.HasBuff(PowerWordShield)) || ObjectManager.Aggressors.Count() > 1 && ObjectManager.GetTarget(ObjectManager.Player).CreatureType != CreatureType.Elemental);

                TryCastSpell(DispelMagic, 0, int.MaxValue, ObjectManager.Player.HasMagicDebuff, castOnSelf: true);

                if (ObjectManager.IsSpellReady(AbolishDisease))
                    TryCastSpell(AbolishDisease, 0, int.MaxValue, ObjectManager.Player.IsDiseased && !ObjectManager.Player.HasBuff(ShadowForm), castOnSelf: true);
                else if (ObjectManager.IsSpellReady(CureDisease))
                    TryCastSpell(CureDisease, 0, int.MaxValue, ObjectManager.Player.IsDiseased && !ObjectManager.Player.HasBuff(ShadowForm), castOnSelf: true);

                TryCastSpell(InnerFire, 0, int.MaxValue, !ObjectManager.Player.HasBuff(InnerFire));

                TryCastSpell(ShadowWordPain, 0, 29, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 10 && !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(ShadowWordPain) && ObjectManager.Player.ManaPercent > 50);

                //TryCastSpell(PowerWordShield, 0, int.MaxValue, !ObjectManager.Player.HasDebuff(WeakenedSoul) && !ObjectManager.Player.HasBuff(PowerWordShield) && (ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 || ObjectManager.Player.HealthPercent < 10), castOnSelf: true);

                TryCastSpell(MindBlast, 0, 29);

                if (ObjectManager.IsSpellReady(MindFlay) &&
                    ObjectManager.GetTarget(ObjectManager.Player).Position.DistanceTo(ObjectManager.Player.Position) <= 19 &&
                    (!ObjectManager.IsSpellReady(PowerWordShield) || ObjectManager.Player.HasBuff(PowerWordShield)))
                    TryCastSpell(MindFlay, 0, 19);
                else
                    TryCastSpell(Smite, 0, 29, !ObjectManager.Player.HasBuff(ShadowForm));
            }
        }
    }
}
