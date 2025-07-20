using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace DruidFeral.Tasks;

internal class PvPRotationTask(IBotContext botContext) : CombatRotationTask(botContext), IBotTask
{
    public void Update()
    {
        if (!ObjectManager.Aggressors.Any())
        {
            BotTasks.Pop();
            return;
        }

        AssignDPSTarget();

        if (ObjectManager.GetTarget(ObjectManager.Player) == null)
            return;

        if (MoveBehindTankSpot(15))
            return;

        ObjectManager.Player.StopAllMovement();
        PerformCombatRotation();
    }

    public override void PerformCombatRotation()
    {
        var target = ObjectManager.GetTarget(ObjectManager.Player);
        if (target == null)
            return;

        ObjectManager.Player.StopAllMovement();
        ObjectManager.Player.Face(target.Position);
        ObjectManager.Player.StartMeleeAttack();

        if (ObjectManager.Player.HealthPercent < 40)
        {
            TryCastSpell(BearForm, 0, 50, ObjectManager.Player.CurrentShapeshiftForm != BearForm);
            TryUseBearAbility(DemoralizingRoar, 10, !target.HasDebuff(DemoralizingRoar));
            TryUseBearAbility(Maul, 10);
            return;
        }

        TryCastSpell(CatForm, 0, 50, ObjectManager.Player.CurrentShapeshiftForm != CatForm);

        TryUseCatAbility(TigersFury, 30, condition: !ObjectManager.Player.HasBuff(TigersFury));
        TryUseCatAbility(Rake, 35, condition: !target.HasDebuff(Rake));
        TryUseCatAbility(Claw, 40);
        TryUseCatAbility(FerociousBite, 35, true, ObjectManager.Player.ComboPoints >= 4 || target.HealthPercent < 30);
    }

    private void TryUseBearAbility(string name, int requiredRage = 0, bool condition = true)
    {
        if (ObjectManager.Player.IsSpellReady(name) &&
            ObjectManager.Player.Rage >= requiredRage &&
            !ObjectManager.Player.IsStunned &&
            ObjectManager.Player.CurrentShapeshiftForm == BearForm &&
            condition)
        {
            ObjectManager.Player.CastSpell(name);
        }
    }

    private void TryUseCatAbility(string name, int requiredEnergy = 0, bool requiresComboPoints = false, bool condition = true)
    {
        if (ObjectManager.Player.IsSpellReady(name) &&
            ObjectManager.Player.Energy >= requiredEnergy &&
            (!requiresComboPoints || ObjectManager.Player.ComboPoints > 0) &&
            !ObjectManager.Player.IsStunned &&
            ObjectManager.Player.CurrentShapeshiftForm == CatForm &&
            condition)
        {
            ObjectManager.Player.CastSpell(name);
        }
    }

    private void TryCastSpell(string name, int minRange, int maxRange, bool condition = true)
    {
        var target = ObjectManager.GetTarget(ObjectManager.Player);
        if (target == null)
            return;

        float distanceToTarget = ObjectManager.Player.Position.DistanceTo(target.Position);

        if (ObjectManager.Player.IsSpellReady(name) &&
            ObjectManager.Player.Mana >= ObjectManager.Player.GetManaCost(name) &&
            distanceToTarget >= minRange &&
            distanceToTarget <= maxRange &&
            condition &&
            !ObjectManager.Player.IsStunned &&
            !ObjectManager.Player.IsCasting &&
            ObjectManager.Player.ChannelingId == 0)
        {
            ObjectManager.Player.CastSpell(name);
        }
    }
}
