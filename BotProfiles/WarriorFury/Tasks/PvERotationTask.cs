﻿using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using static BotRunner.Constants.Spellbook;

namespace WarriorFury.Tasks
{
    internal class PvERotationTask : CombatRotationTask, IBotTask
    {
        private bool slamReady;
        private int slamReadyStartTime;
        private bool backpedaling;
        private int backpedalStartTime;
        private int backpedalDuration;

        internal PvERotationTask(IBotContext botContext) : base(botContext)
        {
            EventHandler.OnSlamReady += OnSlamReadyCallback;
        }

        ~PvERotationTask()
        {
            EventHandler.OnSlamReady -= OnSlamReadyCallback;
        }

        public void Update()
        {
            if (Environment.TickCount - backpedalStartTime > backpedalDuration)
            {
                ObjectManager.Player.StopMovement(ControlBits.Back);
                // ObjectManager.Player.StopMovement(ControlBits.StrafeLeft);
                // ObjectManager.Player.StopMovement(ControlBits.Right);
                backpedaling = false;
            }

            if (backpedaling)
                return;

            if (Environment.TickCount - slamReadyStartTime > 250)
            {
                slamReady = false;
            }

            //if (!FacingAllTargets && ObjectManager.Aggressors.Count() >= 2 && AggressorsInMelee)
            //{
            //    WalkBack(50);
            //    return;
            //}

            if (!ObjectManager.Aggressors.Any())
            {
                BotTasks.Pop();
                return;
            }

            if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                ObjectManager.Player.SetTarget(ObjectManager.Aggressors.First().Guid);
            }

            if (Update(5))
                return;

            ExecuteRotation();
        }

        private void OnSlamReadyCallback(object sender, EventArgs e)
        {
            OnSlamReady();
        }

        private void OnSlamReady()
        {
            slamReady = true;
            slamReadyStartTime = Environment.TickCount;
        }

        private void SlamCallback()
        {
            slamReady = false;
        }

        // Check to see if toon is facing all the ObjectManager.GetTarget(ObjectManager.Player)s and they are within melee, used to determine if player should walkbackwards to reposition ObjectManager.GetTarget(ObjectManager.Player)s in front of mob.
        private bool FacingAllTargets
        {
            get
            {
                return ObjectManager.Aggressors.All(a => a.Position.DistanceTo(ObjectManager.Player.Position) < 7);
            }
        }

        // Check to see if toon is with melee distance of mobs.  This is used to determine if player should use single mob rotation or multi-mob rotation.
        private bool AggressorsInMelee
        {
            get
            {
                return ObjectManager.Aggressors.All(a => a.Position.DistanceTo(ObjectManager.Player.Position) < 7);
            }
        }

        private void WalkBack(int milleseconds)
        {
            backpedaling = true;
            backpedalStartTime = Environment.TickCount;
            backpedalDuration = milleseconds;
            ObjectManager.Player.StartMovement(ControlBits.Back);
            // ObjectManager.Player.StartMovement(ControlBits.StrafeLeft);
            // ObjectManager.Player.StartMovement(ControlBits.Right);
        }

        public override void PerformCombatRotation()
        {
            if (ObjectManager.GetTarget(ObjectManager.Player) == null || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent <= 0)
            {
                if (ObjectManager.Aggressors.Any())
                    ObjectManager.Player.SetTarget(ObjectManager.Aggressors.First().Guid);
                else
                    return;
            }

            ExecuteRotation();
        }

        private void ExecuteRotation()
        {
            string currentStance = ObjectManager.Player.CurrentStance;
            IEnumerable<IWoWUnit> spellcastingAggressors = ObjectManager.Aggressors
                .Where(a => a.Mana > 0);
            // Use these abilities when fighting any number of mobs.
            TryUseAbility(BerserkerStance, condition: ObjectManager.Player.Level >= 30 && currentStance == BattleStance && (ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Rend) || ObjectManager.GetTarget(ObjectManager.Player).HealthPercent < 80 || ObjectManager.GetTarget(ObjectManager.Player).CreatureType == CreatureType.Elemental || ObjectManager.GetTarget(ObjectManager.Player).CreatureType == CreatureType.Undead));

            TryUseAbility(Pummel, 10, currentStance == BerserkerStance && ObjectManager.GetTarget(ObjectManager.Player).Mana > 0 && (ObjectManager.GetTarget(ObjectManager.Player).IsCasting || ObjectManager.GetTarget(ObjectManager.Player).IsChanneling));

            // TryUseAbility(Rend, 10, (currentStance == BattleStance && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 50 && !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Rend) && (ObjectManager.GetTarget(ObjectManager.Player).CreatureType != CreatureType.Elemental && ObjectManager.GetTarget(ObjectManager.Player).CreatureType != CreatureType.Undead)));

            TryUseAbility(DeathWish, 10, ObjectManager.Player.IsSpellReady(DeathWish) && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 80);

            TryUseAbility(BattleShout, 10, !ObjectManager.Player.HasBuff(BattleShout));

            TryUseAbilityById(BloodFury, 4, 0, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 80);

            TryUseAbility(Bloodrage, condition: ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 50);

            TryUseAbility(Execute, 15, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent < 20);

            TryUseAbility(BerserkerRage, condition: ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 70 && currentStance == BerserkerStance);

            TryUseAbility(Overpower, 5, currentStance == BattleStance);

            // Use these abilities if you are fighting TWO OR MORE mobs at once.
            if (ObjectManager.Aggressors.Count() >= 2)
            {
                TryUseAbility(IntimidatingShout, 25, !(ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(IntimidatingShout) || ObjectManager.Player.HasBuff(Retaliation)) && ObjectManager.Aggressors.All(a => a.Position.DistanceTo(ObjectManager.Player.Position) < 10));

                TryUseAbility(DemoralizingShout, 10, !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(DemoralizingShout));

                TryUseAbility(Cleave, 20,
                    ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 && FacingAllTargets);

                TryUseAbility(Whirlwind, 25, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 && currentStance == BerserkerStance && !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(IntimidatingShout) && AggressorsInMelee);

                // if our ObjectManager.GetTarget(ObjectManager.Player) uses melee, but there's a caster attacking us, do not use retaliation
                TryUseAbility(Retaliation, 0, ObjectManager.Player.IsSpellReady(Retaliation) && !spellcastingAggressors.Any() && currentStance == BattleStance && FacingAllTargets && !ObjectManager.Aggressors.Any(a => a.HasDebuff(IntimidatingShout)));
            }

            // Use these abilities if you are fighting only one mob at a time, or multiple and one or more are not in melee range.
            if (ObjectManager.Aggressors.Any() || (ObjectManager.Aggressors.Count() > 1 && !AggressorsInMelee))
            {
                TryUseAbility(Slam, 15, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 20 && slamReady, SlamCallback);

                // TryUseAbility(Rend, 10, (currentStance == BattleStance && ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 50 && !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Rend) && (ObjectManager.GetTarget(ObjectManager.Player).CreatureType != CreatureType.Elemental && ObjectManager.GetTarget(ObjectManager.Player).CreatureType != CreatureType.Undead)));

                TryUseAbility(Bloodthirst, 30);

                TryUseAbility(Hamstring, 10, ObjectManager.GetTarget(ObjectManager.Player).CreatureType == CreatureType.Humanoid && !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(Hamstring));

                TryUseAbility(HeroicStrike, ObjectManager.Player.Level < 30 ? 15 : 45, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent > 30);

                TryUseAbility(Execute, 15, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent < 20);

                TryUseAbility(SunderArmor, 15, ObjectManager.GetTarget(ObjectManager.Player).HealthPercent < 80 && !ObjectManager.GetTarget(ObjectManager.Player).HasDebuff(SunderArmor));
            }

        }
    }
}
